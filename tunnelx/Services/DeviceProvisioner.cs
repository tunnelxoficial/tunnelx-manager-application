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

            /// <summary>Plano, titular e prazo — para a tela do operador.</summary>
            public Ficha Ficha;
        }

        /// <summary>Um ciclo: destrava orfas, reivindica um lote, provisiona e corta.</summary>
        public void ProcessarCiclo(SqlConnection conn)
        {
            RemoverRevogados(conn);
            DestravarOrfas(conn);
            AtualizarFichas(conn);

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

            // Os dados de apoio vem depois, fora do UPDATE: juntar Clients e Plans
            // ali seguraria lock nessas tabelas durante a reivindicacao. Aqui o
            // reader do claim ja fechou e nao ha transacao aberta, entao nada
            // fica preso. Uma consulta para o lote inteiro, e nao uma por
            // aparelho como antes.
            if (lista.Count > 0) CarregarFichas(lista, conn);

            if (lista.Count > 0) Log.Info($"devices: {lista.Count} aparelho(s) reivindicado(s)");
            return lista;
        }

        /// <summary>
        /// SQL comum a quem precisa da ficha de um aparelho.
        /// </summary>
        /// <remarks>
        /// O papel sai de <c>d.ClientId = con.ClientId</c>, e NAO de
        /// ConnectionShareId ser nulo. A diferenca e real: quando alguem que foi
        /// convidado compra o proprio plano e volta ao mesmo tunel como titular,
        /// o backend preserva o ConnectionShareId antigo (deviceService.js faz
        /// `shareId ?? device.ConnectionShareId`). Pela chave estrangeira, o dono
        /// apareceria na tela como convidado de si mesmo.
        ///
        /// As vagas saem de <c>Connections.total_connections</c> e nao das do
        /// plano: o painel permite ajustar esse numero por conexao, e e o numero
        /// da conexao que o servidor usa para recusar um convite sem vaga.
        /// </remarks>
        private const string SelectFicha = @"
            SELECT d.id,
                   cli.name  AS usuario,
                   dono.name AS dono,
                   p.name    AS plano,
                   CASE WHEN d.ClientId = con.ClientId THEN 1 ELSE 0 END AS titular,
                   s.duration_label,
                   s.expires_at,
                   con.total_connections,
                   d.ConnectionId,
                   ISNULL(con.data_limit, p.dataLimit) AS velocidade
              FROM ConnectionDevices d
              JOIN Connections con ON con.id = d.ConnectionId
              JOIN Clients cli     ON cli.id = d.ClientId
         LEFT JOIN Clients dono    ON dono.id = con.ClientId
         LEFT JOIN Plans p         ON p.id  = con.PlanId
         LEFT JOIN ConnectionShares s ON s.id = d.ConnectionShareId";

        /// <summary>Preenche nome e ficha de um lote de aparelhos numa consulta so.</summary>
        private void CarregarFichas(List<DeviceInfo> lote, SqlConnection conn)
        {
            var porId = new Dictionary<int, DeviceInfo>();
            foreach (var d in lote) porId[d.Id] = d;

            // Os ids vem do OUTPUT do proprio UPDATE, nao de fora: sao inteiros
            // lidos do banco, e interpola-los aqui nao abre porta para injecao.
            var ids = string.Join(",", porId.Keys);

            try
            {
                using (var cmd = new SqlCommand(SelectFicha + " WHERE d.id IN (" + ids + ")", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DeviceInfo alvo;
                        if (!porId.TryGetValue(reader.GetInt32(0), out alvo)) continue;

                        alvo.ClientName = Texto(reader, 1) ?? ("cliente" + alvo.ClientId);
                        alvo.Ficha = MontarFicha(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                // A ficha e informacao de tela. Perde-la nao pode impedir o
                // provisionamento, que e o que o cliente esta esperando.
                Log.Aviso("nao foi possivel carregar as fichas do lote: " + ex.Message);
            }

            foreach (var d in lote)
                if (string.IsNullOrEmpty(d.ClientName)) d.ClientName = "cliente" + d.ClientId;
        }

        /// <summary>Monta a ficha a partir das colunas de <see cref="SelectFicha"/>.</summary>
        private static Ficha MontarFicha(System.Data.IDataRecord r)
        {
            var titular = !r.IsDBNull(4) && r.GetInt32(4) == 1;

            return new Ficha
            {
                Dono = Texto(r, 2),
                Plano = Texto(r, 3),
                Papel = titular ? "titular" : "convidado",
                // O rotulo do prazo ja vem pronto do servidor (utils/shareRules.js).
                // Reescrever isso em C# criaria um terceiro formato para a mesma
                // informacao, e o operador leria um numero diferente do que o
                // cliente ve no aplicativo.
                Prazo = titular ? null : Texto(r, 5),
                Expira = titular ? null : Instante(r, 6),
                Vagas = r.IsDBNull(7) ? 1 : Math.Max(1, r.GetInt32(7)),
                Conexao = r.IsDBNull(8) ? 0 : r.GetInt32(8),

                // A conexao manda sobre o plano: e o que permite dar uma velocidade
                // diferente a um cliente sem precisar inventar um plano novo.
                Velocidade = r.IsDBNull(9) ? 0 : r.GetInt32(9)
            };
        }

        /// <summary>
        /// Le uma coluna de data e devolve o instante em UTC, no formato ISO-8601.
        /// </summary>
        /// <remarks>
        /// As colunas de data deste banco sao DATETIMEOFFSET, e GetDateTime sobre
        /// DATETIMEOFFSET lanca InvalidCastException — nao devolve a data sem o
        /// fuso, LANCA. Como toda a atualizacao de fichas roda dentro de um
        /// try/catch que so escreve no log, o efeito era o passo inteiro falhar em
        /// silencio: nenhuma ficha gravada, coluna de plano vazia para a base toda,
        /// e uma unica linha de aviso no arquivo de log para explicar.
        ///
        /// DateTimeOffset tambem nao implementa IConvertible, entao Convert.ToDateTime
        /// falharia do mesmo jeito. Daí a checagem de tipo explicita.
        /// </remarks>
        private static string Instante(System.Data.IDataRecord r, int i)
        {
            if (r.IsDBNull(i)) return null;

            var valor = r.GetValue(i);

            if (valor is DateTimeOffset)
                return ((DateTimeOffset)valor).UtcDateTime.ToString("O");

            if (valor is DateTime)
                return ((DateTime)valor).ToUniversalTime().ToString("O");

            return null;
        }

        private static string Texto(System.Data.IDataRecord r, int i)
        {
            if (r.IsDBNull(i)) return null;
            var v = Convert.ToString(r.GetValue(i));
            return string.IsNullOrWhiteSpace(v) ? null : v;
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


        /* ------------------------------------------------------------ fichas -- */

        /// <summary>
        /// Mantem a ficha de TODOS os clientes em dia, a cada ciclo.
        /// </summary>
        /// <remarks>
        /// O provisionamento grava a ficha do aparelho que acabou de criar, mas os
        /// que ja existiam nunca passariam por ali: nenhum passo do ciclo olha para
        /// CREATED. Sem esta varredura, a coluna de plano nasceria vazia para toda a
        /// base atual e so se preencheria conforme as pessoas fossem reprovisionadas.
        ///
        /// Tambem e o que faz a tela acompanhar mudanca de plano ou troca de prazo
        /// feita no painel, sem ninguem precisar reprovisionar nada.
        ///
        /// Custo: duas consultas por ciclo de 60 segundos. A gravacao so acontece
        /// quando o conteudo muda (ver Ficha.Gravar), entao em regime normal isto
        /// nao toca no disco.
        /// </remarks>
        private void AtualizarFichas(SqlConnection conn)
        {
            try
            {
                var n = AtualizarFichasDeAparelhos(conn) + AtualizarFichasDoLegado(conn);
                if (n > 0) Log.Info($"fichas: {n} atualizada(s)");
            }
            catch (Exception ex)
            {
                // A ficha e informacao de tela: falhar aqui nao pode atrapalhar o
                // provisionamento, que e o que o cliente esta esperando.
                Log.Aviso("nao foi possivel atualizar as fichas: " + ex.Message);
            }
        }

        /// <summary>Fichas dos aparelhos do modelo novo, em pastas dev_&lt;id&gt;.</summary>
        private int AtualizarFichasDeAparelhos(SqlConnection conn)
        {
            var pendentes = new List<KeyValuePair<int, Ficha>>();

            using (var cmd = new SqlCommand(SelectFicha + " WHERE d.revoked_at IS NULL", conn))
            using (var reader = cmd.ExecuteReader())
            {
                // Le tudo antes de escrever: manter o reader aberto enquanto se mexe
                // no disco segura a conexao por muito mais tempo que o necessario.
                while (reader.Read())
                    pendentes.Add(new KeyValuePair<int, Ficha>(reader.GetInt32(0), MontarFicha(reader)));
            }

            var gravadas = 0;
            foreach (var p in pendentes)
            {
                var pasta = PastaDoDevice(p.Key);
                // Pasta ausente e normal: o aparelho pode estar na fila ainda. Criar
                // uma pasta so com a ficha faria aparecer na grade uma linha sem peer.
                if (!Directory.Exists(pasta)) continue;
                if (Ficha.Gravar(pasta, p.Value)) gravadas++;
            }

            return gravadas;
        }

        /// <summary>
        /// Fichas das conexoes do modelo antigo, que tem peer proprio no disco.
        /// </summary>
        /// <remarks>
        /// Sao a maioria da base hoje, e ficariam com plano em branco se so o modelo
        /// novo fosse coberto — o operador leria isso como defeito da coluna nova.
        ///
        /// Existem duas convencoes de nome de pasta, ambas em producao:
        ///   "&lt;id&gt;_&lt;nome&gt;"  gravada por DbBackgroundService.SaveClientToDisk
        ///   "&lt;cpf&gt;"         gravada pela tela, em btGerarConfig/btGerarQr
        /// A primeira casa pelo id; a segunda pelo CPF, comparando so os digitos,
        /// porque o cadastro guarda ora "064.767.391-66" ora "06476739166".
        /// </remarks>
        private int AtualizarFichasDoLegado(SqlConnection conn)
        {
            if (!Directory.Exists(TunnelManager.ClientsDir)) return 0;

            var porId = new Dictionary<int, Ficha>();
            var porCpf = new Dictionary<string, Ficha>(StringComparer.Ordinal);

            var sql = @"
                SELECT con.id, con.cpf, cli.name, p.name, con.total_connections,
                       ISNULL(con.data_limit, p.dataLimit) AS velocidade
                  FROM Connections con
             LEFT JOIN Clients cli ON cli.id = con.ClientId
             LEFT JOIN Plans p     ON p.id  = con.PlanId";

            using (var cmd = new SqlCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var ficha = new Ficha
                    {
                        Dono = Texto(reader, 2),
                        Plano = Texto(reader, 3),
                        Papel = "titular",
                        Vagas = reader.IsDBNull(4) ? 1 : Math.Max(1, reader.GetInt32(4)),
                        Velocidade = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                    };

                    porId[reader.GetInt32(0)] = ficha;
                    ficha.Conexao = reader.GetInt32(0);

                    var cpf = SoDigitos(Texto(reader, 1));
                    if (cpf.Length == 0) continue;

                    // Duas conexoes com o mesmo CPF — assinatura antiga e nova, por
                    // exemplo — disputariam a mesma pasta, e venceria a que o banco
                    // devolvesse por ultimo. Sem ORDER BY isso muda sozinho de um
                    // ciclo para o outro, e o resultado nao e erro visivel: e um
                    // plano plausivel atribuido a pessoa errada. Marcamos a colisao
                    // com null e nao afirmamos nada.
                    if (porCpf.ContainsKey(cpf)) porCpf[cpf] = null;
                    else porCpf[cpf] = ficha;
                }
            }

            var gravadas = 0;

            foreach (var pasta in Directory.GetDirectories(TunnelManager.ClientsDir))
            {
                var nome = Path.GetFileName(pasta);
                if (nome.StartsWith("dev_", StringComparison.OrdinalIgnoreCase)) continue;

                // Cada convencao e reconhecida pela FORMA do nome, e uma pasta so
                // tenta a convencao que combina com ela. Chutar as duas em toda pasta
                // deixaria "12_JOAO", cujo id 12 nao existe, cair no casamento por CPF
                // com a chave "12" — e o resultado de um chute errado nao e erro
                // visivel, e o plano de outra pessoa exibido como fato.
                Ficha ficha = null;

                var separador = nome.IndexOf(Convert.ToChar(95));   // _
                int id;

                if (separador > 0 && int.TryParse(nome.Substring(0, separador), out id))
                {
                    // "<id>_<nome>", do provisionamento antigo por conexao.
                    porId.TryGetValue(id, out ficha);
                }
                else if (separador < 0 && ParecCpf(nome))
                {
                    // "<cpf>", do caminho manual da tela.
                    porCpf.TryGetValue(SoDigitos(nome), out ficha);
                }

                if (ficha == null)
                {
                    // Sem correspondencia no banco: cliente avulso, ou cadastro que
                    // sumiu. Nao inventamos ficha — mas uma ficha ANTIGA aqui afirma
                    // um plano que ja nao vale, entao ela sai.
                    if (ApagarFichaObsoleta(pasta)) gravadas++;
                    continue;
                }

                if (Ficha.Gravar(pasta, ficha)) gravadas++;
            }

            return gravadas;
        }

        /// <summary>O nome da pasta tem a cara de um CPF: so digitos e pontuacao.</summary>
        private static bool ParecCpf(string nome)
        {
            if (string.IsNullOrEmpty(nome)) return false;

            foreach (var c in nome)
            {
                var digito = c >= Convert.ToChar(48) && c <= Convert.ToChar(57);
                if (!digito && c != Convert.ToChar(46) && c != Convert.ToChar(45)) return false;   // . -
            }

            return SoDigitos(nome).Length == 11;
        }

        /// <summary>
        /// Tira a ficha de uma pasta que nao tem mais correspondencia no banco.
        /// </summary>
        /// <remarks>
        /// Sem isto, apagar a conexao no painel ou corrigir um CPF no cadastro
        /// deixaria a tela exibindo para sempre o plano e o titular de um registro
        /// que ja nao existe.
        /// </remarks>
        private static bool ApagarFichaObsoleta(string pasta)
        {
            try
            {
                var caminho = Path.Combine(pasta, Ficha.NomeArquivo);
                if (!File.Exists(caminho)) return false;

                File.Delete(caminho);
                Log.Aviso($"ficha obsoleta removida de {pasta}: sem correspondencia no banco");
                return true;
            }
            catch (Exception ex)
            {
                Log.Aviso($"nao foi possivel remover a ficha obsoleta de {pasta}: {ex.Message}");
                return false;
            }
        }

        private static string SoDigitos(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s) if (c >= Convert.ToChar(48) && c <= Convert.ToChar(57)) sb.Append(c);
            return sb.ToString();
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

            var nome = Json.Escapar($"{device.ClientName} ({device.DeviceName ?? "aparelho"})");
            var json =
                "{\n" +
                "  \"nome\": \"" + nome + "\",\n" +
                "  \"publicKey\": \"" + Json.Escapar(publicKey) + "\",\n" +
                "  \"address\": \"" + Json.Escapar(enderecoCidr) + "\",\n" +
                "  \"deviceId\": " + device.Id + ",\n" +
                "  \"connectionId\": " + device.ConnectionId + ",\n" +
                "  \"clientId\": " + device.ClientId + ",\n" +
                "  \"enabled\": true,\n" +
                "  \"created_at\": \"" + DateTime.Now.ToString("O") + "\"\n" +
                "}\n";

            File.WriteAllText(Path.Combine(dir, "client.json"), json);

            // A ficha vai no mesmo passo, e nao so na varredura de 60 em 60
            // segundos: o reprovisionamento apaga a pasta inteira antes de
            // recria-la, e sem isto o plano e o titular sumiriam da tela ate a
            // proxima varredura.
            Ficha.Gravar(dir, device.Ficha);
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
    }
}
