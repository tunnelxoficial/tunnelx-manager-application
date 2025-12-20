using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace tunnelx.Services
{
    public static class EndpointHelper
    {
        public static string BuildEndpoint(string hostOrIp, int port)
        {
            if (string.IsNullOrWhiteSpace(hostOrIp))
                throw new ArgumentException("Host/IP inválido.", nameof(hostOrIp));

            // Se já vier entre colchetes, não duplica
            if (hostOrIp.StartsWith("[") && hostOrIp.EndsWith("]"))
                return $"{hostOrIp}:{port}";

            // Tenta interpretar como IP literal
            IPAddress ip;
            if (IPAddress.TryParse(hostOrIp, out ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                    return $"[{hostOrIp}]:{port}"; // IPv6 precisa de colchetes
                else
                    return $"{hostOrIp}:{port}";   // IPv4 normal
            }

            // Se não é IP (é hostname/DDNS), retorna como está (WireGuard resolve)
            return $"{hostOrIp}:{port}";
        }
    }

}
