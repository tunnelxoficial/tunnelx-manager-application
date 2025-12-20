using System;
using System.Collections;
using System.Management;

namespace tunnelx.Services
{
    public class InternetConnectionSharing
    {
        public static void EnableSharing(string internetInterfaceName, string localInterfaceName)
        {
            Type managerType = Type.GetTypeFromProgID("HNetCfg.HNetShare.1");
            dynamic manager = Activator.CreateInstance(managerType);

            // Percorre todas as conexões de rede
            foreach (var conn in (IEnumerable)manager.EnumEveryConnection)
            {
                dynamic props = manager.NetConnectionProps[conn];
                string name = props.Name;

                if (string.Equals(name, internetInterfaceName, StringComparison.OrdinalIgnoreCase))
                {
                    dynamic config = manager.INetSharingConfigurationForINetConnection[conn];
                    config.EnableSharing(0); // 0 = compartilhamento público (Internet)
                }
                else if (string.Equals(name, localInterfaceName, StringComparison.OrdinalIgnoreCase))
                {
                    dynamic config = manager.INetSharingConfigurationForINetConnection[conn];
                    config.EnableSharing(1); // 1 = compartilhamento privado (rede interna)
                }
            }
        }
    }

}
