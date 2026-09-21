using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using NetFwTypeLib; // adicionar referência COM: "Windows Firewall API" (ou via interop abaixo)
using QRCoder;
using Chaos.NaCl;


namespace tunnelx.Services
{
    public static class TunnelManager
    {
        // === AJUSTES PADRÃO DO TÚNEL ===
        public const string WireGuardInterfaceName = "TunnelX"; // nome do adaptador virtual
        public const string WgExePath = @"C:\Program Files\WireGuard\wireguard.exe"; // instalação padrão
        public const string WgShowExePath = @"C:\Program Files\WireGuard\wg.exe";    // opcional para status
        public const string ConfDir = @"C:\ProgramData\TunnelX";
        public const string ServerConfPath = @"C:\ProgramData\TunnelX\" + WireGuardInterfaceName + ".conf";
        public const int ListenPort = 51820;

        /// <summary>
        /// Faixa do tunel. Era /24 — 253 enderecos no servidor inteiro.
        ///
        /// O teto ja era baixo quando cada VENDA consumia um IP. Com um peer por
        /// APARELHO (a correcao das quedas, ver ConnectionDevice no backend) um
        /// plano medio de 8 pessoas passa a consumir 8 enderecos, e o mesmo /24
        /// limitaria o produto a ~31 assinaturas. Ampliar junto nao e opcional:
        /// sem isso a correcao trocaria uma falha de qualidade por uma falha de
        /// capacidade que estoura antes.
        ///
        /// O IP do servidor NAO muda — continua 10.66.66.1 — so a mascara. Os
        /// .conf ja distribuidos usam AllowedIPs 0.0.0.0/0 e nao referenciam o
        /// endereco do servidor, e os 10.66.66.x existentes seguem validos dentro
        /// de 10.66.0.0/16. A ampliacao e compativel com o que esta no ar.
        /// </summary>
        public const string SubnetCidr = "10.66.0.0/16";
        public const string ServerAddress = "10.66.66.1/16";
        public const string AndroidAddress = "10.66.66.2/32";
        public const int DefaultMtu = 1420;

        /// <summary>
        /// MTU gravado no .conf do cliente. Ajustavel pelo App.config (ClientMtu).
        /// </summary>
        /// <remarks>
        /// Era DefaultMtu - 40 = 1380, e isso quebra em rede movel.
        ///
        /// A conta: o WireGuard acrescenta 60 bytes sobre IPv4/UDP (20 de IP, 8 de
        /// UDP, 16 de cabecalho e 16 da tag Poly1305). Com MTU 1380 o datagrama que
        /// sai tem 1440 bytes. Em linha fixa isso cabe — PPPoE aceita 1492. Mas em
        /// 4G com CGNAT o MTU do caminho costuma ficar entre 1400 e 1430: o pacote
        /// e descartado em silencio, o aviso de "fragmentation needed" nao volta, e
        /// o TCP retransmite sem parar. O sintoma e exatamente vazao baixa com ping
        /// alto e latencia pior sob carga.
        ///
        /// 1280 e o piso seguro: e o MTU minimo que todo caminho IPv6 precisa
        /// aceitar, entao nenhuma rede o fragmenta. Custa 1,4% de eficiencia
        /// contra 1420 — irrelevante perto de uma retransmissao em laco.
        ///
        /// Depois de estabilizar, da para subir de 20 em 20 ate achar o teto do
        /// caminho, sem recompilar: basta mudar a chave no App.config.
        /// </remarks>
        public static int ClientMtu
        {
            get
            {
                int v;
                var bruto = System.Configuration.ConfigurationManager.AppSettings["ClientMtu"];
                if (int.TryParse(bruto, out v) && v >= 576 && v <= 1420) return v;
                return 1280;
            }
        }
        public static string ClientsDir => System.IO.Path.Combine(ConfDir, "clients");

        // === ENDPOINT PUBLICO / COMPARTILHAMENTO DE INTERNET ===
        // Host (IP ou DNS) que os clientes usam no campo Endpoint do .conf.
        // Ajustavel em App.config -> appSettings/VpnEndpointHost, sem recompilar.
        public static string PublicEndpointHost
        {
            get
            {
                var v = System.Configuration.ConfigurationManager.AppSettings["VpnEndpointHost"];
                return string.IsNullOrWhiteSpace(v) ? "tunnelx.ddns.net" : v.Trim();
            }
        }

