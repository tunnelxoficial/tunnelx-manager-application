using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace tunnelx.Services
{
    public class DbBackgroundService
    {
        private System.Windows.Forms.Timer _timer;
        private bool _isProcessing;

        /// <summary>
        /// Conexao com o banco, vinda do ambiente ou do App.config.
        ///
        /// Era uma constante com usuario e senha em texto puro, compilada no
        /// binario e versionada no git. Duas consequencias: qualquer um com
        /// acesso ao repositorio tem a senha do banco de producao, e ela
        /// permanece no historico mesmo depois de removida daqui — por isso
        /// tirar do codigo nao basta, e preciso ROTACIONAR a senha.
        ///
        /// Encrypt=true e obrigatorio e nao e detalhe: este provisionador grava
        /// a CHAVE PRIVADA de cada cliente no banco, e o banco esta num IP
        /// publico. Sem TLS, cada chave atravessa a internet em claro.
        /// </summary>
        private static string ConnectionString
        {
            get
            {
                var doAmbiente = Environment.GetEnvironmentVariable("TUNNELX_DB");
                var bruta = !string.IsNullOrWhiteSpace(doAmbiente)
                    ? doAmbiente
                    : System.Configuration.ConfigurationManager.AppSettings["DbConnectionString"];

                if (string.IsNullOrWhiteSpace(bruta))
                {
                    throw new InvalidOperationException(
                        "Conexao com o banco nao configurada. Defina a variavel de ambiente " +
                        "TUNNELX_DB ou a chave DbConnectionString no App.config.");
                }

                // Forca TLS mesmo que a string configurada tenha esquecido.
                if (bruta.IndexOf("Encrypt", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    bruta = bruta.TrimEnd(';') + ";Encrypt=true;TrustServerCertificate=true;";
                }
                return bruta;
            }
        }
        
        // Endpoint publico entregue no .conf do cliente. Antes era uma constante
        // com o IP do SERVIDOR DE BANCO, o que gerava configuracoes que nunca
        // conectavam. Agora vem de App.config -> appSettings/VpnEndpointHost.
        private static string VpnEndpointHost { get { return TunnelManager.PublicEndpointHost; } }
        
        public event EventHandler ConnectionCreated;

        /// <summary>
        /// Provisiona um peer por APARELHO — a correcao das quedas.
        ///
        /// Roda no mesmo ciclo da fila de Connections, e nao num timer proprio:
        /// os dois mexem no mesmo arquivo TunnelX.conf e chamam o mesmo wg.exe.
        /// Em timers separados eles se atropelariam, e o resultado seria
        /// justamente o conf truncado e o peer perdido.
        /// </summary>
        private DeviceProvisioner _devices;

        public void Start()
        {
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 60000; // 1 minute
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Run immediately on start
            Task.Run(() => ProcessQueue());
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_isProcessing) return;
            Task.Run(() => ProcessQueue());
        }

        private void ProcessQueue()
        {
            _isProcessing = true;
            try
            {
                ProcessDatabase();
                FlushMemory();
            }
            catch (Exception ex)
            {
                Log.Error("falha no ciclo do servico de fundo", ex);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void ProcessDatabase()
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    
                    /*
                     * Devolve as linhas abandonadas ANTES de reivindicar novas.
                     *
                     * O claim atomico resolveu o provisionamento duplicado, mas
                     * criou um jeito novo de a linha sumir: se o processo morrer
                     * entre o claim e a conclusao — queda de energia, o operador
                     * fechando a janela, uma excecao fora do try — a linha fica em
                     * PROCESSING e ninguem mais a pega, porque o claim so procura
                     * WAIT. O cliente pagou e nunca recebe o tunel.
                     *
                     * Quinze minutos: muito acima do tempo real de provisionar
                     * (segundos), e baixo o bastante para o cliente nao esperar.
                     */
                    using (var reaper = new SqlCommand(@"
                        UPDATE Connections
                           SET status_queue = 'WAIT'
                         WHERE status_queue = 'PROCESSING'
                           AND (queue_claimed_at IS NULL
                                OR queue_claimed_at < DATEADD(minute, -15, SYSDATETIMEOFFSET()));", conn))
                    {
                        var devolvidas = reaper.ExecuteNonQuery();
                        if (devolvidas > 0)
                            Log.Aviso($"fila: {devolvidas} conexao(oes) presa(s) em PROCESSING devolvida(s)");
                    }

                    /*
                     * Reivindica as linhas em vez de so le-las.
                     *
                     * O SELECT simples nao marcava nada: se duas instancias do
                     * provisionador rodassem — ou se a mesma reiniciasse no meio de
                     * um lote — a mesma Connection seria provisionada duas vezes,
                     * gerando dois peers e consumindo dois IPs do pool para um
                     * cliente so.
                     *
                     * O UPDATE ... OUTPUT faz a leitura e a marcacao numa operacao
                     * atomica. ROWLOCK evita escalar para lock de tabela; READPAST
                     * faz outra instancia pular o que ja esta reivindicado em vez de
                     * ficar bloqueada esperando.
                     *
                     * TOP (20): um lote por ciclo. Provisionar e caro (gera chaves,
                     * escreve arquivo, chama o wg.exe) e nada disso deve segurar a
                     * conexao com o banco por minutos.
                     */
                    var sql = @"
                        UPDATE TOP (20) Connections WITH (ROWLOCK, READPAST)
                           SET status_queue = 'PROCESSING',
                               queue_claimed_at = SYSDATETIMEOFFSET()
                        OUTPUT inserted.id, inserted.name
                         WHERE status_queue = 'WAIT';";
                    var clients = new List<ClientInfo>();

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            clients.Add(new ClientInfo
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1)
                            });
                        }
                    }

                    if (clients.Count > 0)
                        Log.Info($"fila: {clients.Count} conexao(oes) reivindicada(s)");

                    /*
                     * A fila de APARELHOS, no mesmo ciclo e na mesma conexao.
                     *
                     * Vem antes de provisionar as Connections de proposito: um
                     * aparelho esperando peer e um cliente olhando a tela agora,
                     * enquanto uma Connection nova e uma venda que acabou de
                     * entrar e ainda nao tem ninguem esperando.
                     */
                    try
                    {
                        if (_devices == null) _devices = new DeviceProvisioner(VpnEndpointHost);
                        _devices.ProcessarCiclo(conn);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("falha no ciclo de aparelhos", ex);
                    }

                    foreach (var client in clients)
                    {
                        try
                        {
                            ProcessClient(client, conn);
                        }
                        catch (Exception ex)
                        {
                            /*
                             * Devolve a linha para a fila.
                             *
                             * Com o claim atomico a linha esta em PROCESSING; sem
                             * devolve-la, uma falha transitoria (wg.exe ocupado, disco
                             * cheio) deixaria o cliente preso nesse estado para sempre,
                             * pago e sem tunel, sem ninguem saber.
                             */
                            Log.Error($"falha ao provisionar a conexao {client.Id} ({client.Name})", ex);
                            DevolverParaFila(client.Id, conn);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("falha ao falar com o banco", ex);
            }
        }

        /// <summary>
        /// Devolve uma linha reivindicada para WAIT, contando a tentativa.
        ///
        /// Depois de 5 tentativas a linha vai para FAILED e para de girar: um
        /// erro permanente (nome invalido, pool de IPs esgotado) reprocessado
        /// a cada 60 segundos consome o provisionador inteiro e esconde as
        /// linhas boas atras dele.
        /// </summary>
        private void DevolverParaFila(int id, SqlConnection conn)
        {
            try
            {
                var sql = @"
                    UPDATE Connections
                       SET queue_attempts = ISNULL(queue_attempts, 0) + 1,
                           status_queue = CASE WHEN ISNULL(queue_attempts, 0) + 1 >= 5
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
                Log.Error($"nao foi possivel devolver a conexao {id} para a fila", ex);
            }
        }

        private void ProcessClient(ClientInfo client, SqlConnection conn)
        {
            // 1. Ensure Server Keys exist
            TunnelManager.EnsureServerKeys();

            // 1.1 Re-provisionamento: se ja existe pasta deste cliente, esta e uma
            // regeracao (status_queue devolvido para WAIT pelo painel). Sem tratar isso,
            // o peer antigo ficaria orfao no tunel e o IP antigo voltaria ao pool,
            // podendo ser entregue a outro cliente -- colisao dentro de 10.66.66.0/24.
            string oldPublicKey = null, oldAddress = null;
            try
            {
                if (Directory.Exists(TunnelManager.ClientsDir))
                {
                    var dirAnterior = Directory.GetDirectories(TunnelManager.ClientsDir)
                        .FirstOrDefault(d => Path.GetFileName(d).StartsWith(client.Id + "_"));

                    if (dirAnterior != null)
                    {
                        var jsonAnterior = Path.Combine(dirAnterior, "client.json");
                        if (File.Exists(jsonAnterior))
                        {
                            foreach (var linha in File.ReadAllLines(jsonAnterior))
                            {
                                var idx = linha.IndexOf(':');
                                if (idx <= 0) continue;
                                var valor = linha.Substring(idx + 1).Trim().Trim('"', ',', ' ');
                                if (linha.IndexOf("\"publicKey\"", StringComparison.OrdinalIgnoreCase) >= 0) oldPublicKey = valor;
                                else if (linha.IndexOf("\"address\"", StringComparison.OrdinalIgnoreCase) >= 0) oldAddress = valor;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("Falha ao ler estado anterior do cliente: " + ex.Message); }

            if (!string.IsNullOrWhiteSpace(oldPublicKey))
                TunnelManager.RemovePeer(oldPublicKey);

            // 2. Generate Client Keys
            var keys = TunnelManager.WireGuardKeyGenerator.GenerateKeyPair();

            // 3. Allocate IP -- reaproveita o endereco anterior numa regeracao para nao
            // consumir outro IP do pool a cada re-provisionamento.
            string clientIpCidr = !string.IsNullOrWhiteSpace(oldAddress)
                ? oldAddress
                : TunnelManager.AllocateClientAddress();

            // 4. Build Config
            // Using the DB Host as the endpoint IP.
            string clientConfig = TunnelManager.BuildClientConf(keys.PrivateKey, clientIpCidr, VpnEndpointHost);

            // 5. Generate QR Code
            byte[] qrCodeBytes = TunnelManager.BuildAndroidPeerQrPng(clientConfig);

            // 6. Save to Disk (Essential for IP persistence and TunnelManager to find it)
            SaveClientToDisk(client, keys.PublicKey, clientIpCidr);

            // 6.1 Persiste o peer no TunnelX.conf. Sem isto o cliente criado pela fila
            // existe apenas em runtime (o wg set do passo 7) e no client.json, e some
            // no proximo restart do servico do tunel ou reboot do servidor.
            TunnelManager.WriteServerConfFromClients();

            // 7. Add Peer to Running Tunnel (if active)
            //
            // Se o peer nao entrou, a conexao NAO pode ser marcada CREATED: o
            // cliente apareceria pronto no painel, baixaria o .conf e nao
            // conectaria. Lancar aqui devolve a linha para a fila (o chamador
            // trata) e a tentativa fica contada.
            if (!TunnelManager.AddPeer(keys.PublicKey, clientIpCidr))
            {
                throw new InvalidOperationException(
                    $"peer nao foi aplicado no tunel para a conexao {client.Id}");
            }
            TunnelManager.UnblockClientInternet(clientIpCidr);

            // 8. Update Database
            // queue_attempts volta a zero: sem isso, uma conexao que falhou 4
            // vezes, foi corrigida e provisionada, iria direto para FAILED na
            // primeira falha de um reprovisionamento futuro.
            var updateSql = @"UPDATE Connections 
                              SET config = @config, 
                                  qrcode = @qrcode,
                                  status_queue = 'CREATED',
                                  queue_attempts = 0
                              WHERE id = @id";

            using (var cmd = new SqlCommand(updateSql, conn))
            {
                cmd.Parameters.Add("@config", SqlDbType.VarChar, -1).Value = clientConfig; // VARCHAR(MAX)
                cmd.Parameters.Add("@qrcode", SqlDbType.VarBinary, -1).Value = qrCodeBytes; // VARBINARY(MAX)
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = client.Id;
                
                cmd.ExecuteNonQuery();
            }

            Log.Info($"conexao {client.Id} ({client.Name}) provisionada com o IP {clientIpCidr}");
            ConnectionCreated?.Invoke(this, EventArgs.Empty);
        }

        private void SaveClientToDisk(ClientInfo client, string publicKey, string addressCidr)
        {
            // Use ID and Name for folder uniqueness
            string safeName = string.Join("_", client.Name.Split(Path.GetInvalidFileNameChars()));
            string folderName = $"{client.Id}_{safeName}";
            string dirPath = Path.Combine(TunnelManager.ClientsDir, folderName);

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            string jsonPath = Path.Combine(dirPath, "client.json");
            
            // Simple JSON construction to avoid external dependency if not needed, 
            // matching the format TunnelManager expects.
            string jsonContent = $@"{{
  ""nome"": ""{client.Name}"",
  ""publicKey"": ""{publicKey}"",
  ""address"": ""{addressCidr}"",
  ""enabled"": true,
  ""created_at"": ""{DateTime.Now:O}""
}}";
            File.WriteAllText(jsonPath, jsonContent);
        }

        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        private void FlushMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch { }
        }

        private class ClientInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
