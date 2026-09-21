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

        // Estado da atualizacao da grade de conexoes.
        //
        // A leitura (wg.exe + os client.json) roda FORA da thread da interface.
        // Antes o Tick fazia tudo aqui, a cada segundo: uma chamada ao wg.exe para
        // o panorama e MAIS UMA POR PEER, via PeerExists, so para saber se o peer
        // existia, dado que ja estava no panorama. Com 12 clientes eram 13
        // processos por segundo bloqueando a tela; com 30, o app passa mais tempo
        // parado do que respondendo. Era esse o travamento.
        private int _coletando;                 // 0/1 por Interlocked: uma coleta de cada vez

        // O ultimo estado lido, guardado para o filtro poder ser aplicado no ato.
        // Sem isto, digitar na busca so teria efeito no proximo ciclo — ate um
        // segundo de atraso a cada tecla, que se sente como travamento.
        private ConnectionSnapshot _ultimoSnapshot;


        #region ... MÉTODOS ...

        public frmDefault()
        {
            InitializeComponent();
            
            // Inicia o serviço de background para consulta ao banco de dados
            var bgService = new Services.DbBackgroundService();
            bgService.ConnectionCreated += BgService_ConnectionCreated;
            bgService.Start();
        }

        private void BgService_ConnectionCreated(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateActiveConnections()));
            }
            else
            {
                UpdateActiveConnections();
            }
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

        // Detecta o tunel pelo adaptador de rede e, como reforco, pelo servico do
        // WireGuard. A consulta WMI anterior (Win32_NetworkAdapter com
        // NetConnectionStatus = 2) nao enxerga adaptadores WinTun: retornava
        // sempre false e mantinha btStop/btGerarQr/btGerarConfig invisiveis,
        // tornando impossivel criar cliente pela tela.
        private bool IsTunnelActive(string tunnelName = "TunnelX")
        {
            try
            {
                var nic = System.Net.NetworkInformation.NetworkInterface
                    .GetAllNetworkInterfaces()
                    .FirstOrDefault(n => string.Equals(n.Name, tunnelName, StringComparison.OrdinalIgnoreCase));

                if (nic != null && nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    return true;
            }
            catch { }

            try
            {
                using (var svc = new System.ServiceProcess.ServiceController("WireGuardTunnel" + "$" + tunnelName))
                    return svc.Status == System.ServiceProcess.ServiceControllerStatus.Running;
            }
            catch
            {
                return false;
            }
        }

        private void ValidaConexao()
        {
            ValidaConexao(IsTunnelActive());
        }

        /// <param name="ativo">Estado do tunel ja apurado: a consulta e cara.</param>
        private void ValidaConexao(bool ativo)
        {
            if (ativo)
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
                if (TunnelManager.UseIcs && !string.IsNullOrWhiteSpace(TunnelManager.InternetInterfaceName))
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

            // Depois que os controles existem e antes de preencher as grades:
            // o tema ajusta estilos que a montagem das linhas vai herdar.
            Services.Tema.Aplicar(this);

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

                // Recupera a chave do servidor a partir do TunnelX.conf existente.
                // NUNCA chamar GenerateKeys()+WriteServerConf() aqui: trocaria a
                // identidade do servidor e o WriteServerConf reescreve o arquivo com
                // apenas o peer Android fixo, apagando TODOS os clientes ja emitidos.
                TunnelManager.EnsureServerKeys();

                var clientKeyPair = tunnelx.Services.TunnelManager.WireGuardKeyGenerator.GenerateKeyPair();
                var address = TunnelManager.AllocateClientAddress();

                string endpointPublic = TunnelManager.ResolveClientEndpointHost();
                string conf = TunnelManager.BuildClientConf(clientKeyPair.PrivateKey, address, endpointPublic);

                var path = Path.Combine(clientDir, "client-peer.conf");
                File.WriteAllText(path, conf);
                var meta = "{\n" +
                           $"  \"nome\": \"{Services.Json.Escapar(form.Nome)}\",\n" +
                           $"  \"celular\": \"{Services.Json.Escapar(form.Celular)}\",\n" +
                           $"  \"email\": \"{Services.Json.Escapar(form.Email)}\",\n" +
                           $"  \"cpf\": \"{Services.Json.Escapar(form.Cpf)}\",\n" +
                           $"  \"publicKey\": \"{Services.Json.Escapar(clientKeyPair.PublicKey)}\",\n" +
                           $"  \"privateKey\": \"{Services.Json.Escapar(clientKeyPair.PrivateKey)}\",\n" +
                           $"  \"address\": \"{Services.Json.Escapar(address)}\",\n" +
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

                    // Ver comentario equivalente em btGerarConfig: regenerar as chaves
                    // aqui invalidaria todos os clientes ja emitidos.
                    TunnelManager.EnsureServerKeys();

                    var clientKeyPair = tunnelx.Services.TunnelManager.WireGuardKeyGenerator.GenerateKeyPair();
                    var address = TunnelManager.AllocateClientAddress();

                    string endpointPublic = TunnelManager.ResolveClientEndpointHost();
                    string conf = TunnelManager.BuildClientConf(clientKeyPair.PrivateKey, address, endpointPublic);

                    var png = TunnelManager.BuildAndroidPeerQrPng(conf);
                    var pathPng = Path.Combine(clientDir, "client-peer.png");
                    File.WriteAllBytes(pathPng, png);
                    var meta = "{\n" +
                               $"  \"nome\": \"{Services.Json.Escapar(form.Nome)}\",\n" +
                               $"  \"celular\": \"{Services.Json.Escapar(form.Celular)}\",\n" +
                               $"  \"email\": \"{Services.Json.Escapar(form.Email)}\",\n" +
                               $"  \"cpf\": \"{Services.Json.Escapar(form.Cpf)}\",\n" +
                               $"  \"publicKey\": \"{Services.Json.Escapar(clientKeyPair.PublicKey)}\",\n" +
                               $"  \"privateKey\": \"{Services.Json.Escapar(clientKeyPair.PrivateKey)}\",\n" +
                               $"  \"address\": \"{Services.Json.Escapar(address)}\",\n" +
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
            SolicitarAtualizacao();
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

        /// <summary>
        /// Pede uma atualizacao da grade. Volta na hora; a grade muda quando a
        /// leitura terminar.
        /// </summary>
        private void UpdateActiveConnections()
        {
            SolicitarAtualizacao();
        }

        /// <summary>
        /// Dispara a leitura do estado em segundo plano.
        /// </summary>
        /// <remarks>
        /// Se a leitura anterior ainda nao acabou, esta chamada e descartada. Sem
        /// isso um ciclo lento (wg.exe demorando, disco ocupado) acumularia Ticks
        /// e a tela ficaria sempre atrasada em relacao ao que esta pedindo.
        /// </remarks>
        private void SolicitarAtualizacao()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _coletando, 1, 0) != 0)
                return;

            System.Threading.Tasks.Task.Run(() =>
            {
                ConnectionSnapshot snap = null;
                bool ativo = false;

                try
                {
                    snap = ConnectionSnapshot.Coletar();
                    ativo = IsTunnelActive();   // nao toca em controle: pode rodar aqui
                }
                catch { }

                try
                {
                    if (snap != null && IsHandleCreated && !IsDisposed)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            // O contador so zera depois de aplicar: assim nunca ha duas
                            // atualizacoes disputando a grade.
                            try { AplicarSnapshot(snap, ativo); }
                            finally { System.Threading.Volatile.Write(ref _coletando, 0); }
                        }));
                        return;
                    }
                }
                catch (ObjectDisposedException) { }      // a tela fechou durante a leitura
                catch (InvalidOperationException) { }

                System.Threading.Volatile.Write(ref _coletando, 0);
            });
        }

        /// <summary>
        /// Redesenha a grade a partir do ultimo estado lido, aplicando a busca.
        /// </summary>
        /// <remarks>
        /// Separado de AplicarSnapshot porque tem dois gatilhos com ritmos bem
        /// diferentes: o ciclo de um segundo e cada tecla digitada na busca.
        /// </remarks>
        private void DesenharGrade()
        {
            if (_ultimoSnapshot == null) return;

            var filtro = txtBusca == null ? null : txtBusca.Text;
            var mostradas = Services.GradeConexoes.Reconciliar(dtgConexoesAtivas, _ultimoSnapshot, filtro);
            var total = _ultimoSnapshot.Peers.Count;

            if (lblResultado != null)
            {
                lblResultado.Text = string.IsNullOrWhiteSpace(filtro)
                    ? (total == 1 ? "1 conexao" : total + " conexoes")
                    : mostradas + " de " + total;
            }

            dtgConexoesAtivas.Visible = true;
        }

        private void txtBusca_TextChanged(object sender, EventArgs e)
        {
            DesenharGrade();
        }

        private void txtBusca_KeyDown(object sender, KeyEventArgs e)
        {
            // Esc limpa a busca: e o gesto que todo mundo tenta primeiro.
            if (e.KeyCode != Keys.Escape) return;

            txtBusca.Clear();
            e.SuppressKeyPress = true;
        }

        private void AplicarSnapshot(ConnectionSnapshot snap, bool tunelAtivo)
        {
            if (IsDisposed) return;

            _ultimoSnapshot = snap;

            ValidaConexao(tunelAtivo);
            DesenharGrade();

            // Os selos contam o estado INTEIRO do servidor, nunca o que sobrou do
            // filtro: eles respondem "como esta o tunel", e essa resposta nao pode
            // mudar porque alguem digitou um nome na busca.
            Services.Tema.AtualizarResumo(groupBox3, snap);
        }


        private void dtgConexoesAtivas_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dtgConexoesAtivas.Columns[e.ColumnIndex].Name != "colActions") return;
            var row = dtgConexoesAtivas.Rows[e.RowIndex];

            // Null na linha de aviso "sem conexoes": nao ha o que ativar ali.
            var peer = Services.GradeConexoes.PeerDaLinha(row);
            if (peer == null) return;

            var pk = peer.PublicKey;
            var addr = peer.Address;
            var enabled = peer.Enabled;

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
            menu.Items.Add(new ToolStripSeparator());

            var excluir = new ToolStripMenuItem("Excluir conexão");
            excluir.Click += (s2, ev) => ExcluirConexao(peer);
            menu.Items.Add(excluir);

            var cellRect = dtgConexoesAtivas.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            var point = dtgConexoesAtivas.PointToScreen(new System.Drawing.Point(cellRect.Left, cellRect.Bottom));
            menu.Show(point);
        }

        /// <summary>
        /// Exclui a conexao de vez: do banco, do tunel e do disco.
        /// </summary>
        /// <remarks>
        /// Pede confirmacao porque nao ha desfazer: a chave privada do cliente
        /// morre junto com a pasta, entao nem reprovisionar devolve o mesmo acesso
        /// — a pessoa precisa receber um .conf novo.
        /// </remarks>
        private void ExcluirConexao(Services.ConnectionSnapshot.PeerRow peer)
        {
            if (peer == null) return;

            var doSistema = !string.IsNullOrEmpty(peer.Pasta) &&
                            System.IO.Path.GetFileName(peer.Pasta)
                                  .StartsWith("dev_", StringComparison.OrdinalIgnoreCase);

            var aviso =
                "Excluir a conexão de " + peer.Name + "?\n\n" +
                "• o peer sai do túnel e o acesso para na hora\n" +
                "• a pasta do cliente e a chave dele são apagadas\n" +
                (doSistema
                    ? "• o acesso é revogado no sistema, e o aplicativo do cliente\n" +
                      "  deixa de mostrar este túnel\n"
                    : "• este cliente foi gerado pela tela e não tem cadastro;\n" +
                      "  nada muda no painel\n") +
                "\nNão há como desfazer.";

            var resposta = MessageBox.Show(aviso, "Excluir conexão",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (resposta != DialogResult.Yes) return;

            Cursor = Cursors.WaitCursor;
            Services.Exclusao.Resultado r;
            try { r = Services.Exclusao.Excluir(peer); }
            finally { Cursor = Cursors.Default; }

            MessageBox.Show(r.Mensagem, r.Excluiu ? "Pronto" : "Não foi possível",
                MessageBoxButtons.OK,
                r.Excluiu ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            UpdateActiveConnections();
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