        // Quando false, o compartilhamento de internet e feito fora do app
        // (WinNAT via New-NetNat, ou RRAS) e o ICS do Windows NAO e acionado.
        // O ICS reescreve a interface do tunel para 192.168.137.1/24, o que
        // conflita com ServerAddress (10.66.66.1/24) e derruba todos os peers.
        public static bool UseIcs
        {
            get
            {
                bool b;
                return bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["UseIcs"], out b) && b;
            }
        }

        // Host usado no Endpoint do .conf do cliente. Prioriza a configuracao e
        // so cai na deteccao automatica se ela estiver vazia. A deteccao por IPv6
        // vinha primeiro e gerava Endpoint = [ipv6]:51820, inacessivel para
        // clientes sem IPv6 -- por isso aqui o IPv4 e o fallback preferido.
        public static string ResolveClientEndpointHost()
        {
            var configured = PublicEndpointHost;
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var v4 = NetworkHelper.GetPublicIp();
            if (!string.IsNullOrWhiteSpace(v4) && v4 != "0.0.0.0")
                return v4;

            var v6 = NetworkHelper.GetPublicIpv6();
            return string.IsNullOrWhiteSpace(v6) ? "0.0.0.0" : "[" + v6 + "]";
        }

        // Defina o nome da interface REAL de Internet e a interface do túnel (WireGuard)
        public static string InternetInterfaceName = ""; // ou "Wi-Fi"

        // === CHAVES EM MEMÓRIA (poderia persistir em arquivo/secure storage) ===
        public static string ServerPrivateKeyB64;
        public static string ServerPublicKeyB64;
        public static string AndroidPrivateKeyB64;
        public static string AndroidPublicKeyB64;

        // === PASSO 0: Verificações ===
        public static void EnsureAdmin()
        {
            // o manifest já pede admin; aqui apenas um lembrete programático
            if (!new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                throw new SecurityException("Execute o aplicativo como Administrador.");
        }

        public static bool IsWireGuardInstalled() =>
            File.Exists(WgExePath);

        // (Opcional) Instalar WireGuard via API do Windows Installer (sem abrir msiexec)
        public static void InstallWireGuardFromMsi(string msiFullPath)
        {
            if (!File.Exists(msiFullPath)) throw new FileNotFoundException("MSI não encontrado.", msiFullPath);
            int res = MsiInstallProduct(msiFullPath, "ACTION=INSTALL ALLUSERS=1");
            if (res != 0) throw new InvalidOperationException($"Falha ao instalar WireGuard (MsiInstallProduct retornou {res}).");
        }

        [DllImport("msi.dll", CharSet = CharSet.Auto)]
        private static extern int MsiInstallProduct(string packagePath, string commandLine);

        public static class WireGuardKeyGenerator
        {
            /// <summary>
            /// Gera um par de chaves Curve25519 (X25519) compatível com WireGuard.
            /// </summary>
            // RNG criptografico unico e thread-safe. Substitui o new Random() anterior,
            // que era semeado por Environment.TickCount (~15 ms de resolucao): a chave
            // privada era reproduzivel por forca bruta do seed, e dois pares gerados em
            // sequencia imediata saiam IDENTICOS (servidor e peer Android).
            private static readonly System.Security.Cryptography.RandomNumberGenerator _rng =
                System.Security.Cryptography.RandomNumberGenerator.Create();

            public static (string PrivateKey, string PublicKey) GenerateKeyPair()
            {
                // 32 bytes de entropia criptografica para a chave privada
                byte[] privateKey = new byte[32];
                _rng.GetBytes(privateKey);

                // Clamping exigido pelo X25519 (mesmo tratamento que "wg genkey" aplica)
                privateKey[0] &= 248;
                privateKey[31] &= 127;
                privateKey[31] |= 64;

                // Deriva a chave pública correspondente
                byte[] publicKey = MontgomeryCurve25519.GetPublicKey(privateKey);

                string privateKeyB64 = Convert.ToBase64String(privateKey);
                string publicKeyB64 = Convert.ToBase64String(publicKey);

                return (privateKeyB64, publicKeyB64);
            }
        }


        // === PASSO 1: Gerar chaves (Curve25519) ===
        public static void GenerateKeys()
        {
            // Usando Sodium.Core – gera pares de chave para X25519 (curva do WireGuard)
            // Aqui usamos KeyPair de crypto_box (Curve25519), apenas como gerador de pares (a pública será usada no handshake)
            var serverKeyPair = WireGuardKeyGenerator.GenerateKeyPair();
            var androidKeyPair = WireGuardKeyGenerator.GenerateKeyPair();

            ServerPrivateKeyB64 = serverKeyPair.PrivateKey;
            ServerPublicKeyB64 = serverKeyPair.PublicKey;

            AndroidPrivateKeyB64 = androidKeyPair.PrivateKey;
            AndroidPublicKeyB64 = androidKeyPair.PublicKey;
        }

        // === PASSO 2: Criar wg0.conf (servidor/Windows) ===
        public static void WriteServerConf()
        {
            Directory.CreateDirectory(ConfDir);

            string conf = $@"
[Interface]
PrivateKey = {ServerPrivateKeyB64}
Address = {ServerAddress}
ListenPort = {ListenPort}
MTU = {DefaultMtu}

[Peer]
PublicKey = {AndroidPublicKeyB64}
AllowedIPs = {AndroidAddress}
".Trim() + Environment.NewLine;

            File.WriteAllText(ServerConfPath, conf);
        }

        // === PASSO 3: Abrir Firewall (COM INetFwPolicy2, sem netsh) ===
        public static void EnsureFirewallRuleUdp(int port, string ruleName = "WireGuard UDP 51820")
        {
            var policy2 = (INetFwPolicy2)Activator.CreateInstance(
                Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));

            // se já existir, apaga e recria (idempotente)
            foreach (INetFwRule r in policy2.Rules.Cast<INetFwRule>().ToList())
            {
                if (string.Equals(r.Name, ruleName, StringComparison.OrdinalIgnoreCase))
                    policy2.Rules.Remove(r.Name);
            }

            var rule = (INetFwRule)Activator.CreateInstance(
                Type.GetTypeFromProgID("HNetCfg.FWRule"));

            rule.Name = ruleName;
            rule.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP;
            rule.LocalPorts = port.ToString();
            rule.Direction = (NetFwTypeLib.NET_FW_RULE_DIRECTION_)NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
            rule.Action = (NetFwTypeLib.NET_FW_ACTION_)NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
            rule.Enabled = true;

            policy2.Rules.Add(rule);
        }

