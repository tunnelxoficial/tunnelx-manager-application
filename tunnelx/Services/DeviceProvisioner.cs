using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace tunnelx.Services
{
    /// <summary>
    /// Provisiona UM PEER POR APARELHO — a correcao da causa das quedas.
    /// </summary>
    /// <remarks>
    /// O modelo antigo gerava um par de chaves por Connection (por PLANO) e
    /// entregava o MESMO .conf para as N pessoas. O WireGuard nao suporta isso:
    /// ele guarda, por peer, UM unico endpoint — o IP:porta de onde veio o ultimo
    /// pacote autenticado. Com varios aparelhos na mesma chave, cada um reescrevia
    /// esse campo e o trafego de retorno passava a sair para quem falou por ultimo.
    /// Com PersistentKeepalive de 15s, o revezamento era continuo mesmo com todo
    /// mundo parado: a "oscilacao" que o dono do produto relatou.
    ///
    /// Aqui cada linha de ConnectionDevices vira um peer proprio, com chave e /32
    /// proprios. E o que torna possivel, tambem, medir consumo e aplicar limite de
    /// banda por pessoa — `wg show` conta por peer.
    ///
    /// Convive com o provisionamento antigo de proposito: as Connections ja
    /// emitidas continuam com o peer delas, e os .conf distribuidos seguem
    /// funcionando. Derrubar os clientes atuais para consertar a arquitetura seria
    /// trocar um problema por outro.
    /// </remarks>
    public class DeviceProvisioner
    {
        private const int LoteMaximo = 20;
        private const int TentativasAteFalhar = 5;
        private const int MinutosAteDestravar = 15;

        private readonly string _vpnEndpointHost;

        public DeviceProvisioner(string vpnEndpointHost)
        {
            _vpnEndpointHost = vpnEndpointHost;
        }

        private class DeviceInfo
        {
            public int Id;
            public int ConnectionId;
            public int ClientId;
            public string ClientName;
            public string DeviceName;
        }

        /// <summary>Um ciclo: destrava orfas, reivindica um lote, provisiona e corta.</summary>
        public void ProcessarCiclo(SqlConnection conn)
        {
            RemoverRevogados(conn);
            DestravarOrfas(conn);

            foreach (var device in Reivindicar(conn))
            {
                try
                {
                    Provisionar(device, conn);
                }
                catch (Exception ex)
                {
                    Log.Error($"falha ao provisionar o aparelho {device.Id} (conexao {device.ConnectionId})", ex);
                    DevolverParaFila(device.Id, conn);
                }
            }
        }

        /// <summary>
        /// Devolve para a fila o que ficou preso em PROCESSING.
        ///
        /// Se o processo morrer entre o claim e a conclusao, a linha fica nesse
        /// estado e ninguem mais a pega — o claim so procura WAIT. Sem isto, o
        /// cliente paga e nunca recebe o tunel.
        /// </summary>
        private void DestravarOrfas(SqlConnection conn)
        {
            var sql = $@"
                UPDATE ConnectionDevices
                   SET status_queue = 'WAIT'
                 WHERE status_queue = 'PROCESSING'
                   AND (queue_claimed_at IS NULL
                        OR queue_claimed_at < DATEADD(minute, -{MinutosAteDestravar}, SYSDATETIMEOFFSET()));";

            using (var cmd = new SqlCommand(sql, conn))
            {
                var n = cmd.ExecuteNonQuery();
                if (n > 0) Log.Aviso($"devices: {n} aparelho(s) preso(s) em PROCESSING devolvido(s)");
            }
        }

        /// <summary>
        /// Reivindica um lote, atomicamente.
        ///
        /// ROWLOCK + READPAST faz outra instancia PULAR o que ja foi reivindicado
        /// em vez de esperar, e o OUTPUT devolve as linhas na mesma operacao que
        /// as marca — nao ha janela entre ler e marcar.
        /// </summary>
        private List<DeviceInfo> Reivindicar(SqlConnection conn)
        {
            var sql = $@"
                UPDATE TOP ({LoteMaximo}) d WITH (ROWLOCK, READPAST)
                   SET status_queue = 'PROCESSING',
                       queue_claimed_at = SYSDATETIMEOFFSET()
                OUTPUT inserted.id, inserted.ConnectionId, inserted.ClientId, inserted.device_name
                  FROM ConnectionDevices d
                 WHERE d.status_queue = 'WAIT'
                   AND d.revoked_at IS NULL;";

            var lista = new List<DeviceInfo>();

            using (var cmd = new SqlCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    lista.Add(new DeviceInfo
                    {
                        Id = reader.GetInt32(0),
                        ConnectionId = reader.GetInt32(1),
                        ClientId = reader.GetInt32(2),
                        DeviceName = reader.IsDBNull(3) ? null : reader.GetString(3)
                    });
                }
            }

            // O nome do cliente vem depois, fora do UPDATE: juntar Clients ali
            // seguraria lock na tabela de clientes durante a reivindicacao.
            foreach (var d in lista) d.ClientName = BuscarNomeDoCliente(d.ClientId, conn);

            if (lista.Count > 0) Log.Info($"devices: {lista.Count} aparelho(s) reivindicado(s)");
            return lista;
        }

        private string BuscarNomeDoCliente(int clientId, SqlConnection conn)
        {
            using (var cmd = new SqlCommand("SELECT name FROM Clients WHERE id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", clientId);
                var v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? $"cliente{clientId}" : Convert.ToString(v);
            }
        }

        /// <summary>Gera chave, aloca IP, aplica o peer e grava a config.</summary>
        private void Provisionar(DeviceInfo device, SqlConnection conn)
        {
            TunnelManager.EnsureServerKeys();

            // Reprovisionamento: se este aparelho ja tinha peer, tira o antigo do
            // tunel antes. Sem isto o peer velho ficaria orfao consumindo o IP.
            RemoverPeerAnterior(device.Id, conn);

            var keys = TunnelManager.WireGuardKeyGenerator.GenerateKeyPair();
            var endereco = TunnelManager.AllocateClientAddress();

            var config = TunnelManager.BuildClientConf(keys.PrivateKey, endereco, _vpnEndpointHost);
            var qr = TunnelManager.BuildAndroidPeerQrPng(config);

            GravarNoDisco(device, keys.PublicKey, endereco);
            TunnelManager.WriteServerConfFromClients();

            if (!TunnelManager.AddPeer(keys.PublicKey, endereco))
            {
                throw new InvalidOperationException(
                    $"peer do aparelho {device.Id} nao foi aplicado no tunel");
            }

            TunnelManager.UnblockClientInternet(endereco);

            var sql = @"UPDATE ConnectionDevices
                           SET public_key = @pk,
                               address = @addr,
                               config = @config,
                               qrcode = @qrcode,
                               status_queue = 'CREATED',
                               queue_attempts = 0,
                               updatedAt = SYSDATETIMEOFFSET()
                         WHERE id = @id";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@pk", SqlDbType.NVarChar, 64).Value = keys.PublicKey;
                cmd.Parameters.Add("@addr", SqlDbType.NVarChar, 32).Value = endereco;
                cmd.Parameters.Add("@config", SqlDbType.NVarChar, -1).Value = config;
                cmd.Parameters.Add("@qrcode", SqlDbType.VarBinary, -1).Value = qr;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = device.Id;
                cmd.ExecuteNonQuery();
            }

            Log.Info($"aparelho {device.Id} (cliente {device.ClientId} na conexao {device.ConnectionId}) " +
                     $"provisionado com {endereco}");
        }

        private void RemoverPeerAnterior(int deviceId, SqlConnection conn)
        {
            string chaveAntiga = null;
            using (var cmd = new SqlCommand("SELECT public_key FROM ConnectionDevices WHERE id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", deviceId);
                var v = cmd.ExecuteScalar();
                if (v != null && v != DBNull.Value) chaveAntiga = Convert.ToString(v);
            }

            if (string.IsNullOrWhiteSpace(chaveAntiga)) return;

            Log.Info($"aparelho {deviceId}: removendo peer anterior antes de regerar");
            if (!TunnelManager.RemovePeer(chaveAntiga))
            {
                // Nao da para regerar por cima de um peer que continua no tunel: o
                // antigo ficaria orfao segurando o IP, e o cliente teria dois peers
                // com AllowedIPs conflitantes.
                throw new InvalidOperationException(
                    $"peer anterior do aparelho {deviceId} nao pode ser removido");
            }
            ApagarDoDisco(deviceId);
        }

        /// <summary>
        /// Remove do tunel os aparelhos cortados (convidado removido, prazo vencido).
        ///
        /// Marcar revogado no banco nao tira o peer do servidor: sem este passo, a
        /// pessoa removida continuaria navegando e a vaga do titular continuaria
        /// ocupada. E o mesmo vazamento que este produto ja teve.
        /// </summary>
        private void RemoverRevogados(SqlConnection conn)
        {
            var pendentes = new List<Tuple<int, string>>();

            var sql = @"SELECT TOP (50) id, public_key
                          FROM ConnectionDevices
                         WHERE revoked_at IS NOT NULL
                           AND public_key IS NOT NULL";

            using (var cmd = new SqlCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    pendentes.Add(Tuple.Create(reader.GetInt32(0), reader.GetString(1)));
                }
            }

            foreach (var p in pendentes)
            {
                if (!TunnelManager.RemovePeer(p.Item2))
                {
                    Log.Error($"aparelho {p.Item1}: peer NAO removido do tunel, tentando no proximo ciclo");
                    continue;
                }

                ApagarDoDisco(p.Item1);

                /*
                 * O UPDATE so vale se a linha AINDA estiver revogada.
                 *
                 * Entre o SELECT la em cima e este ponto passam dois processos
                 * wg.exe por linha — segundos, com TOP (50). Nesse intervalo o
                 * titular pode re-convidar a pessoa: garantirDevice limpa o
                 * revoked_at e devolve o aparelho para a fila com WAIT.
                 *
                 * Sem a condicao, este UPDATE atropelaria essa reativacao e
                 * gravaria REVOKED com config nula. Como a reivindicacao so pega
                 * WAIT, o aparelho ficaria parado para sempre: peer ja removido do
                 * tunel, convite ativo, e o convidado em "preparando" sem fim.
                 *
                 * O estado e REVOKED e nao FAILED: FAILED significa "tentei
                 * provisionar 5 vezes e desisti", e misturar os dois faria o painel
                 * mostrar erro onde houve um corte normal.
                 */
                using (var cmd = new SqlCommand(
                    "UPDATE ConnectionDevices SET public_key = NULL, config = NULL, qrcode = NULL," +
                    " address = NULL, status_queue = 'REVOKED'" +
                    " WHERE id = @id AND revoked_at IS NOT NULL", conn))
                {
                    cmd.Parameters.AddWithValue("@id", p.Item1);
                    cmd.ExecuteNonQuery();
                }

                Log.Info($"aparelho {p.Item1}: peer removido do tunel");
            }

            if (pendentes.Count > 0) TunnelManager.WriteServerConfFromClients();
        }

        private void DevolverParaFila(int id, SqlConnection conn)
        {
            try
            {
                var sql = $@"
                    UPDATE ConnectionDevices
                       SET queue_attempts = ISNULL(queue_attempts, 0) + 1,
                           status_queue = CASE WHEN ISNULL(queue_attempts, 0) + 1 >= {TentativasAteFalhar}
                                               THEN 'FAILED' ELSE 'WAIT' END
                     WHERE id = @id;";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"nao foi possivel devolver o aparelho {id} para a fila", ex);
            }
        }

        /* ------------------------------------------------------------ disco -- */

        private static string PastaDoDevice(int deviceId)
        {
            return Path.Combine(TunnelManager.ClientsDir, $"dev_{deviceId}");
        }

        /// <summary>
        /// Grava o client.json que WriteServerConfFromClients relê.
        ///
        /// O JSON e montado com escape. A versao anterior interpolava o nome do
        /// cliente cru; um nome com aspas quebrava o arquivo, e como o mesmo arquivo
        /// e relido para montar o TunnelX.conf, um nome escolhido no cadastro
        /// publico poderia injetar publicKey ou AllowedIPs de terceiros.
        /// </summary>
        private void GravarNoDisco(DeviceInfo device, string publicKey, string enderecoCidr)
        {
            var dir = PastaDoDevice(device.Id);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var nome = Escapar($"{device.ClientName} ({device.DeviceName ?? "aparelho"})");
            var json =
                "{\n" +
                "  \"nome\": \"" + nome + "\",\n" +
                "  \"publicKey\": \"" + Escapar(publicKey) + "\",\n" +
                "  \"address\": \"" + Escapar(enderecoCidr) + "\",\n" +
                "  \"deviceId\": " + device.Id + ",\n" +
                "  \"connectionId\": " + device.ConnectionId + ",\n" +
                "  \"clientId\": " + device.ClientId + ",\n" +
                "  \"enabled\": true,\n" +
                "  \"created_at\": \"" + DateTime.Now.ToString("O") + "\"\n" +
                "}\n";

            File.WriteAllText(Path.Combine(dir, "client.json"), json);
        }

        private void ApagarDoDisco(int deviceId)
        {
            try
            {
                var dir = PastaDoDevice(deviceId);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
                Log.Aviso($"nao foi possivel apagar a pasta do aparelho {deviceId}: {ex.Message}");
            }
        }

        /// <summary>Escapa o que nao pode aparecer cru dentro de uma string JSON.</summary>
        private static string Escapar(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return string.Empty;
            var sb = new System.Text.StringBuilder(valor.Length + 8);
            foreach (var c in valor)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
