using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace tunnelx.Services
{
    /// <summary>
    /// Mantem a grade de conexoes ativas em dia com um <see cref="ConnectionSnapshot"/>.
    /// </summary>
    /// <remarks>
    /// Vive fora do formulario para poder ser exercitada sem subir a tela inteira
    /// (o construtor do formulario liga o servico que fala com o banco).
    /// </remarks>
    public static class GradeConexoes
    {
        /// <summary>Marca a linha de aviso "sem conexoes", que nao representa peer algum.</summary>
        public const string LinhaVazia = "\u0000sem-conexoes";

        /// <summary>
        /// Ajusta a grade ao snapshot mexendo so no que mudou.
        /// </summary>
        /// <remarks>
        /// Antes era Rows.Clear() e reconstrucao completa a cada segundo: piscava,
        /// perdia a linha selecionada e a posicao da rolagem, e fechava o menu de
        /// contexto na cara de quem ia clicar em "Desativar conexao".
        /// </remarks>
        /// <param name="filtro">Texto da busca. Vazio mostra tudo.</param>
        /// <returns>Quantas linhas ficaram visiveis.</returns>
        public static int Reconciliar(DataGridView grade, ConnectionSnapshot snap, string filtro = null)
        {
            if (grade == null || snap == null) return 0;

            var peers = Filtrar(snap.Peers, filtro);
            var buscando = !string.IsNullOrWhiteSpace(filtro);

            grade.SuspendLayout();
            try
            {
                var porChave = new Dictionary<string, DataGridViewRow>(StringComparer.Ordinal);
                foreach (DataGridViewRow linha in grade.Rows)
                {
                    if (linha.IsNewRow) continue;   // a linha fantasma de "adicionar"

                    var c = ChaveDaLinha(linha);
                    if (c != null && !porChave.ContainsKey(c)) porChave[c] = linha;
                }

                var vistas = new HashSet<string>(StringComparer.Ordinal);

                foreach (var peer in peers)
                {
                    vistas.Add(peer.Key);

                    DataGridViewRow linha;
                    if (!porChave.TryGetValue(peer.Key, out linha))
                    {
                        linha = grade.Rows[grade.Rows.Add()];
                        porChave[peer.Key] = linha;
                    }

                    // A Tag carrega o peer inteiro: o menu de contexto le chave e
                    // endereco, e a pintura le o estado e a idade do handshake.
                    linha.Tag = peer;

                    // O IP do tunel no lugar do pedaco da chave publica: sete
                    // caracteres de uma chave de 44 nao identificam ninguem, e o
                    // endereco e o que se usa para achar a maquina na rede. A chave
                    // inteira continua no tooltip da celula.
                    var ip = (peer.Address ?? "\u2014").Split('/')[0];

                    Definir(linha, "colClient", peer.Name);
                    Definir(linha, "colPlano", peer.Plano ?? "—");
                    Definir(linha, "colOrigem", peer.Origem);
                    Definir(linha, "colPeer", ip);
                    Definir(linha, "colEndpoint", peer.Endpoint);
                    // Curto na celula, completo no tooltip: a coluna e estreita e
                    // "7 minutes, 12 seconds ago" nao se le de relance.
                    Definir(linha, "colHandshake", peer.HandshakeCurto);
                    Dica(linha, "colHandshake", peer.Handshake);
                    Dica(linha, "colPeer", peer.PublicKey);   // a chave inteira

                    // Prazo, vagas, titular e CPF vao no texto de apoio: respondem a
                    // segunda pergunta do operador e nao cabem na largura da coluna.
                    var detalhe = peer.Detalhe(DateTime.Now);
                    Dica(linha, "colPlano", detalhe);
                    Dica(linha, "colOrigem", detalhe);
                    Dica(linha, "colClient",
                        (peer.Address ?? "") + (peer.Enabled ? "" : "  (fora do t\u00fanel)"));
                    Definir(linha, "colRx", peer.Rx);
                    Definir(linha, "colTx", peer.Tx);
                }

                // Sem nenhuma linha visivel, mantem (ou cria) a linha de aviso em
                // vez de recria-la a cada ciclo — recriar faz a grade piscar.
                //
                // Os dois motivos de ficar vazia sao diferentes e precisam de
                // textos diferentes: "nao ha ninguem conectado" e um fato sobre o
                // servidor; "a busca nao achou" e um fato sobre o que voce digitou.
                // Mostrar o primeiro no lugar do segundo faria o operador achar que
                // perdeu os clientes.
                if (peers.Count == 0)
                {
                    vistas.Add(LinhaVazia);

                    DataGridViewRow aviso;
                    if (!porChave.TryGetValue(LinhaVazia, out aviso))
                    {
                        aviso = grade.Rows[grade.Rows.Add()];
                        aviso.Tag = new ConnectionSnapshot.PeerRow { PublicKey = LinhaVazia };
                    }
                    {
                        Definir(aviso, "colClient", "—");
                        Definir(aviso, "colPlano", "—");
                        Definir(aviso, "colPeer", "—");
                        Definir(aviso, "colEndpoint", "—");
                        Definir(aviso, "colOrigem", buscando
                            ? "Nenhuma conexão corresponde à busca"
                            : "Sem conexões");
                        Definir(aviso, "colHandshake", "—");
                        Definir(aviso, "colRx", "0");
                        Definir(aviso, "colTx", "0");
                    }
                }

                // Sobras: peers que sumiram, e a linha de aviso quando ja ha peers.
                for (var k = grade.Rows.Count - 1; k >= 0; k--)
                {
                    // A linha fantasma de "adicionar" nao pode ser removida: tentar
                    // derruba a atualizacao inteira com InvalidOperationException.
                    if (grade.Rows[k].IsNewRow) continue;

                    var c = ChaveDaLinha(grade.Rows[k]);
                    if (c == null || !vistas.Contains(c)) grade.Rows.RemoveAt(k);
                }
            }
            finally
            {
                grade.ResumeLayout();
            }

            return peers.Count;
        }

        /// <summary>
        /// Aplica a busca sobre nome, plano, titular, IP e endereco de origem.
        /// </summary>
        /// <remarks>
        /// A comparacao ignora caixa E acento. Sem tirar o acento, procurar por
        /// "conceicao" nao acha "Conceicao" — e quem digita rapido nao poe acento.
        /// Varias palavras funcionam como E: "lucas basico" acha o convidado do
        /// Lucas no Plano Basico, em qualquer ordem.
        /// </remarks>
        public static List<ConnectionSnapshot.PeerRow> Filtrar(
            List<ConnectionSnapshot.PeerRow> peers, string filtro)
        {
            if (peers == null) return new List<ConnectionSnapshot.PeerRow>();
            if (string.IsNullOrWhiteSpace(filtro)) return peers;

            var termos = Simplificar(filtro)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (termos.Length == 0) return peers;

            return peers.Where(p =>
            {
                var alvo = Simplificar(string.Join(" ", new[]
                {
                    p.Name, p.Plano, p.Origem, p.Address, p.Endpoint, p.Dono, p.Cpf
                }));

                return termos.All(t => alvo.IndexOf(t, StringComparison.Ordinal) >= 0);
            }).ToList();
        }

        /// <summary>Minusculas e sem acento, para comparar como quem digita pensa.</summary>
        /// <remarks>
        /// FormD separa a letra do acento em dois caracteres; descartar os da
        /// categoria NonSpacingMark deixa so a letra. ToLowerInvariant, e nao
        /// ToLower, porque a cultura corrente nao deve mudar o resultado de uma
        /// busca — em turco, ToLower("I") devolve outra letra.
        /// </remarks>
        public static string Simplificar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;

            var decomposto = texto.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposto.Length);

            foreach (var c in decomposto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        /// <summary>O peer da linha, ou null quando ela nao representa nenhum.</summary>
        public static ConnectionSnapshot.PeerRow PeerDaLinha(DataGridViewRow linha)
        {
            var peer = linha == null ? null : linha.Tag as ConnectionSnapshot.PeerRow;
            return peer == null || peer.PublicKey == LinhaVazia ? null : peer;
        }

        /// <summary>Identidade da linha: a chave publica do peer, ou o marcador de vazio.</summary>
        public static string ChaveDaLinha(DataGridViewRow linha)
        {
            var peer = linha == null ? null : linha.Tag as ConnectionSnapshot.PeerRow;
            return peer == null || string.IsNullOrEmpty(peer.PublicKey) ? null : peer.PublicKey;
        }

        /// <summary>Escreve a celula so quando o valor mudou.</summary>
        /// <remarks>
        /// Atribuir Value dispara invalidacao mesmo com o mesmo texto. Numa grade
        /// que atualiza de segundo em segundo, isso e repintura continua a toa.
        ///
        /// Coluna ausente e ignorada em silencio. Cells["nome"] LANCA quando a
        /// coluna nao existe, e esta funcao roda dentro do laco de atualizacao: uma
        /// excecao aqui nao deixa uma celula vazia, ela derruba a atualizacao
        /// inteira e a grade congela — exatamente o defeito que este caminho foi
        /// reescrito para tirar. Uma coluna a menos deve custar uma celula, nao a
        /// tela.
        /// </remarks>
        private static void Definir(DataGridViewRow linha, string coluna, string valor)
        {
            var celula = Celula(linha, coluna);
            if (celula == null) return;

            if (!string.Equals(celula.Value as string, valor, StringComparison.Ordinal))
                celula.Value = valor;
        }

        /// <summary>Escreve o texto de apoio, se a coluna existir.</summary>
        private static void Dica(DataGridViewRow linha, string coluna, string texto)
        {
            var celula = Celula(linha, coluna);
            if (celula == null) return;

            if (!string.Equals(celula.ToolTipText, texto, StringComparison.Ordinal))
                celula.ToolTipText = texto ?? string.Empty;
        }

        private static DataGridViewCell Celula(DataGridViewRow linha, string coluna)
        {
            if (linha == null || linha.DataGridView == null) return null;

            var col = linha.DataGridView.Columns[coluna];
            return col == null ? null : linha.Cells[col.Index];
        }
    }
}