        // === PASSO 4: Ativar ICS (NAT) Internet -> WireGuard (COM HNetCfg.HNetShare) ===
        public static void EnableIcsSharing(string internetInterface, string localInterface)
        {
            if (!UseIcs)
            {
                Console.WriteLine("ICS desativado (App.config UseIcs=false). Compartilhamento via WinNAT/RRAS.");
                return;
            }
            IcsHelper.EnableSharing(internetInterface, localInterface);
        }

        // === PASSO 5: Subir o túnel (executa wireguard.exe diretamente, sem shell) ===
        public static void InstallAndStartTunnelService()
        {
            if (!File.Exists(WgExePath))
                throw new FileNotFoundException("WireGuard não instalado no caminho padrão.", WgExePath);

            var psi = new ProcessStartInfo
            {
                FileName = WgExePath,
                Arguments = $"/installtunnelservice \"{ServerConfPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                // se o túnel já está rodando, o WireGuard pode retornar código ≠0, mas a msg indica sucesso
                if (p.ExitCode != 0)
                {
                    string combined = (output + " " + error).ToLowerInvariant();

                    if (combined.Contains("already") && combined.Contains("running"))
                    {
                        // apenas loga e segue, não é falha
                        Console.WriteLine("Túnel já estava instalado e em execução.");
                    }
                    else
                    {
                        // erro real
                        throw new InvalidOperationException(
                            $"Falha ao instalar/iniciar o serviço do túnel WireGuard.\nSaída:\n{output}\nErro:\n{error}");
                    }
                }
            }
            SetInterfaceMtu(DefaultMtu);
            SetInterfaceMetric(5);
        }

        public static void UninstallTunnelService()
        {
            if (!File.Exists(WgExePath)) return;

            var psi = new ProcessStartInfo
            {
                FileName = WgExePath,
                Arguments = $"/uninstalltunnelservice \"{ServerConfPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                p.WaitForExit();
            }
        }

