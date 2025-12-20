using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows.Forms;
using tunnelx.Models;
using tunnelx.Services;
using tunnelx.Strings;
using static tunnelx.Strings.Language;

namespace tunnelx
{
    public partial class frmDefault : Form
    {
        private List<InterfaceModel> listaInterfaces = new List<InterfaceModel>();
        private Language _language;
        private bool _lastTunnelActive;


        #region ... MÉTODOS ...

        public frmDefault()
        {
            InitializeComponent();
        }

        private void ListarInterfaces()
        {
            dtgInterfaces.Visible = false;
            lblGridListaInterfaces.Text = _language.Text("loading");
            lblGridListaInterfaces.Visible = true;

            // Puxa interfaces do computador
            listaInterfaces = new List<InterfaceModel>();

            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!nic.Name.Equals("TunnelX"))
                {
                    listaInterfaces.Add(new InterfaceModel
                    {
                        description = nic.Description,
                        name = nic.Name,
                        networkInterfaceType = nic.NetworkInterfaceType,
                        operationalStatus = nic.OperationalStatus,
                        speed = nic.Speed,
                        physicalAddress = nic.GetPhysicalAddress(),
                        ipv4InterfaceStatistics = nic.GetIPv4Statistics(),
                        ipInterfaceStatistics = nic.GetIPStatistics(),
                        ipInterfaceProperties = nic.GetIPProperties()
                    });
                }
            }

            // Lista interfaces em Gride
            if (listaInterfaces.Count > 0)
            {
                dtgInterfaces.Rows.Clear();

                foreach (var _interface in listaInterfaces)
                {
                    var icon = "❌";
                    var color = Color.DarkGray;

                    switch (_interface.operationalStatus)
                    {
                        case System.Net.NetworkInformation.OperationalStatus.Up:
                            if (_interface.ipv4InterfaceStatistics.BytesReceived > 0
                                && _interface.ipv4InterfaceStatistics.BytesSent > 0)
                            {
                                icon = "✔️";
                                color = Color.Black;
                            }
                            break;
                    }

                    dtgInterfaces.Rows.Add(false, _interface.name, icon);
                    dtgInterfaces.Rows[dtgInterfaces.Rows.Count - 1].Tag = _interface;

                    if (icon == "❌")
                    {
                        dtgInterfaces.Rows[dtgInterfaces.Rows.Count - 1].DefaultCellStyle.ForeColor = color;

                        dtgInterfaces.Rows[dtgInterfaces.Rows.Count - 1].Cells[2].Style.ForeColor = Color.Red;
                        dtgInterfaces.Rows[dtgInterfaces.Rows.Count - 1].Cells[2].ToolTipText = _language.Text("no-signal");
                    }
                    else
                    {
                        dtgInterfaces.Rows[dtgInterfaces.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.ForestGreen;

                        dtgInterfaces.Rows[dtgInterfaces.Rows.Count - 1].Cells[2].Style.ForeColor = Color.ForestGreen;
                        dtgInterfaces.Rows[dtgInterfaces.Rows.Count - 1].Cells[2].ToolTipText = _language.Text("signal");
                    }
                }

                lblGridListaInterfaces.Visible = false;
                dtgInterfaces.Visible = true;
                dtgInterfaces.ClearSelection();
            }
            else
            {
                lblGridListaInterfaces.Text = _language.Text("no-search");
            }
        }

