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

            /// <summary>
            /// Plano, titular e prazo. Nulo para cliente sem correspondencia no
            /// banco — o que e o caso normal de quem foi gerado a mao pela tela.
            /// </summary>
            public Ficha Ficha;

            /// <summary>Pasta de onde esta linha foi lida. Necessaria para excluir.</summary>
            public string Pasta;

            /// <summary>Identidade da linha na grade. A chave publica e unica por peer.</summary>
            public string Key { get { return PublicKey ?? string.Empty; } }

            public string Plano { get { return Ficha == null ? null : Ficha.Plano; } }
            public string Dono { get { return Ficha == null ? null : Ficha.Dono; } }
            public bool Convidado { get { return Ficha != null && Ficha.Convidado; } }

            /// <summary>
            /// De quem e o acesso, na palavra que o produto ja usa.
            /// </summary>
            /// <remarks>
            /// "Titular" e "Convidado" sao os termos do aplicativo e do backend.
            /// Inventar um terceiro aqui faria o operador e o cliente falarem
            /// linguas diferentes sobre a mesma coisa.
            /// </remarks>
            public string Origem { get { return OrigemEm(DateTime.Now); } }

            /// <param name="agora">Para decidir se o prazo do convidado ja passou.</param>
            public string OrigemEm(DateTime agora)
            {
                if (Ficha == null) return "\u2014";
                if (!Ficha.Convidado) return "Titular";

                var quem = string.IsNullOrWhiteSpace(Ficha.Dono)
                    ? "Convidado"
                    : "Convidado de " + Ficha.Dono;

                // Dizer so "Convidado de Lucas" para quem perdeu o prazo afirmaria um
                // acesso que nao existe mais. Ver Ficha.Vencido.
                return Ficha.Vencido(agora) ? quem + " (prazo vencido)" : quem;
            }

            /// <summary>Convidado cujo prazo ja passou.</summary>
            public bool PrazoVencido(DateTime agora)
            {
                return Ficha != null && Ficha.Convidado && Ficha.Vencido(agora);
            }

            /// <summary>Quantos aparelhos dividem esta mesma conexao. Contado na hora.</summary>
            public int Ocupadas;

            /// <summary>CPF do cliente, quando a pasta o guarda. Serve a busca.</summary>
            public string Cpf;

            /// <summary>Texto de apoio para o tooltip: prazo e vagas.</summary>
            public string Detalhe(DateTime agora)
            {
                if (Ficha == null)
                    return "Cliente avulso: gerado pela tela, sem cadastro no sistema.";

                var partes = new List<string>();

                if (!string.IsNullOrWhiteSpace(Ficha.Plano)) partes.Add(Ficha.Plano);
                if (Ficha.Vagas > 0)
                    partes.Add(Ocupadas > 0
                        ? Ocupadas + " de " + Ficha.Vagas + " aparelhos"
                        : Ficha.Vagas + (Ficha.Vagas == 1 ? " vaga" : " vagas"));

                if (Ficha.Convidado)
                {
                    if (!string.IsNullOrWhiteSpace(Ficha.Dono)) partes.Add("titular: " + Ficha.Dono);
                    if (!string.IsNullOrWhiteSpace(Ficha.Prazo)) partes.Add("acesso por " + Ficha.Prazo);

                    // A contagem regressiva e calculada AGORA, e nunca lida do
                    // arquivo: gravada em disco, ela estaria errada no dia seguinte.
                    if (!string.IsNullOrWhiteSpace(Cpf)) partes.Add("CPF " + Cpf);

                    var falta = Ficha.Restante(agora);
                    if (falta.HasValue)
                    {
                        var d = falta.Value;
                        partes.Add(d.TotalSeconds <= 0
                            ? "PRAZO VENCIDO"
                            : d.TotalDays >= 1
                                ? "expira em " + (int)d.TotalDays + (((int)d.TotalDays) == 1 ? " dia" : " dias")
                                : "expira em " + (int)d.TotalHours + "h");
                    }
                }

                return string.Join("  \u00b7  ", partes);
            }
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

                // A saida do wg e fatiada UMA vez, num indice por chave publica.
                // Antes cada peer refazia o Split do texto inteiro: com N clientes,
                // N fatiamentos de um texto que tambem cresce com N, a cada segundo.
                // Era a mesma forma do custo que a chamada repetida ao wg.exe tinha.
                var porChave = IndexarPeers(status);

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

                    InfoPeer info;
                    if (!porChave.TryGetValue(pk, out info)) info = InfoPeer.Vazio;

                    snap.Peers.Add(new PeerRow
                    {
                        Name = Campo(txt, "nome") ?? "—",
                        PublicKey = pk,
                        Address = Campo(txt, "address"),
                        // Sem processo novo: a presenca sai do mesmo texto.
                        Enabled = info.Presente,
                        Endpoint = info.Endpoint ?? "—",
                        Handshake = info.Handshake ?? "—",
                        Rx = info.Rx ?? "0",
                        Tx = info.Tx ?? "0",
                        HandshakeSegundos = SegundosDoHandshake(info.Handshake),

                        // Arquivo irmao do client.json, gravado pelo provisionador.
                        // Ausente e caso normal, nao erro: Ler devolve null.
                        Ficha = Ficha.Ler(dir),
                        Pasta = dir,
                        // O caminho manual da tela grava o CPF no client.json; quem
                        // atende no telefone procura por ele antes do nome.
                        Cpf = Campo(txt, "cpf")
                    });
                }

                // Ordem estavel: sem isto a grade reordena sozinha conforme o
                // sistema de arquivos devolve os diretorios, e a linha que a pessoa
                // ia clicar pula de lugar.
                snap.Peers = snap.Peers.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                                       .ThenBy(p => p.PublicKey, StringComparer.Ordinal)
                                       .ToList();

                // Quantos aparelhos dividem cada tunel, contado aqui e nao gravado no
                // disco: e o unico dado que mudaria para TODOS os aparelhos de uma
                // conexao sempre que UM entrasse ou saisse — e cada mudanca seria uma
                // regravacao de arquivo debaixo de um leitor de 1 segundo.
                var porConexao = new Dictionary<int, int>();
                foreach (var p in snap.Peers)
                {
                    if (p.Ficha == null || p.Ficha.Conexao <= 0) continue;
                    int n;
                    porConexao[p.Ficha.Conexao] =
                        porConexao.TryGetValue(p.Ficha.Conexao, out n) ? n + 1 : 1;
                }

                foreach (var p in snap.Peers)
                {
                    int n;
                    if (p.Ficha != null && porConexao.TryGetValue(p.Ficha.Conexao, out n))
                        p.Ocupadas = n;
                }
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

        /// <summary>O que a saida do wg diz sobre um peer.</summary>
        public class InfoPeer
        {
            public static readonly InfoPeer Vazio = new InfoPeer();

            public bool Presente;
            public string Endpoint;
            public string Handshake;
            public string Rx;
            public string Tx;
        }

        /// <summary>
        /// Le a saida inteira do wg de uma vez, num indice por chave publica.
        /// </summary>
        /// <remarks>
        /// A versao anterior varria o texto inteiro uma vez POR PEER. O texto cresce
        /// com o numero de clientes e a varredura tambem, entao o custo era N vezes N,
        /// a cada segundo — a mesma forma do problema que motivou tirar a chamada
        /// repetida ao wg.exe, so que em memoria.
        ///
        /// A comparacao de chave e exata: o valor apos "peer:" E a chave, e casar por
        /// substring deixaria uma chave que e prefixo de outra roubar o endpoint alheio.
        /// </remarks>
        public static Dictionary<string, InfoPeer> IndexarPeers(string saidaWg)
        {
            var mapa = new Dictionary<string, InfoPeer>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(saidaWg)) return mapa;

            InfoPeer atual = null;

            foreach (var bruta in saidaWg.Split(new[] { Convert.ToChar(13), Convert.ToChar(10) },
                                                StringSplitOptions.RemoveEmptyEntries))
            {
                var linha = bruta.Trim();

                if (linha.StartsWith("peer:", StringComparison.OrdinalIgnoreCase))
                {
                    var chave = Depois(linha);
                    if (string.IsNullOrWhiteSpace(chave)) { atual = null; continue; }

                    atual = new InfoPeer { Presente = true };
                    mapa[chave] = atual;
                    continue;
                }

                if (atual == null) continue;   // linhas do bloco "interface:"

                if (linha.StartsWith("endpoint:", StringComparison.OrdinalIgnoreCase))
                    atual.Endpoint = Depois(linha);
                else if (linha.StartsWith("latest handshake:", StringComparison.OrdinalIgnoreCase))
                    atual.Handshake = Depois(linha);
                else if (linha.StartsWith("transfer:", StringComparison.OrdinalIgnoreCase))
                {
                    // "transfer: 105.74 MiB received, 240.09 MiB sent"
                    var partes = Depois(linha).Split(Convert.ToChar(44));
                    if (partes.Length == 2)
                    {
                        atual.Rx = partes[0].Replace("received", string.Empty).Trim();
                        atual.Tx = partes[1].Replace("sent", string.Empty).Trim();
                    }
                }
            }

            return mapa;
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
            var partes = texto.Split(new[] { Convert.ToChar(32), Convert.ToChar(44) },
                                     StringSplitOptions.RemoveEmptyEntries);

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