        // === PASSO 6: Gerar config do Android e QR ===
        public static string BuildAndroidPeerConf(string windowsPublicEndpointHostOrIp)
        {
            // Endpoint: IP/host público do Windows (roteador com port forward UDP ListenPort) ou IP público direto
            return $@"
[Interface]
PrivateKey = {AndroidPrivateKeyB64}
Address = {AndroidAddress}
DNS = 8.8.8.8
MTU = {ClientMtu}

[Peer]
PublicKey = {ServerPublicKeyB64}
Endpoint = {windowsPublicEndpointHostOrIp}:{ListenPort}
AllowedIPs = 0.0.0.0/0
PersistentKeepalive = 15
".Trim() + Environment.NewLine;
        }

        public static string BuildClientConf(string clientPrivateKeyB64, string clientAddressCidr, string windowsPublicEndpointHostOrIp)
        {
            return $@"
[Interface]
PrivateKey = {clientPrivateKeyB64}
Address = {clientAddressCidr}
DNS = 8.8.8.8
MTU = {ClientMtu}

[Peer]
PublicKey = {ServerPublicKeyB64}
Endpoint = {windowsPublicEndpointHostOrIp}:{ListenPort}
AllowedIPs = 0.0.0.0/0
PersistentKeepalive = 15
".Trim() + Environment.NewLine;
        }

        public static byte[] BuildAndroidPeerQrPng(string androidConfText, int pixelsPerModule = 8)
        {
            using (var gen = new QRCodeGenerator())
            {
                QRCodeData data = gen.CreateQrCode(androidConfText, QRCodeGenerator.ECCLevel.Q);

                using (var qr = new PngByteQRCode(data))
                {
                    return qr.GetGraphic(pixelsPerModule);
                }
            }
        }

        // === PASSO 7: Utilitários e status ===
        public static string[] ListNetworkInterfaceNames() =>
            System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Select(n => n.Name).ToArray();

