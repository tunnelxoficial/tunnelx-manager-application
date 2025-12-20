using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;

namespace tunnelx.Services
{
    public static class WireGuardManager
    {
        private const int ICSSHARINGTYPE_PUBLIC = 0;
        private const int ICSSHARINGTYPE_PRIVATE = 1;

        // Métodos p/ REATIVAR TUNNEL SE JA CONFIGURADO

        public static void StartTunnelAndShare(string internetIfName)
        {
            string tunnelName = "TunnelX";
            string tunnelIfName = "TunnelX";
            int wgPortUdp = 51820;

            // 1) Encontra um arquivo de configuração válido
            string conf = FindWireGuardConfPath(tunnelName);

            if (conf == null)
                throw new FileNotFoundException($"Arquivo de configuração do WireGuard para '{tunnelName}' não encontrado.");

            // 2) Instala (ou reinstala) o serviço do túnel
            RunAdmin($@"wireguard /installtunnelservice ""{conf}""");

            // 3) Sobe o serviço (pode estar START_PENDING por alguns segundos)
            RunAdmin($@"sc start ""WireGuardTunnel${tunnelName}""");

            // 4) Aguarda a interface ficar "Connected" por até ~10s
            WaitForInterfaceUp(tunnelIfName, 10_000);

            // 5) (Recomendado) Aplica ICS: Wi-Fi (PUBLIC) → TunnelX (PRIVATE)
            EnableIcs(internetIfName, tunnelIfName);

            // 6) (Opcional) Abre porta UDP no firewall
            RunAdmin($@"netsh advfirewall firewall add rule name=""WireGuard UDP {wgPortUdp}"" dir=in action=allow protocol=UDP localport={wgPortUdp}");

            // 7) (Opcional) Força rota do /24 local do túnel (ajuste se usar outra sub-rede)
            RunAdmin($@"route add 10.66.66.0 mask 255.255.255.0 10.66.66.1 metric 1");

            Console.WriteLine("✅ TunnelX reativado e ICS aplicado.");
        }

        private static string FindWireGuardConfPath(string tunnelName)
        {
            // Candidatos comuns (WireGuard para Windows usa .conf.dpapi)
            var candidates = new[]
            {
            $@"C:\Program Files\WireGuard\Data\Configurations\{tunnelName}.conf.dpapi",
            $@"C:\Program Files\WireGuard\Data\Configurations\{tunnelName}.conf",
            // caso tenha exportado o .conf para outro lugar:
            $@"C:\Windows\System32\{tunnelName}.conf",
            $@"C:\{tunnelName}.conf"
        };
            return candidates.FirstOrDefault(File.Exists);
        }

        private static void WaitForInterfaceUp(string connectionId, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (IsInterfaceConnected(connectionId))
                    return;
                Thread.Sleep(500);
            }
            // segue mesmo se não detectou; WireGuard pode demorar mais um pouco
        }

