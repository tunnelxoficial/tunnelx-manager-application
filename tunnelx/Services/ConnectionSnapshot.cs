using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace tunnelx.Services
{
    /// <summary>
    /// Uma leitura completa do estado das conexoes, montada FORA da thread da UI.
    /// </summary>
    /// <remarks>
    /// Existe por causa de um travamento concreto. A tela atualizava assim, a cada
    /// segundo, direto no Tick do timer:
    ///
    ///   GetWgStatus()                  -> 1 processo wg.exe
    ///   para CADA peer: PeerExists()   -> mais 1 processo wg.exe, por peer
    ///
    /// Com 12 clientes eram 13 processos por segundo, todos bloqueando a thread da
    /// interface — e o custo cresce linear com a base. A partir de ~30 peers o app
    /// passa mais tempo parado do que respondendo, que e exatamente o "travando o
    /// sistema" relatado.
    ///
    /// Aqui o wg.exe e chamado UMA vez por atualizacao, e a existencia de cada peer
    /// sai do texto que ja esta em maos. A UI so recebe o resultado pronto.
    /// </remarks>
    public class ConnectionSnapshot
    {
        public class PeerRow
        {
            public string Name;
            public string PublicKey;
            public string Address;
            public bool Enabled;
            public string Endpoint;
            public string Handshake;
            public string Rx;
            public string Tx;

            /// <summary>Idade do ultimo handshake em segundos; -1 se nunca houve.</summary>
            public int HandshakeSegundos = -1;

            /// <summary>
            /// Idade do handshake em forma curta: "14s", "7min", "2h", "3d".
            /// </summary>
            /// <remarks>
            /// O texto do wg ("7 minutes, 12 seconds ago") nao cabe na coluna e
            /// nao se le de relance. O texto completo continua no tooltip.
            /// </remarks>
            public string HandshakeCurto
            {
                get
                {
                    var s = HandshakeSegundos;
                    if (s < 0) return "\u2014";
                    if (s < 5) return "agora";
                    if (s < 60) return s + "s";
                    if (s < 3600) return (s / 60) + "min";
                    if (s < 86400) return (s / 3600) + "h";
                    return (s / 86400) + "d";
                }
            }

            /// <summary>
            /// Conectado de fato, e nao apenas cadastrado.
            /// </summary>
            /// <remarks>
            /// O WireGuard renova o handshake a cada ~2 minutos enquanto ha trafego,
            /// e o keepalive de 15s mantem o aparelho ocioso batendo ponto. Passados
            /// 3 minutos sem handshake, aquele aparelho nao esta do outro lado.
            /// </remarks>
            public bool Online
            {
                get { return Enabled && HandshakeSegundos >= 0 && HandshakeSegundos <= 180; }
            }

            /// <summary>Identidade da linha na grade. A chave publica e unica por peer.</summary>
            public string Key { get { return PublicKey ?? string.Empty; } }
        }

        public List<PeerRow> Peers = new List<PeerRow>();
        public bool TunnelUp;
        public string Erro;

        /// <summary>
        /// Le tudo. BLOQUEANTE — chamar de uma Task, nunca da thread da UI.
        /// </summary>
        public static ConnectionSnapshot Coletar()
        {
            // UMA chamada ao wg.exe. Era uma por peer, mais uma.
            var status = TunnelManager.GetWgStatus() ?? string.Empty;
            return Montar(status, Path.Combine(TunnelManager.ConfDir, "clients"));
        }

        /// <summary>
        /// Monta o snapshot a partir da saida do wg e de um diretorio de clientes.
        /// Nao chama processo nenhum — e por aqui que o comportamento e testado.
        /// </summary>
        public static ConnectionSnapshot Montar(string status, string raiz)
        {
            var snap = new ConnectionSnapshot();
            status = status ?? string.Empty;

            try
            {
                snap.TunnelUp = status.IndexOf("interface:", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!Directory.Exists(raiz))
                {
                    Directory.CreateDirectory(raiz);
                    return snap;
                }

                foreach (var dir in Directory.GetDirectories(raiz))
                {
                    var json = Path.Combine(dir, "client.json");
                    if (!File.Exists(json)) continue;

                    string txt;
                    try { txt = File.ReadAllText(json); }
                    catch { continue; } // arquivo sendo reescrito pelo provisionador

                    var pk = Campo(txt, "publicKey");
                    if (string.IsNullOrWhiteSpace(pk)) continue;

                    var info = LerPeer(status, pk);

                    snap.Peers.Add(new PeerRow
                    {
                        Name = Campo(txt, "nome") ?? "—",
                        PublicKey = pk,
                        Address = Campo(txt, "address"),
                        // Sem processo novo: a presenca sai do mesmo texto.
                        Enabled = info.presente,
                        Endpoint = info.endpoint ?? "—",
                        Handshake = info.handshake ?? "—",
                        Rx = info.rx ?? "0",
                        Tx = info.tx ?? "0",
                        HandshakeSegundos = SegundosDoHandshake(info.handshake)
                    });
                }

                // Ordem estavel: sem isto a grade reordena sozinha conforme o
                // sistema de arquivos devolve os diretorios, e a linha que a pessoa
                // ia clicar pula de lugar.
                snap.Peers = snap.Peers.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                                       .ThenBy(p => p.PublicKey, StringComparer.Ordinal)
                                       .ToList();
            }
            catch (Exception ex)
            {
                snap.Erro = ex.Message;
            }

            return snap;
        }

        /// <summary>Le um campo do client.json sem trazer um parser de JSON junto.</summary>
        private static string Campo(string json, string chave)
        {
            var linha = json.Split('\n')
                .FirstOrDefault(l => l.IndexOf("\"" + chave + "\"", StringComparison.OrdinalIgnoreCase) >= 0);
            if (linha == null) return null;

            var i = linha.IndexOf(':');
            if (i < 0) return null;

            return linha.Substring(i + 1).Trim().Trim('"', ',', ' ');
        }

        /// <summary>Extrai endpoint, handshake e trafego de um peer da saida do wg.</summary>
        private static (bool presente, string endpoint, string handshake, string rx, string tx) LerPeer(
            string saidaWg, string publicKey)
        {
            if (string.IsNullOrWhiteSpace(saidaWg) || string.IsNullOrWhiteSpace(publicKey))
                return (false, null, null, null, null);

            var linhas = saidaWg.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool dentro = false, achou = false;
            string endpoint = null, handshake = null, rx = null, tx = null;

            foreach (var bruta in linhas)
            {
                var s = bruta.Trim();

                if (s.StartsWith("peer:", StringComparison.OrdinalIgnoreCase))
                {
                    // Chegou no proximo peer: o que interessava ja foi lido.
                    if (dentro) break;
                    dentro = string.Equals(Depois(s), publicKey, StringComparison.OrdinalIgnoreCase);
                    if (dentro) achou = true;
                    continue;
                }

                if (!dentro) continue;

                if (s.StartsWith("endpoint:", StringComparison.OrdinalIgnoreCase))
                    endpoint = Depois(s);
                else if (s.StartsWith("latest handshake:", StringComparison.OrdinalIgnoreCase))
                    handshake = Depois(s);
                else if (s.StartsWith("transfer:", StringComparison.OrdinalIgnoreCase))
                {
                    // "transfer: 105.74 MiB received, 240.09 MiB sent"
                    var v = Depois(s);
                    var partes = v.Split(',');
                    if (partes.Length == 2)
                    {
                        rx = partes[0].Replace("received", string.Empty).Trim();
                        tx = partes[1].Replace("sent", string.Empty).Trim();
                    }
                }
            }

            return (achou, endpoint, handshake, rx, tx);
        }

        /// <summary>
        /// Converte o handshake do wg ("12 seconds ago", "1 minute, 4 seconds ago")
        /// em segundos. Devolve -1 quando nunca houve handshake.
        /// </summary>
        public static int SegundosDoHandshake(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return -1;

            var total = 0;
            var achou = false;

            // Le pares "<numero> <unidade>", ignorando virgulas e o "ago" final.
            var partes = texto.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i + 1 < partes.Length; i++)
            {
                int n;
                if (!int.TryParse(partes[i], out n)) continue;

                var unidade = partes[i + 1].ToLowerInvariant();
                if (unidade.StartsWith("second")) { total += n; achou = true; }
                else if (unidade.StartsWith("minute")) { total += n * 60; achou = true; }
                else if (unidade.StartsWith("hour")) { total += n * 3600; achou = true; }
                else if (unidade.StartsWith("day")) { total += n * 86400; achou = true; }
            }

            return achou ? total : -1;
        }

        private static string Depois(string linha)
        {
            var i = linha.IndexOf(':');
            return i < 0 ? linha : linha.Substring(i + 1).Trim();
        }
    }
}