        public static string GetWgStatus()
        {
            if (!File.Exists(WgShowExePath)) return "wg.exe não encontrado.";
            var psi = new ProcessStartInfo
            {
                FileName = WgShowExePath,
                Arguments = $"show {WireGuardInterfaceName}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using (var p = Process.Start(psi))
            {
                var txt = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return txt;
            }
        }
        public static void SetInterfaceMetric(int metric)
        {
            try
            {
                var psi4 = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C netsh interface ipv4 set interface \"{WireGuardInterfaceName}\" metric={metric}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi4)?.WaitForExit();
                var psi6 = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C netsh interface ipv6 set interface \"{WireGuardInterfaceName}\" metric={metric}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi6)?.WaitForExit();
            }
            catch { }
        }
        public static void SetInterfaceMtu(int mtu)
        {
            try
            {
                var psi4 = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C netsh interface ipv4 set subinterface \"{WireGuardInterfaceName}\" mtu={mtu} store=persistent",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi4)?.WaitForExit();
                var psi6 = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C netsh interface ipv6 set subinterface \"{WireGuardInterfaceName}\" mtu={mtu} store=persistent",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi6)?.WaitForExit();
            }
            catch { }
        }

        public static string AllocateClientAddress()
        {
            Directory.CreateDirectory(ClientsDir);
            var used = new System.Collections.Generic.HashSet<int>();
            foreach (var dir in Directory.GetDirectories(ClientsDir))
            {
                var json = System.IO.Path.Combine(dir, "client.json");
                if (!File.Exists(json)) continue;
                try
                {
                    var txt = File.ReadAllText(json);
                    var line = txt.Split('\n').FirstOrDefault(l => l.IndexOf("\"address\"", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (line != null)
                    {
                        var idx = line.IndexOf(':');
                        if (idx > 0)
                        {
                            var val = line.Substring(idx + 1).Trim().Trim('"', ',', ' ');
                            /*
                             * Guarda o endereco como (terceiro * 256 + quarto).
                             *
                             * Antes so o ultimo octeto era lido, porque o pool era um
                             * /24 e os tres primeiros eram fixos. Num /16 o terceiro
                             * octeto varia, e ignora-lo faria 10.66.1.5 e 10.66.2.5
                             * contarem como o MESMO endereco — duas pessoas com o mesmo
                             * IP de tunel, que e exatamente a colisao que esta correcao
                             * existe para eliminar.
                             */
                            var semMascara = val.Split('/')[0].Trim();
                            var partes = semMascara.Split('.');
                            if (partes.Length == 4
                                && int.TryParse(partes[2], out var terceiro)
                                && int.TryParse(partes[3], out var quarto))
                            {
                                used.Add(terceiro * 256 + quarto);
                            }
                        }
                    }
                }
                catch { }
            }
            // 10.66.66.1 e o servidor (ServerAddress) e 10.66.66.2 e o peer Android
            // fixo (AndroidAddress). Nenhum dos dois tem client.json, entao nao
            // aparecem na varredura acima -- sem isso o PRIMEIRO cliente recebia .2
            // e colidia. Os numeros sao os do terceiro+quarto octeto (66*256+1).
            used.Add(66 * 256 + 1);
            used.Add(66 * 256 + 2);

            /*
             * PRIMEIRO esgota 10.66.66.0/24, so depois abre o resto do /16.
             *
             * A ordem nao e estetica, e a diferenca entre funcionar hoje e exigir
             * uma parada. O WireGuard roteia por AllowedIPs, mas quem entrega o
             * pacote a interface e o SISTEMA, pela rota que a mascara da interface
             * cria. Enquanto o tunel em execucao estiver de pe como /24, um
             * 10.66.0.10 nao tem rota: o pacote sai pelo gateway padrao e o cliente
             * fica "conectado" sem trafego.
             *
             * A mascara nova (/16) so vale depois que o servico do tunel for
             * reinstalado — o que derruba todos os peers de uma vez. Mantendo o /24
             * como primeira escolha, a ampliacao entra sem parada: os ~250 primeiros
             * aparelhos continuam na faixa que ja funciona, e quando ela encher o
             * operador ja tera reiniciado o tunel em alguma manutencao.
             *
             * Os .0 e .255 de cada /24 sao pulados: nao ha broadcast num tunel
             * ponto-a-ponto, mas pilhas e firewalls domesticos tratam .255 de forma
             * especial, e um endereco que funciona em 99% das redes e pior que um
             * que funciona em todas — a falha apareceria como "so esse cliente cai".
             */
            for (int quarto = 2; quarto <= 254; quarto++)
            {
                if (!used.Contains(66 * 256 + quarto)) return $"10.66.66.{quarto}/32";
            }

            Log.Aviso("pool 10.66.66.0/24 esgotado; alocando no restante do /16. " +
                      "O tunel PRECISA estar rodando com a mascara /16 (ServerAddress) " +
                      "para estes enderecos terem rota — reinstale o servico do tunel.");

            for (int n = 2; n <= 65534; n++)
            {
                int terceiro = n / 256;
                int quarto = n % 256;
                if (terceiro == 66) continue;              // ja varrido acima
                if (quarto == 0 || quarto == 255) continue;
                if (used.Contains(n)) continue;
                return $"10.66.{terceiro}.{quarto}/32";
            }

            throw new InvalidOperationException("Sem enderecos disponiveis no pool " + SubnetCidr + ".");
        }

        public static void WriteServerConfFromClients()
        {
            EnsureServerKeys();
            Directory.CreateDirectory(ConfDir);
            Directory.CreateDirectory(ClientsDir);
            var peers = new System.Collections.Generic.List<(string pk, string ip)>();
            foreach (var dir in Directory.GetDirectories(ClientsDir))
            {
                var json = System.IO.Path.Combine(dir, "client.json");
                if (!File.Exists(json)) continue;
                try
                {
                    var txt = File.ReadAllText(json);
                    var pkLine = txt.Split('\n').FirstOrDefault(l => l.IndexOf("\"publicKey\"", StringComparison.OrdinalIgnoreCase) >= 0);
                    var ipLine = txt.Split('\n').FirstOrDefault(l => l.IndexOf("\"address\"", StringComparison.OrdinalIgnoreCase) >= 0);
                    var enabledLine = txt.Split('\n').FirstOrDefault(l => l.IndexOf("\"enabled\"", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (pkLine != null && ipLine != null)
                    {
                        var pkIdx = pkLine.IndexOf(':');
                        var ipIdx = ipLine.IndexOf(':');
                        var pk = pkLine.Substring(pkIdx + 1).Trim().Trim('"', ',', ' ');
                        var ip = ipLine.Substring(ipIdx + 1).Trim().Trim('"', ',', ' ');
                        bool enabled = true;
                        if (enabledLine != null)
                        {
                            var enIdx = enabledLine.IndexOf(':');
                            var enVal = enabledLine.Substring(enIdx + 1).Trim().Trim(',', ' ').ToLowerInvariant();
                            enabled = enVal == "true";
                        }
                        if (enabled && !string.IsNullOrWhiteSpace(pk) && !string.IsNullOrWhiteSpace(ip))
                            peers.Add((pk, ip));
                    }
                }
                catch { }
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Interface]");
            sb.AppendLine($"PrivateKey = {ServerPrivateKeyB64}");
            sb.AppendLine($"Address = {ServerAddress}");
            sb.AppendLine($"ListenPort = {ListenPort}");
            sb.AppendLine($"MTU = {DefaultMtu}");
            sb.AppendLine();
            foreach (var p in peers)
            {
                sb.AppendLine("[Peer]");
                sb.AppendLine($"PublicKey = {p.pk}");
                sb.AppendLine($"AllowedIPs = {p.ip}");
                sb.AppendLine();
            }
            File.WriteAllText(ServerConfPath, sb.ToString().Trim() + Environment.NewLine);
        }

        public static void EnsureServerKeys()
        {
            if (!string.IsNullOrWhiteSpace(ServerPrivateKeyB64)) return;
            try
            {
                if (File.Exists(ServerConfPath))
                {
                    var txt = File.ReadAllText(ServerConfPath);
                    foreach (var raw in txt.Split('\n'))
                    {
                        var line = raw.Trim();
                        if (line.StartsWith("PrivateKey", StringComparison.OrdinalIgnoreCase))
                        {
                            var idx = line.IndexOf('=');
                            if (idx > 0)
                            {
                                var val = line.Substring(idx + 1).Trim();
                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    ServerPrivateKeyB64 = val;
                                    try
                                    {
                                        var priv = Convert.FromBase64String(ServerPrivateKeyB64);
                                        var pub = MontgomeryCurve25519.GetPublicKey(priv);
                                        ServerPublicKeyB64 = Convert.ToBase64String(pub);
                                    }
                                    catch { }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            if (string.IsNullOrWhiteSpace(ServerPrivateKeyB64))
            {
                // Se ja existem clientes emitidos, gerar um par novo troca a identidade
                // do servidor e invalida todos eles de uma vez. Melhor falhar de forma
                // visivel do que destruir a base em silencio.
                bool haClientes = false;
                try
                {
                    haClientes = Directory.Exists(ClientsDir) &&
                                 Directory.GetDirectories(ClientsDir)
                                          .Any(d => File.Exists(System.IO.Path.Combine(d, "client.json")));
                }
                catch { }

                if (haClientes)
                    throw new InvalidOperationException(
                        "Chave privada do servidor nao encontrada em " + ServerConfPath +
                        ", mas existem clientes emitidos em " + ClientsDir + ". " +
                        "Gerar novas chaves invalidaria todos eles. " +
                        "Restaure o TunnelX.conf a partir de backup antes de continuar.");

                GenerateKeys();
            }
        }

        public static void SetClientEnabledByPublicKey(string publicKey, bool enabled)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(ClientsDir))
                {
                    var json = System.IO.Path.Combine(dir, "client.json");
                    if (!File.Exists(json)) continue;
                    var txt = File.ReadAllText(json);
                    var pkLine = txt.Split('\n').FirstOrDefault(l => l.IndexOf("\"publicKey\"", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (pkLine == null) continue;
                    var pkIdx = pkLine.IndexOf(':');
                    var pk = pkLine.Substring(pkIdx + 1).Trim().Trim('"', ',', ' ');
                    if (!string.Equals(pk, publicKey, StringComparison.OrdinalIgnoreCase)) continue;
                    var lines = txt.Split('\n').ToList();
                    int foundEnabled = -1;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].IndexOf("\"enabled\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foundEnabled = i; break;
                        }
                    }
                    var newLine = $"  \"enabled\": {(enabled ? "true" : "false")}";
                    if (foundEnabled >= 0)
                    {
                        lines[foundEnabled] = newLine;
                    }
                    else
                    {
                        if (lines.Count > 0 && lines[lines.Count - 1].Trim() == "}")
                            lines.Insert(lines.Count - 1, newLine + ",");
                        else
                            lines.Add(newLine);
                    }
                    File.WriteAllText(json, string.Join("\n", lines));
                    return;
                }
            }
            catch { }
        }

        public static void ReloadTunnel()
        {
            if (!File.Exists(WgShowExePath)) return;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = WgShowExePath,
                    Arguments = $"syncconf {WireGuardInterfaceName} \"{ServerConfPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit();
                    if (p.ExitCode == 0) return;
                }
            }
            catch { }
            try
            {
                UninstallTunnelService();
                InstallAndStartTunnelService();
            }
            catch { }
        }

        public static bool PeerExists(string publicKey)
        {
            var status = GetWgStatus() ?? string.Empty;
            return status.IndexOf("peer: " + publicKey, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Acrescenta o peer ao tunel em execucao.
        /// </summary>
        /// <returns>true somente se o peer estiver de fato no tunel depois.</returns>
        /// <remarks>
        /// Antes este metodo era void e engolia toda excecao com um catch vazio,
        /// sem nunca olhar o ExitCode. O chamador marcava a conexao como CREATED
        /// de qualquer jeito — entao o cliente aparecia pronto no painel, baixava
        /// o .conf, e simplesmente nao conectava. Sem log, sem erro, sem pista.
        ///
        /// A conferencia final e por PeerExists e nao pelo ExitCode: o wg.exe pode
        /// sair com 0 e ainda assim nao ter aplicado nada (interface errada, tunel
        /// parado). O que importa e o estado do tunel, nao o codigo de saida.
        /// </remarks>
        public static bool AddPeer(string publicKey, string allowedIps)
        {
            if (!File.Exists(WgShowExePath))
            {
                Log.Error($"AddPeer: wg.exe nao encontrado em {WgShowExePath}");
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = WgShowExePath,
                    Arguments = $"set {WireGuardInterfaceName} peer {publicKey} allowed-ips {allowedIps}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var p = Process.Start(psi))
                {
                    var saida = p.StandardOutput.ReadToEnd();
                    var erro = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    if (p.ExitCode != 0)
                    {
                        Log.Error($"AddPeer: wg saiu com {p.ExitCode}. stderr: {erro?.Trim()} stdout: {saida?.Trim()}");
                        return false;
                    }
                }

                if (!PeerExists(publicKey))
                {
                    Log.Error($"AddPeer: wg nao reclamou, mas o peer nao esta no tunel {WireGuardInterfaceName}");
                    return false;
                }

                Log.Info($"AddPeer: peer aplicado com allowed-ips {allowedIps}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("AddPeer: excecao ao chamar o wg", ex);
                return false;
            }
        }

        /// <summary>
        /// Remove o peer do tunel em execucao.
        /// </summary>
        /// <returns>true se o peer nao estiver mais no tunel.</returns>
        /// <remarks>
        /// Falhar aqui em silencio e pior que em AddPeer: o peer removido no banco
        /// continua valendo no tunel, e o cliente cortado segue navegando.
        /// </remarks>
        public static bool RemovePeer(string publicKey)
        {
            if (!File.Exists(WgShowExePath))
            {
                Log.Error($"RemovePeer: wg.exe nao encontrado em {WgShowExePath}");
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = WgShowExePath,
                    Arguments = $"set {WireGuardInterfaceName} peer {publicKey} remove",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var p = Process.Start(psi))
                {
                    var erro = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    if (p.ExitCode != 0)
                    {
                        Log.Error($"RemovePeer: wg saiu com {p.ExitCode}. stderr: {erro?.Trim()}");
                        return false;
                    }
                }

                if (PeerExists(publicKey))
                {
                    Log.Error("RemovePeer: o peer CONTINUA no tunel apos a remocao");
                    return false;
                }

                Log.Info("RemovePeer: peer removido do tunel");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("RemovePeer: excecao ao chamar o wg", ex);
                return false;
            }
        }

        public static void BlockClientInternet(string addressCidr)
        {
            try
            {
                var ip = addressCidr;
                var slash = ip.IndexOf('/');
                if (slash > 0) ip = ip.Substring(0, slash);
                var ruleNameOut = $"TunnelX BLOCK OUT {ip}";
                var ruleNameIn = $"TunnelX BLOCK IN {ip}";
                var ifaceGuid = GetWireGuardInterfaceGuid();
                if (string.IsNullOrWhiteSpace(ifaceGuid)) return;
                if (!ifaceGuid.StartsWith("{")) ifaceGuid = "{" + ifaceGuid + "}";
                if (!ifaceGuid.EndsWith("}")) ifaceGuid = ifaceGuid + "}";
                var policy2 = (INetFwPolicy2)Activator.CreateInstance(
                    Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                foreach (INetFwRule r in policy2.Rules.Cast<INetFwRule>().ToList())
                {
                    if (string.Equals(r.Name, ruleNameOut, StringComparison.OrdinalIgnoreCase))
                    {
                        r.Enabled = true;
                    }
                    if (string.Equals(r.Name, ruleNameIn, StringComparison.OrdinalIgnoreCase))
                    {
                        r.Enabled = true;
                    }
                }
                var rule = (INetFwRule)Activator.CreateInstance(
                    Type.GetTypeFromProgID("HNetCfg.FWRule"));
                rule.Name = ruleNameOut;
                rule.Direction = (NetFwTypeLib.NET_FW_RULE_DIRECTION_)NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT;
                rule.Action = (NetFwTypeLib.NET_FW_ACTION_)NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                rule.Enabled = true;
                rule.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY;
                rule.RemoteAddresses = ip;
                rule.Interfaces = new object[] { ifaceGuid };
                policy2.Rules.Add(rule);
                var rule2 = (INetFwRule)Activator.CreateInstance(
                    Type.GetTypeFromProgID("HNetCfg.FWRule"));
                rule2.Name = ruleNameIn;
                rule2.Direction = (NetFwTypeLib.NET_FW_RULE_DIRECTION_)NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN;
                rule2.Action = (NetFwTypeLib.NET_FW_ACTION_)NET_FW_ACTION_.NET_FW_ACTION_BLOCK;
                rule2.Enabled = true;
                rule2.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY;
                rule2.RemoteAddresses = ip;
                rule2.Interfaces = new object[] { ifaceGuid };
                policy2.Rules.Add(rule2);
            }
            catch { }
        }

        public static void UnblockClientInternet(string addressCidr)
        {
            try
            {
                var ip = addressCidr;
                var slash = ip.IndexOf('/');
                if (slash > 0) ip = ip.Substring(0, slash);
                var ruleNameOut = $"TunnelX BLOCK OUT {ip}";
                var ruleNameIn = $"TunnelX BLOCK IN {ip}";
                var policy2 = (INetFwPolicy2)Activator.CreateInstance(
                    Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                foreach (INetFwRule r in policy2.Rules.Cast<INetFwRule>().ToList())
                {
                    if (string.Equals(r.Name, ruleNameOut, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.Name, ruleNameIn, StringComparison.OrdinalIgnoreCase))
                    {
                        policy2.Rules.Remove(r.Name);
                    }
                }
            }
            catch { }
        }

        public static string GetWireGuardInterfaceGuid()
        {
            try
            {
                var ni = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => string.Equals(n.Name, WireGuardInterfaceName, StringComparison.OrdinalIgnoreCase));
                return ni?.Id;
            }
            catch { return null; }
        }
    }

    // --- Enums da API de Firewall (se preferir, pode usar interop via referência COM) ---
    public enum NET_FW_PROFILE_TYPE2_
    {
        NET_FW_PROFILE2_DOMAIN = 1,
        NET_FW_PROFILE2_PRIVATE = 2,
        NET_FW_PROFILE2_PUBLIC = 4,
        NET_FW_PROFILE2_ALL = 2147483647
    }
    public enum NET_FW_IP_PROTOCOL_
    {
        NET_FW_IP_PROTOCOL_TCP = 6,
        NET_FW_IP_PROTOCOL_UDP = 17,
        NET_FW_IP_PROTOCOL_ANY = 256
    }
    public enum NET_FW_RULE_DIRECTION_
    {
        NET_FW_RULE_DIR_IN = 1,
        NET_FW_RULE_DIR_OUT = 2
    }
    public enum NET_FW_ACTION_
    {
        NET_FW_ACTION_BLOCK = 0,
        NET_FW_ACTION_ALLOW = 1
    }
}