        private static bool IsInterfaceConnected(string connectionId)
        {
            var q = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2");
            foreach (ManagementObject o in q.Get())
            {
                var name = o["NetConnectionID"]?.ToString();
                if (string.Equals(name, connectionId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void EnableIcs(string internetIfName, string tunnelIfName)
        {
            const int ICSSHARINGTYPE_PUBLIC = 0;
            const int ICSSHARINGTYPE_PRIVATE = 1;

            // Garante serviço ICS
            RunAdmin("sc start SharedAccess");

            var t = Type.GetTypeFromProgID("HNetCfg.HNetShare");
            dynamic mgr = Activator.CreateInstance(t);

            // Desativa qualquer ICS antigo (evita conflitos)
            foreach (dynamic conn in mgr.EnumEveryConnection)
            {
                dynamic cfg = mgr.INetSharingConfigurationForINetConnection[conn];
                try { if (cfg.SharingEnabled == true) cfg.DisableSharing(); } catch { }
            }

            dynamic connInternet = FindByName(mgr, internetIfName)
                ?? throw new Exception($"Interface de internet '{internetIfName}' não encontrada.");
            dynamic connTunnel = FindByName(mgr, tunnelIfName)
                ?? throw new Exception($"Interface do túnel '{tunnelIfName}' não encontrada.");

            dynamic cfgInternet = mgr.INetSharingConfigurationForINetConnection[connInternet];
            dynamic cfgTunnel = mgr.INetSharingConfigurationForINetConnection[connTunnel];

            try { cfgInternet.EnableSharing(ICSSHARINGTYPE_PUBLIC); } catch { }
            try { cfgTunnel.EnableSharing(ICSSHARINGTYPE_PRIVATE); } catch { }
        }

        private static dynamic FindByName(dynamic mgr, string name)
        {
            foreach (dynamic conn in mgr.EnumEveryConnection)
            {
                var props = mgr.NetConnectionProps[conn];
                if (string.Equals((string)props.Name, name, StringComparison.OrdinalIgnoreCase))
                    return conn;
            }
            return null;
        }



        // Métodos p/  COMPARTILHAR A INTERNET APOS TUNNEL CRIADO/REATIVADO
        public static void ApplyWifiToTunnelx(string internetIfName, string tunnelIfName = "TunnelX")
        {
            // Rode toda a sequência em STA (requisito do COM HNetCfg)
            var t = new Thread(() =>
            {
                try
                {
                    WaitForInterfaceUp(tunnelIfName, 10000);
                    EnsureServices();

                    dynamic mgr = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.HNetShare.1"));

                    // LIMPA ICS antigo (evita conflito)
                    DisableAllSharing(mgr);

                    // ATENÇÃO: sempre marque primeiro a pública (Wi-Fi) e depois a privada (TunnelX)
                    dynamic connInternet = FindByName(mgr, internetIfName)
                        ?? throw new Exception($"Interface de internet '{internetIfName}' não encontrada.");
                    dynamic connTunnel = FindByName(mgr, tunnelIfName)
                        ?? throw new Exception($"Interface do túnel '{tunnelIfName}' não encontrada.");

                    dynamic cfgInternet = mgr.INetSharingConfigurationForINetConnection[connInternet];
                    dynamic cfgTunnel = mgr.INetSharingConfigurationForINetConnection[connTunnel];

                    bool pubOk = Try(() => cfgInternet.EnableSharing(ICSSHARINGTYPE_PUBLIC), "EnableSharing(PUBLIC)", 3, 800);
                    bool privOk = Try(() => cfgTunnel.EnableSharing(ICSSHARINGTYPE_PRIVATE), "EnableSharing(PRIVATE)", 3, 800);

                    if (!pubOk || !privOk)
                    {
                        EnsureServices();
                        DisableAllSharing(mgr);

                        connInternet = FindByName(mgr, internetIfName)
                            ?? throw new Exception($"Interface de internet '{internetIfName}' não encontrada.");
                        connTunnel = FindByName(mgr, tunnelIfName)
                            ?? throw new Exception($"Interface do túnel '{tunnelIfName}' não encontrada.");

                        cfgInternet = mgr.INetSharingConfigurationForINetConnection[connInternet];
                        cfgTunnel = mgr.INetSharingConfigurationForINetConnection[connTunnel];

                        pubOk = Try(() => cfgInternet.EnableSharing(ICSSHARINGTYPE_PUBLIC), "EnableSharing(PUBLIC)", 3, 800);
                        privOk = Try(() => cfgTunnel.EnableSharing(ICSSHARINGTYPE_PRIVATE), "EnableSharing(PRIVATE)", 3, 800);

                        if (!pubOk || !privOk)
                        {
                            EnsureServices();
                            DisableAllSharing(mgr);

                            connInternet = FindByName(mgr, internetIfName)
                                ?? throw new Exception($"Interface de internet '{internetIfName}' não encontrada.");
                            connTunnel = FindByName(mgr, tunnelIfName)
                                ?? throw new Exception($"Interface do túnel '{tunnelIfName}' não encontrada.");

                            cfgInternet = mgr.INetSharingConfigurationForINetConnection[connInternet];
                            cfgTunnel = mgr.INetSharingConfigurationForINetConnection[connTunnel];

                            privOk = Try(() => cfgTunnel.EnableSharing(ICSSHARINGTYPE_PRIVATE), "EnableSharing(PRIVATE)", 3, 800);
                            pubOk = Try(() => cfgInternet.EnableSharing(ICSSHARINGTYPE_PUBLIC), "EnableSharing(PUBLIC)", 3, 800);
                        }
                    }

                    if (pubOk && privOk)
                        Console.WriteLine("✅ ICS aplicado: Wi-Fi (PUBLIC) → TunnelX (PRIVATE).");
                    else
                        Console.WriteLine("⚠️ ICS não foi aplicado após tentativas.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ Falha ao aplicar ICS: " + ex.Message);
                }
            });

            t.SetApartmentState(ApartmentState.STA); // ESSENCIAL
            t.Start();
            t.Join();
        }

        private static void EnsureServices()
        {
            // ICS usa o serviço SharedAccess; RRAS (RemoteAccess) conflita.
            RunAdmin("sc stop RemoteAccess >nul 2>&1");
            RunAdmin("sc stop SharedAccess >nul 2>&1");
            Thread.Sleep(500);
            RunAdmin("sc start EventSystem");
            RunAdmin("sc start Netman");
            RunAdmin("sc start SharedAccess");
            Thread.Sleep(500);
            RunAdmin("netsh wlan stop hostednetwork >nul 2>&1"); // inofensivo se não existir
        }

        private static void DisableAllSharing(dynamic mgr)
        {
            foreach (dynamic conn in mgr.EnumEveryConnection)
            {
                try
                {
                    dynamic cfg = mgr.INetSharingConfigurationForINetConnection[conn];
                    if (cfg.SharingEnabled == true) cfg.DisableSharing();
                }
                catch { /* ignora erros de placas "especiais" */ }
            }
        }

        private static bool Try(Action a, string label, int retries = 3, int delayMs = 800)
        {
            for (int i = 0; i <= retries; i++)
            {
                try { a(); return true; }
                catch (System.Runtime.InteropServices.COMException com) when ((uint)com.HResult == 0x80040201)
                {
                    if (i == retries) return false;
                    Thread.Sleep(delayMs);
                    continue;
                }
                catch
                {
                    if (i == retries) return false;
                    Thread.Sleep(delayMs);
                    continue;
                }
            }
            return false;
        }

        private static void RunAdmin(string cmd)
        {
            var psi = new ProcessStartInfo("cmd.exe", "/C " + cmd)
            {
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            Process.Start(psi)?.WaitForExit();
        }
    }
}
