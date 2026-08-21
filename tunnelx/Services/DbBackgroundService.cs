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
        // Using the provided credentials. Assuming MSSQL based on library availability and schema comments.
        private const string ConnectionString = "Server=64.20.61.66;Database=tunnelx;User Id=tunnelx;Password=TuNn3Lx2@25;";
        
        // Endpoint publico entregue no .conf do cliente. Antes era uma constante
        // com o IP do SERVIDOR DE BANCO, o que gerava configuracoes que nunca
        // conectavam. Agora vem de App.config -> appSettings/VpnEndpointHost.
        private static string VpnEndpointHost { get { return TunnelManager.PublicEndpointHost; } }
        
        public event EventHandler ConnectionCreated;

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
                // In a real app, log this error to a file or EventLog
                Console.WriteLine($"Error in background service: {ex.Message}");
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
                    
                    // Fetch clients with status_queue = 'WAIT'
                    var sql = "SELECT id, name FROM Connections WHERE status_queue = 'WAIT'";
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

                    foreach (var client in clients)
                    {
                        try
                        {
                            ProcessClient(client, conn);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing client {client.Id}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database connection error: {ex.Message}");
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
            TunnelManager.AddPeer(keys.PublicKey, clientIpCidr);
            TunnelManager.UnblockClientInternet(clientIpCidr);

            // 8. Update Database
            var updateSql = @"UPDATE Connections 
                              SET config = @config, 
                                  qrcode = @qrcode,
                                  status_queue = 'CREATED'
                              WHERE id = @id";

            using (var cmd = new SqlCommand(updateSql, conn))
            {
                cmd.Parameters.Add("@config", SqlDbType.VarChar, -1).Value = clientConfig; // VARCHAR(MAX)
                cmd.Parameters.Add("@qrcode", SqlDbType.VarBinary, -1).Value = qrCodeBytes; // VARBINARY(MAX)
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = client.Id;
                
                cmd.ExecuteNonQuery();
            }

            Console.WriteLine($"Processed client {client.Id} ({client.Name}) - Assigned IP {clientIpCidr}");
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
