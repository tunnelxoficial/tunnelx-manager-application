using System;
using System.Collections.Generic;
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
        public static void Reconciliar(DataGridView grade, ConnectionSnapshot snap)
        {
            if (grade == null || snap == null) return;

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

                foreach (var peer in snap.Peers)
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
                    Definir(linha, "colPeer", ip);
                    Definir(linha, "colEndpoint", peer.Endpoint);
                    // Curto na celula, completo no tooltip: a coluna e estreita e
                    // "7 minutes, 12 seconds ago" nao se le de relance.
                    Definir(linha, "colHandshake", peer.HandshakeCurto);
                    linha.Cells["colHandshake"].ToolTipText = peer.Handshake ?? string.Empty;
                    linha.Cells["colPeer"].ToolTipText = peer.PublicKey;   // a chave inteira
                    linha.Cells["colClient"].ToolTipText =
                        (peer.Address ?? "") + (peer.Enabled ? "" : "  (fora do t\u00fanel)");
                    Definir(linha, "colRx", peer.Rx);
                    Definir(linha, "colTx", peer.Tx);
                }

                // Sem nenhum peer, mantem (ou cria) a linha de aviso em vez de
                // recria-la a cada ciclo — recriar faz a grade piscar sozinha.
                if (snap.Peers.Count == 0)
                {
                    vistas.Add(LinhaVazia);
                    if (!porChave.ContainsKey(LinhaVazia))
                    {
                        var aviso = grade.Rows[grade.Rows.Add()];
                        aviso.Tag = new ConnectionSnapshot.PeerRow { PublicKey = LinhaVazia };
                        Definir(aviso, "colClient", "—");
                        Definir(aviso, "colPeer", "—");
                        Definir(aviso, "colEndpoint", "Sem conexões");
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
        /// </remarks>
        private static void Definir(DataGridViewRow linha, string coluna, string valor)
        {
            var celula = linha.Cells[coluna];
            if (!string.Equals(celula.Value as string, valor, StringComparison.Ordinal))
                celula.Value = valor;
        }
    }
}