        private bool IsTunnelActive(string tunnelName = "TunnelX")
        {
            var query = "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject adapter in searcher.Get())
                {
                    var name = adapter["NetConnectionID"]?.ToString();
                    if (string.Equals(name, tunnelName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }

        private void ValidaConexao()
        {
            if (IsTunnelActive())
            {
                // Online e Funcionando
                dtgInterfaces.Enabled = false;
                btCriarTunel.Enabled = false;
                btStop.Enabled = true;
                btCriarTunel.Visible = false;
                btStop.Visible = true;
                btGerarQr.Visible = true;
                btGerarConfig.Visible = true;
                if (!_lastTunnelActive)
                {
                    RestoreAfterTunnelRestart();
                    _lastTunnelActive = true;
                }
            }
            else
            {
                // Caiu
                dtgInterfaces.Enabled = true;
                btCriarTunel.Enabled = true;
                btStop.Enabled = false;
                btCriarTunel.Visible = true;
                btStop.Visible = false;
                btGerarQr.Visible = false;
                btGerarConfig.Visible = false;
                _lastTunnelActive = false;
            }
        }

        private void RestoreAfterTunnelRestart()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(TunnelManager.InternetInterfaceName))
                    WireGuardManager.ApplyWifiToTunnelx(TunnelManager.InternetInterfaceName, TunnelManager.WireGuardInterfaceName);
                TunnelManager.WriteServerConfFromClients();
                TunnelManager.ReloadTunnel();
                var root = System.IO.Path.Combine(TunnelManager.ConfDir, "clients");
                if (!Directory.Exists(root)) return;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var json = System.IO.Path.Combine(dir, "client.json");
                    if (!File.Exists(json)) continue;
                    string pk = null, addr = null, enabledStr = null;
                    var lines = File.ReadAllLines(json);
                    foreach (var line in lines)
                    {
                        var l = line.Trim();
                        if (pk == null && l.IndexOf("\"publicKey\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var idx = l.IndexOf(':');
                            if (idx > 0) pk = l.Substring(idx + 1).Trim().Trim('"', ',', ' ');
                        }
                        else if (addr == null && l.IndexOf("\"address\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var idx = l.IndexOf(':');
                            if (idx > 0) addr = l.Substring(idx + 1).Trim().Trim('"', ',', ' ');
                        }
                        else if (enabledStr == null && l.IndexOf("\"enabled\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var idx = l.IndexOf(':');
                            if (idx > 0) enabledStr = l.Substring(idx + 1).Trim().Trim(',', ' ').ToLowerInvariant();
                        }
                    }
                    bool enabled = string.Equals(enabledStr, "true", StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(pk) && !string.IsNullOrWhiteSpace(addr))
                    {
                        if (enabled)
                        {
                            TunnelManager.AddPeer(pk, addr);
                            TunnelManager.UnblockClientInternet(addr);
                        }
                        else
                        {
                            TunnelManager.BlockClientInternet(addr);
                        }
                    }
                }
                UpdateActiveConnections();
            }
            catch { }
        }

        private void StopTunnelHard(string tunnelName = "TunnelX", int wgPort = 51820)
        {
            // 1) Tenta parar o serviço do túnel
            RunAdmin($@"sc stop ""WireGuardTunnel${tunnelName}""");

            // 2) Desinstala o serviço do túnel (evita auto-restart)
            RunAdmin($@"wireguard /uninstalltunnelservice ""{tunnelName}""");

            // 3) Se existir wg-quick, derruba também (não faz mal repetir)
            RunAdmin($@"where wg-quick && wg-quick down ""{tunnelName}""");

            // 4) Desativa a interface de rede com esse NetConnectionID
            DisableNetAdapterByConnectionId(tunnelName);

            // 5) Desliga ICS no TunnelX (caso tenha sido marcado como PRIVATE)
            DisableIcsOn(tunnelName);

            // 6) Limpeza de rotas e firewall (idempotente)
            RunAdmin($@"route delete 10.66.66.0 mask 255.255.255.0");
            RunAdmin($@"netsh advfirewall firewall delete rule name=""WireGuard UDP {wgPort}""");

            // 7) Verificação final
            bool stillUp = IsTunnelUp(tunnelName);
            Console.WriteLine(stillUp
                ? "⚠️ O TunnelX ainda parece ativo (verifique UI do WireGuard)."
                : "✅ TunnelX parado e limpo.");
        }

        private void DisableNetAdapterByConnectionId(string connectionId)
        {
            var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID IS NOT NULL");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["NetConnectionID"]?.ToString();
                if (string.Equals(name, connectionId, StringComparison.OrdinalIgnoreCase))
                {
                    try { obj.InvokeMethod("Disable", null); } catch { /* alguns drivers ignoram */ }
                }
            }
        }

        private void DisableIcsOn(string connectionId)
        {
            try
            {
                var t = Type.GetTypeFromProgID("HNetCfg.HNetShare");
                if (t == null) return;
                dynamic mgr = Activator.CreateInstance(t);
                foreach (dynamic conn in mgr.EnumEveryConnection)
                {
                    var props = mgr.NetConnectionProps[conn];
                    if (!string.Equals((string)props.Name, connectionId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    dynamic cfg = mgr.INetSharingConfigurationForINetConnection[conn];
                    if (cfg.SharingEnabled == true)
                    {
                        try { cfg.DisableSharing(); } catch { /* ignore */ }
                    }
                }
            }
            catch { /* ICS COM pode não estar disponível */ }
        }

        private bool IsTunnelUp(string tunnelName)
        {
            // 1) checa serviço
            var svc = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C sc query \"WireGuardTunnel${tunnelName}\" | find /I \"RUNNING\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            string out1 = svc?.StandardOutput.ReadToEnd() ?? "";
            svc?.WaitForExit();
            if (!string.IsNullOrWhiteSpace(out1)) return true;

            // 2) checa interface conectada
            var q = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2");
            foreach (ManagementObject o in q.Get())
            {
                var name = o["NetConnectionID"]?.ToString();
                if (string.Equals(name, tunnelName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // 3) checa wg.exe (se estiver disponível)
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C where wg && wg show interfaces",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            var p = Process.Start(psi);
            string out2 = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit();
            return out2?.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Any(s => s.Equals(tunnelName, StringComparison.OrdinalIgnoreCase)) == true;
        }

        private void RunAdmin(string cmd)
        {
            var psi = new ProcessStartInfo("cmd.exe", "/C " + cmd)
            {
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            Process.Start(psi)?.WaitForExit();
        }

        #endregion


        #region ... EVENTOS ...

        private void frmTeste_Load(object sender, EventArgs e)
        {
            _language = new Language(language.PT);

            ListarInterfaces();
            dtgInterfaces.ClearSelection();

            ValidaConexao();
            tmrStatus.Start();
            UpdateActiveConnections();
        }

        private void btTeste_Click(object sender, EventArgs e)
        {
            //InternetConnectionSharing.EnableSharing("Ethernet", "WireGuard");

        }

        private void dtgInterfaces_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            var novoValor = !(bool)dtgInterfaces.Rows[e.RowIndex].Cells[0].Value;

            if (novoValor)
            {
                foreach (DataGridViewRow row in dtgInterfaces.Rows)
                    row.Cells[0].Value = false;
            }

            dtgInterfaces.Rows[e.RowIndex].Cells[0].Value = novoValor;
            dtgInterfaces.ClearSelection();
        }

        private void dtgInterfaces_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            dtgInterfaces.ClearSelection();
        }

        private void btCriarTunel_Click(object sender, EventArgs e)
        {
            using (var dlg = new frmSelectInterface())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedInterfaceName))
                {
                    MessageBox.Show(_language.Text("select-interface"), _language.Text("notice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                ValidaConexao();
                UpdateActiveConnections();
            }
        }

        private void btGerarConfig_Click(object sender, EventArgs e)
        {
            using (var form = new frmClientInfo())
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                    return;
                var clientDirRoot = Path.Combine(TunnelManager.ConfDir, "clients");
                var clientDir = Path.Combine(clientDirRoot, SanitizeFileName(form.Cpf));
                Directory.CreateDirectory(clientDirRoot);
                Directory.CreateDirectory(clientDir);

                if (string.IsNullOrEmpty(TunnelManager.ServerPrivateKeyB64) ||
                    string.IsNullOrEmpty(TunnelManager.ServerPublicKeyB64))
                {
                    TunnelManager.GenerateKeys();
                    TunnelManager.WriteServerConf();
                }

                var clientKeyPair = tunnelx.Services.TunnelManager.WireGuardKeyGenerator.GenerateKeyPair();
                var address = TunnelManager.AllocateClientAddress();

                string endpointPublic = NetworkHelper.GetPublicIpv6();
                string conf;
                if (!string.IsNullOrEmpty(endpointPublic))
                    conf = TunnelManager.BuildClientConf(clientKeyPair.PrivateKey, address, "[" + endpointPublic + "]");
                else
                {
                    endpointPublic = NetworkHelper.GetPublicIp();
                    conf = TunnelManager.BuildClientConf(clientKeyPair.PrivateKey, address, endpointPublic);
                }

                var path = Path.Combine(clientDir, "client-peer.conf");
                File.WriteAllText(path, conf);
                var meta = "{\n" +
                           $"  \"nome\": \"{form.Nome}\",\n" +
                           $"  \"celular\": \"{form.Celular}\",\n" +
                           $"  \"email\": \"{form.Email}\",\n" +
                           $"  \"cpf\": \"{form.Cpf}\",\n" +
                           $"  \"publicKey\": \"{clientKeyPair.PublicKey}\",\n" +
                           $"  \"privateKey\": \"{clientKeyPair.PrivateKey}\",\n" +
                           $"  \"address\": \"{address}\",\n" +
                           $"  \"enabled\": true\n" +
                           "}";
                File.WriteAllText(Path.Combine(clientDir, "client.json"), meta);
                MessageBox.Show("Configuração do cliente gerada em:\n" + path + "\n\nAbrindo pasta do cliente…", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                try { System.Diagnostics.Process.Start("explorer.exe", clientDir); } catch { }
                TunnelManager.WriteServerConfFromClients();
                TunnelManager.AddPeer(clientKeyPair.PublicKey, address);
                TunnelManager.UnblockClientInternet(address);
                TunnelManager.ReloadTunnel();
            }
        }

        private void btGerarQr_Click(object sender, EventArgs e)
        {
            try
            {
                using (var form = new frmClientInfo())
                {
                    if (form.ShowDialog(this) != DialogResult.OK)
                        return;
                    var clientDirRoot = Path.Combine(TunnelManager.ConfDir, "clients");
                    var clientDir = Path.Combine(clientDirRoot, SanitizeFileName(form.Cpf));
                    Directory.CreateDirectory(clientDirRoot);
                    Directory.CreateDirectory(clientDir);

                    if (string.IsNullOrEmpty(TunnelManager.ServerPrivateKeyB64) ||
                        string.IsNullOrEmpty(TunnelManager.ServerPublicKeyB64))
                    {
                        TunnelManager.GenerateKeys();
                        TunnelManager.WriteServerConf();
                    }

                    var clientKeyPair = tunnelx.Services.TunnelManager.WireGuardKeyGenerator.GenerateKeyPair();
                    var address = TunnelManager.AllocateClientAddress();

                    string endpointPublic = NetworkHelper.GetPublicIpv6();
                    string conf;
                    if (!string.IsNullOrEmpty(endpointPublic))
                        conf = TunnelManager.BuildClientConf(clientKeyPair.PrivateKey, address, "[" + endpointPublic + "]");
                    else
                    {
                        endpointPublic = NetworkHelper.GetPublicIp();
                        conf = TunnelManager.BuildClientConf(clientKeyPair.PrivateKey, address, endpointPublic);
                    }

                    var png = TunnelManager.BuildAndroidPeerQrPng(conf);
                    var pathPng = Path.Combine(clientDir, "client-peer.png");
                    File.WriteAllBytes(pathPng, png);
                    var meta = "{\n" +
                               $"  \"nome\": \"{form.Nome}\",\n" +
                               $"  \"celular\": \"{form.Celular}\",\n" +
                               $"  \"email\": \"{form.Email}\",\n" +
                               $"  \"cpf\": \"{form.Cpf}\",\n" +
                               $"  \"publicKey\": \"{clientKeyPair.PublicKey}\",\n" +
                               $"  \"privateKey\": \"{clientKeyPair.PrivateKey}\",\n" +
                               $"  \"address\": \"{address}\",\n" +
                               $"  \"enabled\": true\n" +
                               "}";
                    File.WriteAllText(Path.Combine(clientDir, "client.json"), meta);

                    using (var ms = new MemoryStream(png))
                    using (var bmp = new Bitmap(ms))
                    {
                        ShowQrDialog(bmp, pathPng);
                    }
                    try { System.Diagnostics.Process.Start("explorer.exe", clientDir); } catch { }
                    TunnelManager.WriteServerConfFromClients();
                    TunnelManager.AddPeer(clientKeyPair.PublicKey, address);
                    TunnelManager.UnblockClientInternet(address);
                    TunnelManager.ReloadTunnel();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Falha ao gerar QR Code:\n" + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowQrDialog(Image qrImage, string savedPath)
        {
            var dlg = new Form();
            dlg.Text = "QR Code do TunnelX";
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.MinimizeBox = false;
            dlg.MaximizeBox = false;
            dlg.ClientSize = new Size(420, 520);

            var lbl = new Label();
            lbl.Text = "Use o app para escanear este QR.\nArquivo salvo em:\n" + savedPath;
            lbl.AutoSize = false;
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.Dock = DockStyle.Top;
            lbl.Height = 70;

            var pic = new PictureBox();
            pic.Image = (Image)qrImage.Clone();
            pic.SizeMode = PictureBoxSizeMode.Zoom;
            pic.Dock = DockStyle.Fill;
            pic.BackColor = Color.White;

            var panelBottom = new Panel();
            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.Height = 64;

            var btnOk = new Button();
            btnOk.Text = "OK";
            btnOk.Width = 100;
            btnOk.Height = 32;
            btnOk.BackColor = Color.Gainsboro;
            btnOk.FlatStyle = FlatStyle.Standard;
            btnOk.Font = new Font("Trebuchet MS", 10F, FontStyle.Regular);
            btnOk.Location = new Point((dlg.ClientSize.Width - btnOk.Width) / 2, 16);
            btnOk.Anchor = AnchorStyles.Top;
            btnOk.Click += (s, e) => dlg.Close();

            panelBottom.Controls.Add(btnOk);
            dlg.Controls.Add(pic);
            dlg.Controls.Add(panelBottom);
            dlg.Controls.Add(lbl);

            dlg.ShowDialog(this);
            pic.Image.Dispose();
            dlg.Dispose();
        }

        private void tmrStatus_Tick(object sender, EventArgs e)
        {
            ValidaConexao();
            UpdateActiveConnections();
        }

        private void btStop_Click(object sender, EventArgs e)
        {
            try
            {
                StopTunnelHard("TunnelX", 51820);
                MessageBox.Show("TunnelX fechado!", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                UpdateActiveConnections();
                ValidaConexao();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao parar túnel: " + ex.Message, "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateActiveConnections()
        {
            try
            {
                dtgConexoesAtivas.Rows.Clear();
                var status = TunnelManager.GetWgStatus();
                var root = System.IO.Path.Combine(TunnelManager.ConfDir, "clients");
                if (!Directory.Exists(root))
                    Directory.CreateDirectory(root);
                var dirs = Directory.GetDirectories(root);
                foreach (var dir in dirs)
                {
                    var json = System.IO.Path.Combine(dir, "client.json");
                    if (!File.Exists(json)) continue;
                    var txt = File.ReadAllText(json);
                    var nameLine = txt.Split('\n').FirstOrDefault(l => l.IndexOf("\"nome\"", StringComparison.OrdinalIgnoreCase) >= 0);
                    var pkLine = txt.Split('\n').FirstOrDefault(l => l.IndexOf("\"publicKey\"", StringComparison.OrdinalIgnoreCase) >= 0);
                    var addrLine = txt.Split('\n').FirstOrDefault(l => l.IndexOf("\"address\"", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (pkLine == null) continue;
                    var name = nameLine != null ? nameLine.Substring(nameLine.IndexOf(':') + 1).Trim().Trim('"', ',', ' ') : "—";
                    var pk = pkLine.Substring(pkLine.IndexOf(':') + 1).Trim().Trim('"', ',', ' ');
                    var addr = addrLine != null ? addrLine.Substring(addrLine.IndexOf(':') + 1).Trim().Trim('"', ',', ' ') : null;
                    var enabled = TunnelManager.PeerExists(pk);
                    var info = GetPeerStatus(status, pk);
                    var peerShort = pk.Length > 10 ? pk.Substring(0, 10) + "…" : pk;
                    var idx = dtgConexoesAtivas.Rows.Add();
                    var r = dtgConexoesAtivas.Rows[idx];
                    r.Cells["colClient"].Value = name;
                    r.Cells["colPeer"].Value = peerShort;
                    r.Cells["colEndpoint"].Value = info.endpoint ?? "—";
                    r.Cells["colHandshake"].Value = info.handshake ?? "—";
                    r.Cells["colRx"].Value = info.rx ?? "0";
                    r.Cells["colTx"].Value = info.tx ?? "0";
                    r.Tag = pk + "|" + (addr ?? "") + "|" + (enabled ? "1" : "0");
                }
                if (dtgConexoesAtivas.Rows.Count == 0)
                {
                    var idx = dtgConexoesAtivas.Rows.Add();
                    var r = dtgConexoesAtivas.Rows[idx];
                    r.Cells["colClient"].Value = "—";
                    r.Cells["colPeer"].Value = "—";
                    r.Cells["colEndpoint"].Value = "Sem conexões";
                    r.Cells["colHandshake"].Value = "—";
                    r.Cells["colRx"].Value = "0";
                    r.Cells["colTx"].Value = "0";
                }
                dtgConexoesAtivas.Visible = true;
            }
            catch
            {
                // evita travar UI caso wg.exe não esteja disponível
                dtgConexoesAtivas.Rows.Clear();
                dtgConexoesAtivas.Rows.Add("", "—", "—", "Sem conexões", "0", "0");
            }
        }

        private (string endpoint, string handshake, string rx, string tx) GetPeerStatus(string wgShowOutput, string publicKey)
        {
            if (string.IsNullOrWhiteSpace(wgShowOutput) || string.IsNullOrWhiteSpace(publicKey)) return (null, null, null, null);
            var lines = wgShowOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool match = false;
            string endpoint = null, handshake = null, rx = null, tx = null;
            foreach (var line in lines)
            {
                var s = line.Trim();
                if (s.StartsWith("peer:", StringComparison.OrdinalIgnoreCase))
                {
                    var val = s.Substring(5).Trim();
                    match = string.Equals(val, publicKey, StringComparison.OrdinalIgnoreCase);
                }
                else if (match && s.StartsWith("endpoint:", StringComparison.OrdinalIgnoreCase))
                {
                    endpoint = s.Substring(9).Trim();
                }
                else if (match && s.StartsWith("latest handshake:", StringComparison.OrdinalIgnoreCase))
                {
                    handshake = s.Substring(17).Trim();
                }
                else if (match && s.StartsWith("transfer:", StringComparison.OrdinalIgnoreCase))
                {
                    var val = s.Substring(9).Trim();
                    var parts = val.Split(',');
                    if (parts.Length >= 2)
                    {
                        rx = parts[0].Replace("received", "").Trim();
                        tx = parts[1].Replace("sent", "").Trim();
                    }
                }
            }
            return (endpoint, handshake, rx, tx);
        }

        private void dtgConexoesAtivas_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dtgConexoesAtivas.Columns[e.ColumnIndex].Name != "colActions") return;
            var row = dtgConexoesAtivas.Rows[e.RowIndex];
            var tag = row.Tag as string;
            if (string.IsNullOrEmpty(tag)) return;
            var parts = tag.Split('|');
            var pk = parts[0];
            var addr = parts.Length > 1 ? parts[1] : null;
            var enabled = parts.Length > 2 && parts[2] == "1";

            var menu = new ContextMenuStrip();
            if (enabled)
            {
                var disable = new ToolStripMenuItem("Desativar conexão");
                disable.Click += (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(pk))
                        TunnelManager.RemovePeer(pk);
                    TunnelManager.SetClientEnabledByPublicKey(pk, false);
                    TunnelManager.WriteServerConfFromClients();
                    if (!string.IsNullOrEmpty(addr))
                        TunnelManager.BlockClientInternet(addr);
                    UpdateActiveConnections();
                };
                menu.Items.Add(disable);
            }
            else
            {
                var enableItem = new ToolStripMenuItem("Ativar conexão");
                enableItem.Click += (s, ev) =>
                {
                    TunnelManager.SetClientEnabledByPublicKey(pk, true);
                    TunnelManager.WriteServerConfFromClients();
                    if (!string.IsNullOrEmpty(pk) && !string.IsNullOrEmpty(addr))
                        TunnelManager.AddPeer(pk, addr);
                    if (!string.IsNullOrEmpty(addr))
                        TunnelManager.UnblockClientInternet(addr);
                    UpdateActiveConnections();
                };
                menu.Items.Add(enableItem);
            }
            var cellRect = dtgConexoesAtivas.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            var point = dtgConexoesAtivas.PointToScreen(new System.Drawing.Point(cellRect.Left, cellRect.Bottom));
            menu.Show(point);
        }

        private struct PeerRow
        {
            public string Peer;
            public string PeerFull;
            public string Endpoint;
            public string Handshake;
            public string Rx;
            public string Tx;
        }

        private System.Collections.Generic.List<PeerRow> ParsePeers(string wgShowOutput)
        {
            var list = new System.Collections.Generic.List<PeerRow>();
            if (string.IsNullOrWhiteSpace(wgShowOutput)) return list;
            var lines = wgShowOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            PeerRow current = new PeerRow();
            bool hasPeer = false;
            foreach (var line in lines)
            {
                var s = line.Trim();
                if (s.StartsWith("peer:", StringComparison.OrdinalIgnoreCase))
                {
                    if (hasPeer) list.Add(current);
                    current = new PeerRow();
                    var val = s.Substring(5).Trim();
                    current.PeerFull = val;
                    current.Peer = val.Length > 10 ? val.Substring(0, 10) + "…" : val;
                    hasPeer = true;
                }
                else if (s.StartsWith("endpoint:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Endpoint = s.Substring(9).Trim();
                }
                else if (s.StartsWith("latest handshake:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Handshake = s.Substring(17).Trim();
                }
                else if (s.StartsWith("transfer:", StringComparison.OrdinalIgnoreCase))
                {
                    var val = s.Substring(9).Trim();
                    var parts = val.Split(',');
                    if (parts.Length >= 2)
                    {
                        current.Rx = parts[0].Replace("received", "").Trim();
                        current.Tx = parts[1].Replace("sent", "").Trim();
                    }
                }
            }
            if (hasPeer) list.Add(current);
            return list;
        }

        private string GetClientNameByPublicKey(string peerPublicKey)
        {
            try
            {
                var root = Path.Combine(TunnelManager.ConfDir, "clients");
                if (!Directory.Exists(root)) return null;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var json = Path.Combine(dir, "client.json");
                    if (!File.Exists(json)) continue;
                    var txt = File.ReadAllText(json);
                    if (txt.IndexOf(peerPublicKey ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var nameLine = txt.Split('\n').FirstOrDefault(l => l.Contains("\"nome\""));
                        if (nameLine != null)
                        {
                            var idx = nameLine.IndexOf(':');
                            if (idx > 0)
                            {
                                var val = nameLine.Substring(idx + 1).Trim().Trim('"', ',', ' ');
                                return string.IsNullOrWhiteSpace(val) ? null : val;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private string SanitizeFileName(string name)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var arr = name.ToCharArray();
            for (int i = 0; i < arr.Length; i++)
            {
                if (Array.IndexOf(invalid, arr[i]) >= 0) arr[i] = '_';
            }
            return new string(arr);
        }
        #endregion


    }
}
