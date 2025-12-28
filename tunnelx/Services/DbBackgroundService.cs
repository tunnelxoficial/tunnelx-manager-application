using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
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
        
        // Use the DB Host as the VPN Endpoint by default, as no other IP was provided.
        // If this is incorrect, it should be changed here.
        private const string VpnEndpointHost = "64.20.61.66"; 
        
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

            // 2. Generate Client Keys
            var keys = TunnelManager.WireGuardKeyGenerator.GenerateKeyPair();

            // 3. Allocate IP
            // Note: This relies on existing client files in ClientsDir to avoid collisions.
            string clientIpCidr = TunnelManager.AllocateClientAddress();

            // 4. Build Config
            // Using the DB Host as the endpoint IP.
            string clientConfig = TunnelManager.BuildClientConf(keys.PrivateKey, clientIpCidr, VpnEndpointHost);

            // 5. Generate QR Code
            byte[] qrCodeBytes = TunnelManager.BuildAndroidPeerQrPng(clientConfig);

            // 6. Save to Disk (Essential for IP persistence and TunnelManager to find it)
            SaveClientToDisk(client, keys.PublicKey, clientIpCidr);

            // 7. Add Peer to Running Tunnel (if active)
            TunnelManager.AddPeer(keys.PublicKey, clientIpCidr);
            TunnelManager.UnblockClientInternet(clientIpCidr);

            // 8. Update Database
            var updateSql = @"UPDATE Connections 
                              SET config = @config, 
                                  qrcode = @qrcode, 
                                  status_queue = 'CREATED',
                                  status = 'active'
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
