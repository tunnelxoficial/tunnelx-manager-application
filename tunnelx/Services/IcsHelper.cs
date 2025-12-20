using System;
using System.Collections;
using System.ServiceProcess;

namespace tunnelx.Services
{
    public static class IcsHelper
    {
        /// <summary>
        /// Ativa ICS/NAT da interface com Internet (public) para a interface local do túnel (private).
        /// Ex.: EnableSharing("Wi-Fi", "wg0") ou EnableSharing("Wi-Fi", "WireGuard")
        /// </summary>
        public static bool EnableSharing(string internetInterface, string localInterface, int startIcsTimeoutMs = 8000)
        {
            // 1) Garante o serviço ICS (SharedAccess) iniciado
            try
            {
                using (var sc = new ServiceController("SharedAccess"))
                {
                    if (sc.Status != ServiceControllerStatus.Running &&
                        sc.Status != ServiceControllerStatus.StartPending)
                    {
                        sc.Start();
                    }
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(startIcsTimeoutMs));
                }
            }
            catch (Exception ex)
            {
                // Se não conseguir iniciar o ICS, não adianta seguir
                System.Diagnostics.Debug.WriteLine("ICS start failed: " + ex);
                return false;
            }

            // 2) Usa COM HNetCfg para configurar o compartilhamento
            dynamic mgr = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.HNetShare.1"));
            if (mgr == null) return false;

            // (a) Desliga compartilhamento em todas para evitar estados antigos
            foreach (var c in (IEnumerable)mgr.EnumEveryConnection)
            {
                dynamic cfg = null;
                try
                {
                    cfg = mgr.INetSharingConfigurationForINetConnection[c];
                    if (cfg != null && cfg.SharingEnabled) cfg.DisableSharing();
                }
                catch { /* ignora */ }
            }

            bool publicOk = false, privateOk = false;

            // (b) Religa: internet = público(0), túnel = privado(1)
            foreach (var c in (IEnumerable)mgr.EnumEveryConnection)
            {
                dynamic props = null;
                try { props = mgr.NetConnectionProps[c]; } catch { continue; }
                if (props == null) continue;

                string name = (props.Name ?? "").ToString();

                try
                {
                    var cfg = mgr.INetSharingConfigurationForINetConnection[c];
                    if (cfg == null) continue;

                    if (StringEquals(name, internetInterface))
                    {
                        cfg.EnableSharing(0);        // público = fornece Internet
                        publicOk = true;
                    }
                    else if (StringEquals(name, localInterface))
                    {
                        cfg.EnableSharing(1);        // privado = rede interna (WireGuard)
                        privateOk = true;
                    }
                }
                catch { /* ignora, tenta prosseguir */ }
            }

            // 3) Verificação básica: ambas devem estar compartilhadas
            bool verified = false;
            try
            {
                bool pubEnabled = false, privEnabled = false;

                foreach (var c in (IEnumerable)mgr.EnumEveryConnection)
                {
                    dynamic props = null;
                    try { props = mgr.NetConnectionProps[c]; } catch { continue; }
                    if (props == null) continue;

                    string name = (props.Name ?? "").ToString();
                    dynamic cfg = null;
                    try { cfg = mgr.INetSharingConfigurationForINetConnection[c]; } catch { continue; }
                    if (cfg == null) continue;

                    if (StringEquals(name, internetInterface) && cfg.SharingEnabled) pubEnabled = true;
                    if (StringEquals(name, localInterface) && cfg.SharingEnabled) privEnabled = true;
                }

                verified = pubEnabled && privEnabled;
            }
            catch { /* ignore */ }

            return publicOk && privateOk && verified;
        }

        private static bool StringEquals(string a, string b)
            => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
