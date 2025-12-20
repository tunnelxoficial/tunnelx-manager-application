using System.IO;
using System.Net;

namespace tunnelx.Services
{
    public static class NetworkHelper
    {
        public static string GetPublicIp()
        {
            string[] endpoints =
            {
            "https://api.ipify.org",
            "https://ifconfig.me/ip",
            "https://icanhazip.com",
            "https://ident.me"
        };

            foreach (var url in endpoints)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Timeout = 4000; // 4 segundos
                    request.UserAgent = "Mozilla/5.0"; // alguns sites exigem user-agent

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        string ip = reader.ReadToEnd().Trim();
                        if (IPAddress.TryParse(ip, out _))
                            return ip;
                    }
                }
                catch
                {
                    // tenta o próximo serviço
                }
            }

            return "0.0.0.0";
        }

        public static string GetPublicIpv6()
        {
            // Endpoints que retornam apenas IPv6 (texto puro)
            string[] urls =
            {
            "https://api64.ipify.org",
            "https://ifconfig.co/ip",
            "https://ident.me"
        };

            foreach (var url in urls)
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Timeout = 4000;
                    req.UserAgent = "Mozilla/5.0";

                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream()))
                    {
                        var text = sr.ReadToEnd().Trim();
                        IPAddress ip;
                        if (IPAddress.TryParse(text, out ip) &&
                            ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        {
                            return text;
                        }
                    }
                }
                catch { /* tenta próximo */ }
            }

            return null; // sem IPv6 público detectado
        }

    }

}
