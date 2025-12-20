using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Net.NetworkInformation;
using tunnelx.Services;

namespace tunnelx
{
    public class frmSelectInterface : Form
    {
        private FlowLayoutPanel cardsPanel;
        private Button btnOk;
        private Button btnCancel;
        public string SelectedInterfaceName { get; private set; }
        public bool TunnelCreated { get; private set; }
        private System.Collections.Generic.Dictionary<CheckBox, string> map;

        public frmSelectInterface()
        {
            Text = "Selecionar Interface";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(720, 600);
            BackColor = Color.FromArgb(248, 249, 251);
            map = new System.Collections.Generic.Dictionary<CheckBox, string>();
            MinimumSize = new Size(720, 560);

            var panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.White
            };
            var lblTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Selecionar interface para criar túnel",
                Font = new Font(new FontFamily("Segoe UI"), 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 16, 0)
            };
            panelTop.Controls.Add(lblTitle);

            cardsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMargin = new Size(0, 32),
                Padding = new Padding(16, 40, 16, 32),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = Color.White,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(16, 14, 16, 14)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };

            btnOk = new Button
            {
                Text = "Criar",
                Width = 120,
                Height = 36,
                BackColor = Color.RoyalBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Margin = new Padding(12, 0, 0, 0);
            btnOk.Click += (s, e) =>
            {
                var selected = map.FirstOrDefault(kv => kv.Key.Checked).Value;
                if (string.IsNullOrWhiteSpace(selected))
                {
                    MessageBox.Show("Selecione uma interface.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                SelectedInterfaceName = selected;
                try
                {
                    TunnelManager.InternetInterfaceName = SelectedInterfaceName;
                    WireGuardManager.StartTunnelAndShare(SelectedInterfaceName);
                    TunnelManager.WriteServerConfFromClients();
                    TunnelManager.ReloadTunnel();

                    string endpointPublic = NetworkHelper.GetPublicIpv6();
                    string androidConf = "";
                    if (!string.IsNullOrEmpty(endpointPublic))
                        androidConf = TunnelManager.BuildAndroidPeerConf("[" + endpointPublic + "]");
                    else
                    {
                        endpointPublic = NetworkHelper.GetPublicIp();
                        androidConf = TunnelManager.BuildAndroidPeerConf(endpointPublic);
                    }
                    File.WriteAllText(Path.Combine(TunnelManager.ConfDir, "android-peer.conf"), androidConf);
                    var png = TunnelManager.BuildAndroidPeerQrPng(androidConf);
                    File.WriteAllBytes(Path.Combine(TunnelManager.ConfDir, "android-peer.png"), png);
                }
                catch
                {
                    TunnelManager.EnsureAdmin();
                    if (!TunnelManager.IsWireGuardInstalled())
                    {
                        var msi = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wireguard-amd64.msi");
                        TunnelManager.InstallWireGuardFromMsi(msi);
                    }
                    TunnelManager.WriteServerConfFromClients();
                    TunnelManager.EnsureFirewallRuleUdp(TunnelManager.ListenPort);
                    TunnelManager.EnableIcsSharing(
                        internetInterface: SelectedInterfaceName,
                        localInterface: TunnelManager.WireGuardInterfaceName);
                    TunnelManager.InstallAndStartTunnelService();
                    TunnelManager.ReloadTunnel();

                    string endpointPublic = NetworkHelper.GetPublicIpv6();
                    string androidConf = TunnelManager.BuildAndroidPeerConf("[" + endpointPublic + "]");
                    File.WriteAllText(Path.Combine(TunnelManager.ConfDir, "android-peer.conf"), androidConf);
                    var png = TunnelManager.BuildAndroidPeerQrPng(androidConf);
                    File.WriteAllBytes(Path.Combine(TunnelManager.ConfDir, "android-peer.png"), png);
                }

                WireGuardManager.ApplyWifiToTunnelx(SelectedInterfaceName, "TunnelX");
                TunnelCreated = true;
                MessageBox.Show("TunnelX aberto com sucesso!", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            };

            btnCancel = new Button
            {
                Text = "Cancelar",
                Width = 100,
                Height = 36,
                BackColor = Color.Gainsboro,
                ForeColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Margin = new Padding(0);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnOk);
            footer.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
            footer.Controls.Add(buttons, 1, 0);
            Controls.Add(panelTop);
            Controls.Add(footer);
            Controls.Add(cardsPanel);

            Load += (s, e) => LoadInterfaces();
            Resize += (s, e) => ResizeCards();
        }

        private void LoadInterfaces()
        {
            cardsPanel.Controls.Clear();
            map.Clear();
            cardsPanel.Controls.Add(new Panel { Height = 40, Width = 1 });
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!nic.Name.Equals("TunnelX", StringComparison.OrdinalIgnoreCase))
                {
                    var card = BuildCard(nic);
                    cardsPanel.Controls.Add(card);
                }
            }
            cardsPanel.Controls.Add(new Panel { Height = 96, Width = 1 });
            ResizeCards();
        }

        private Panel BuildCard(NetworkInterface nic)
        {
            var card = new Panel
            {
                Width = 360,
                Height = 120,
                BackColor = Color.White,
                Margin = new Padding(0, 8, 0, 8)
            };
            card.Padding = new Padding(12);
            card.BorderStyle = BorderStyle.FixedSingle;

            var chk = new CheckBox
            {
                Text = nic.Name,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(12, 12)
            };
            chk.CheckedChanged += (s, e) =>
            {
                if (chk.Checked)
                {
                    foreach (var kv in map.Keys)
                        if (!ReferenceEquals(kv, chk))
                            kv.Checked = false;
                    SelectedInterfaceName = map[chk];
                }
                else
                {
                    if (SelectedInterfaceName == map[chk])
                        SelectedInterfaceName = null;
                }
            };

            var lblDesc = new Label
            {
                Text = nic.Description,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = false,
                Location = new Point(12, 40),
                Size = new Size(320, 20)
            };

            var statusText = nic.OperationalStatus.ToString();
            var lblStatus = new Label
            {
                Text = "Status: " + statusText,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = nic.OperationalStatus == OperationalStatus.Up ? Color.ForestGreen : Color.DarkRed,
                AutoSize = false,
                Location = new Point(12, 64),
                Size = new Size(200, 20)
            };

            card.Controls.Add(chk);
            card.Controls.Add(lblDesc);
            card.Controls.Add(lblStatus);
            map[chk] = nic.Name;
            return card;
        }

        private void ResizeCards()
        {
            var scrollbar = cardsPanel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
            var inner = cardsPanel.ClientSize.Width - 32 - scrollbar;
            foreach (Control c in cardsPanel.Controls)
                c.Width = inner > 360 ? inner : 360;
        }
    }
}
