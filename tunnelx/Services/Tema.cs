using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;

namespace tunnelx.Services
{
    /// <summary>
    /// Pele escura do provisionador, aplicada em tempo de execucao.
    /// </summary>
    /// <remarks>
    /// Mora aqui, e nao no Designer, de proposito: o Designer do Visual Studio
    /// continua abrindo a tela normalmente, e a aparencia toda fica num arquivo
    /// so, em vez de espalhada por cem linhas de propriedades geradas.
    /// </remarks>
    public static class Tema
    {
        // ---- paleta ----------------------------------------------------------
        // Azul e verde vem da marca (#1356C1 e #5CF463), clareados o suficiente
        // para terem contraste sobre fundo escuro.
        public static readonly Color Fundo = Cor(0x070B16);
        public static readonly Color Painel = Cor(0x0E1526);
        public static readonly Color Superficie = Cor(0x121B2E);
        public static readonly Color SuperficieAlt = Cor(0x0F1727);
        public static readonly Color Borda = Cor(0x1E2B45);
        public static readonly Color BordaViva = Cor(0x2B3D60);
        public static readonly Color Texto = Cor(0xE8EFFA);
        public static readonly Color TextoFraco = Cor(0x8195B6);
        public static readonly Color Azul = Cor(0x4F8DFF);
        public static readonly Color AzulProfundo = Cor(0x1356C1);
        public static readonly Color Verde = Cor(0x5CF463);
        public static readonly Color VerdeEscuro = Cor(0x16351F);
        public static readonly Color Ambar = Cor(0xFFB020);
        public static readonly Color Vermelho = Cor(0xFF5C79);

        private static Color Cor(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        private static readonly string FonteUi = "Segoe UI";
        private static readonly string FonteNumero = "Consolas";

        /// <summary>
        /// Aplica a pele na tela inteira. Chamar uma vez, depois que os controles
        /// existem (no Load), e nao no construtor.
        /// </summary>
        public static void Aplicar(Form form)
        {
            if (form == null) return;

            form.BackColor = Fundo;
            form.ForeColor = Texto;
            form.Font = new Font(FonteUi, 9F);
            DuploBuffer(form);
            BarraDeTituloEscura(form);

            Percorrer(form);
        }

        private static void Percorrer(Control pai)
        {
            foreach (Control c in pai.Controls)
            {
                Vestir(c);
                Percorrer(c);
            }
        }

        private static void Vestir(Control c)
        {
            var grade = c as DataGridView;
            if (grade != null) { VestirGrade(grade); return; }

            var caixa = c as GroupBox;
            if (caixa != null) { VestirCaixa(caixa); return; }

            var botao = c as Button;
            if (botao != null) { VestirBotao(botao); return; }

            var link = c as LinkLabel;
            if (link != null) { VestirLink(link); return; }

            var rotulo = c as Label;
            if (rotulo != null) { VestirRotulo(rotulo); return; }

            var foto = c as PictureBox;
            if (foto != null) { VestirLogo(foto); return; }

            var campo = c as TextBox;
            if (campo != null) { VestirCampo(campo); return; }

            if (c is Panel || c is TableLayoutPanel || c is FlowLayoutPanel)
            {
                VestirPainel(c);
                return;
            }

            c.BackColor = Painel;
            c.ForeColor = Texto;
        }

        // ---- painel e cabecalho ----------------------------------------------

        private static void VestirPainel(Control c)
        {
            DuploBuffer(c);
            c.ForeColor = Texto;

            // O cabecalho ganha um tratamento proprio; os demais so escurecem.
            if (c.Name == "headerPanel")
            {
                c.BackColor = Painel;
                c.Paint -= PintarCabecalho;
                c.Paint += PintarCabecalho;
                return;
            }

            c.BackColor = c.Parent != null && c.Parent.Name == "headerPanel" ? Painel : Fundo;
        }

        /// <summary>
        /// Pinta o cabecalho: degrade, malha de pontos e um fio azul-verde embaixo.
        /// </summary>
        /// <remarks>
        /// A malha e o fio nao sao enfeite solto: separam o cabecalho da area de
        /// trabalho sem gastar uma linha divisoria dura, e o fio usa as duas cores
        /// da marca.
        /// </remarks>
        private static void PintarCabecalho(object sender, PaintEventArgs e)
        {
            var c = (Control)sender;
            var r = c.ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;

            var g = e.Graphics;

            using (var fundo = new LinearGradientBrush(r, Cor(0x0C1730), Cor(0x080D1C), 15f))
                g.FillRectangle(fundo, r);

            // Malha de pontos, bem apagada: textura, nao desenho.
            using (var ponto = new SolidBrush(Color.FromArgb(16, Azul)))
                for (var y = 6; y < r.Height; y += 14)
                    for (var x = 6; x < r.Width; x += 14)
                        g.FillRectangle(ponto, x, y, 1, 1);

            // Clarao difuso sob o logo, para o cabecalho nao ficar chapado.
            using (var caminho = new GraphicsPath())
            {
                caminho.AddEllipse(-80, -130, 420, 300);
                using (var brilho = new PathGradientBrush(caminho))
                {
                    brilho.CenterColor = Color.FromArgb(38, AzulProfundo);
                    brilho.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(brilho, caminho);
                }
            }

            // Fio inferior: azul da marca virando verde da marca.
            using (var fio = new LinearGradientBrush(
                       new Rectangle(0, r.Bottom - 2, r.Width, 2), AzulProfundo, Verde, 0f))
                g.FillRectangle(fio, 0, r.Bottom - 2, r.Width, 2);
        }

        // ---- grade -----------------------------------------------------------

        private static void VestirGrade(DataGridView g)
        {
            DuploBuffer(g);

            g.EnableHeadersVisualStyles = false;
            g.BackgroundColor = Painel;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.GridColor = Borda;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.RowHeadersVisible = false;
            g.AllowUserToResizeRows = false;

            g.ColumnHeadersDefaultCellStyle.BackColor = Painel;
            g.ColumnHeadersDefaultCellStyle.ForeColor = TextoFraco;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Painel;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextoFraco;
            g.ColumnHeadersDefaultCellStyle.Font = new Font(FonteUi, 8F, FontStyle.Bold);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            g.ColumnHeadersHeight = 34;

            g.DefaultCellStyle.BackColor = Superficie;
            g.DefaultCellStyle.ForeColor = Texto;
            g.DefaultCellStyle.SelectionBackColor = Cor(0x17335C);
            g.DefaultCellStyle.SelectionForeColor = Texto;
            g.DefaultCellStyle.Font = new Font(FonteUi, 9F);
            g.DefaultCellStyle.Padding = new Padding(5, 0, 5, 0);
            g.AlternatingRowsDefaultCellStyle.BackColor = SuperficieAlt;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Cor(0x17335C);

            g.RowTemplate.Height = 34;
            foreach (DataGridViewRow linha in g.Rows) linha.Height = 34;

            // Largura relativa por coluna. Com AutoSize Fill e o FillWeight que
            // decide quem trunca; nascendo todas em 100, o nome do cliente e os
            // numeros de trafego eram cortados enquanto sobrava espaco em "A\u00e7\u00f5es".
            foreach (DataGridViewColumn col in g.Columns)
            {
                switch (col.Name)
                {
                    case "colActions":
                        col.FillWeight = 40;
                        col.HeaderText = string.Empty;
                        break;

                    case "colClient":
                        col.FillWeight = 148;
                        break;

                    case "colPlano":
                        col.FillWeight = 100;
                        break;

                    case "colOrigem":
                        col.FillWeight = 168;
                        break;

                    case "colPeer":
                        col.HeaderText = "IP";
                        col.FillWeight = 76;
                        col.DefaultCellStyle.Font = new Font(FonteNumero, 8F);
                        col.DefaultCellStyle.ForeColor = TextoFraco;
                        break;

                    case "colEndpoint":
                        col.FillWeight = 132;
                        col.DefaultCellStyle.Font = new Font(FonteNumero, 8F);
                        break;

                    case "colHandshake":
                        // "Handshake" nao cabe e nao diz nada a quem opera.
                        col.HeaderText = "Visto";
                        col.FillWeight = 50;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                        break;

                    case "colRx":
                    case "colTx":
                        col.FillWeight = 76;
                        col.DefaultCellStyle.Font = new Font(FonteNumero, 8F);
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        // O cabecalho acompanha os numeros, senao a coluna fica torta.
                        col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                }
            }

            var acoes = g.Columns["colActions"] as DataGridViewButtonColumn;
            if (acoes != null)
            {
                acoes.FlatStyle = FlatStyle.Flat;
                acoes.DefaultCellStyle.BackColor = Superficie;
                acoes.DefaultCellStyle.ForeColor = Azul;
                acoes.DefaultCellStyle.SelectionBackColor = Cor(0x17335C);
                acoes.DefaultCellStyle.SelectionForeColor = Azul;
            }

            g.CellPainting -= PintarCelula;
            g.CellPainting += PintarCelula;
        }

        /// <summary>
        /// Desenha o estado da conexao: um ponto colorido antes do nome e o
        /// handshake tingido pela idade.
        /// </summary>
        /// <remarks>
        /// O texto "3 minutes ago" nao diz nada a quem passa o olho. A cor diz:
        /// verde e alguem conectado agora, ambar e um aparelho que esta sumindo,
        /// cinza e cadastro sem ninguem do outro lado.
        /// </remarks>
        private static void PintarCelula(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var g = (DataGridView)sender;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (g.Columns[e.ColumnIndex] is DataGridViewButtonColumn)
            {
                PintarAcoes(e);
                return;
            }

            if (g.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
            {
                PintarCaixaDeSelecao(e);
                return;
            }

            var coluna = g.Columns[e.ColumnIndex].Name;
            if (coluna == "colOrigem")
            {
                PintarOrigem(e, g.Rows[e.RowIndex]);
                return;
            }

            if (coluna != "colClient" && coluna != "colHandshake") return;

            var peer = GradeConexoes.PeerDaLinha(g.Rows[e.RowIndex]);
            if (peer == null) return;   // linha de aviso: sem estado a mostrar

            var cor = CorDoEstado(peer);

            if (coluna == "colHandshake")
            {
                e.CellStyle.ForeColor = peer.HandshakeSegundos < 0 ? TextoFraco : cor;
                return;
            }

            // colClient: ponto de estado + nome, desenhados a mao.
            e.PaintBackground(e.CellBounds, true);

            var raio = 7;
            var cx = e.CellBounds.Left + 10;
            var cy = e.CellBounds.Top + e.CellBounds.Height / 2 - raio / 2;

            var suave = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Halo so para quem esta de fato conectado: destaca no meio da lista.
            if (peer.Online)
                using (var halo = new SolidBrush(Color.FromArgb(55, cor)))
                    e.Graphics.FillEllipse(halo, cx - 4, cy - 4, raio + 8, raio + 8);

            using (var ponto = new SolidBrush(cor))
                e.Graphics.FillEllipse(ponto, cx, cy, raio, raio);

            e.Graphics.SmoothingMode = suave;

            var texto = e.FormattedValue as string ?? string.Empty;
            var corTexto = e.State.HasFlag(DataGridViewElementStates.Selected)
                ? e.CellStyle.SelectionForeColor
                : (peer.Enabled ? Texto : TextoFraco);

            TextRenderer.DrawText(
                e.Graphics, texto, e.CellStyle.Font,
                new Rectangle(cx + raio + 9, e.CellBounds.Top,
                              e.CellBounds.Width - (raio + 21), e.CellBounds.Height),
                corTexto,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            e.Handled = true;
        }

        /// <summary>
        /// Tres pontos, sem moldura de botao.
        /// </summary>
        /// <remarks>
        /// O botao nativo desenha uma moldura em cada linha. Numa lista de trinta,
        /// sao trinta retangulos disputando atencao com o conteudo — e abrir o menu
        /// nao e a acao principal de quem esta lendo a tela.
        /// </remarks>
        private static void PintarAcoes(DataGridViewCellPaintingEventArgs e)
        {
            e.PaintBackground(e.CellBounds, true);

            var suave = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var cy = e.CellBounds.Top + e.CellBounds.Height / 2 - 1;
            var cx = e.CellBounds.Left + e.CellBounds.Width / 2 - 7;

            using (var ponto = new SolidBrush(TextoFraco))
                for (var i = 0; i < 3; i++)
                    e.Graphics.FillEllipse(ponto, cx + i * 6, cy, 3, 3);

            e.Graphics.SmoothingMode = suave;
            e.Handled = true;
        }

        /// <summary>
        /// Caixa de selecao desenhada a mao.
        /// </summary>
        /// <remarks>
        /// A nativa usa as cores do sistema: um quadrado branco que, sobre fundo
        /// escuro, vira o objeto mais brilhante da tela sem ser o mais importante.
        /// </remarks>
        private static void PintarCaixaDeSelecao(DataGridViewCellPaintingEventArgs e)
        {
            e.PaintBackground(e.CellBounds, true);

            var marcado = e.FormattedValue is bool && (bool)e.FormattedValue;
            var lado = 15;
            var r = new Rectangle(
                e.CellBounds.Left + (e.CellBounds.Width - lado) / 2,
                e.CellBounds.Top + (e.CellBounds.Height - lado) / 2,
                lado, lado);

            var suave = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var caminho = Arredondado(r, 4))
            {
                using (var fundo = new SolidBrush(marcado ? Azul : Cor(0x0B1220)))
                    e.Graphics.FillPath(fundo, caminho);
                using (var borda = new Pen(marcado ? Azul : BordaViva))
                    e.Graphics.DrawPath(borda, caminho);
            }

            if (marcado)
                using (var caneta = new Pen(Color.White, 2f))
                {
                    e.Graphics.DrawLines(caneta, new[]
                    {
                        new Point(r.Left + 3, r.Top + 8),
                        new Point(r.Left + 6, r.Top + 11),
                        new Point(r.Left + 12, r.Top + 4)
                    });
                }

            e.Graphics.SmoothingMode = suave;
            e.Handled = true;
        }

        /// <summary>
        /// Desenha a origem: um selo para convidado, texto apagado para titular.
        /// </summary>
        /// <remarks>
        /// O selo existe porque essa e a pergunta que se faz varrendo a lista com
        /// o olho — "de quem e esse acesso?" — e uma palavra no meio de texto do
        /// mesmo peso nao se destaca. As palavras sao as do aplicativo:
        /// "Convidado" e "Titular", nunca um vocabulario novo.
        /// </remarks>
        private static void PintarOrigem(DataGridViewCellPaintingEventArgs e, DataGridViewRow linha)
        {
            var peer = GradeConexoes.PeerDaLinha(linha);
            if (peer == null) return;   // linha de aviso

            e.PaintBackground(e.CellBounds, true);

            var selecionada = e.State.HasFlag(DataGridViewElementStates.Selected);

            if (peer.Ficha == null || !peer.Ficha.Convidado)
            {
                TextRenderer.DrawText(
                    e.Graphics, e.FormattedValue as string ?? string.Empty, e.CellStyle.Font,
                    new Rectangle(e.CellBounds.Left + 6, e.CellBounds.Top,
                                  e.CellBounds.Width - 12, e.CellBounds.Height),
                    selecionada ? e.CellStyle.SelectionForeColor : TextoFraco,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                e.Handled = true;
                return;
            }

            var vencido = peer.PrazoVencido(DateTime.Now);
            var cor = vencido ? Vermelho : Cor(0x9B7BFF);
            var rotulo = vencido ? "VENCIDO" : "CONVIDADO";

            var fonte = new Font(FonteUi, 7.5F, FontStyle.Bold);
            try
            {
                var suave = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                var largura = TextRenderer.MeasureText(e.Graphics, rotulo, fonte).Width + 12;
                var selo = new Rectangle(e.CellBounds.Left + 6,
                                         e.CellBounds.Top + (e.CellBounds.Height - 17) / 2,
                                         largura, 17);

                using (var caminho = Arredondado(selo, 8))
                {
                    using (var fundo = new SolidBrush(Color.FromArgb(46, cor))) e.Graphics.FillPath(fundo, caminho);
                    using (var borda = new Pen(Color.FromArgb(120, cor))) e.Graphics.DrawPath(borda, caminho);
                }

                TextRenderer.DrawText(e.Graphics, rotulo, fonte, selo, cor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Graphics.SmoothingMode = suave;

                // Depois do selo vem o nome de quem emprestou o tunel.
                var dono = string.IsNullOrWhiteSpace(peer.Ficha.Dono) ? string.Empty : peer.Ficha.Dono;
                var sobra = new Rectangle(selo.Right + 6, e.CellBounds.Top,
                                          e.CellBounds.Right - selo.Right - 10, e.CellBounds.Height);

                if (sobra.Width > 20)
                {
                    TextRenderer.DrawText(e.Graphics, dono, e.CellStyle.Font, sobra,
                        selecionada ? e.CellStyle.SelectionForeColor : TextoFraco,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                }
            }
            finally
            {
                fonte.Dispose();
            }

            e.Handled = true;
        }

        /// <summary>Verde conectado, ambar sumindo, vermelho bloqueado, cinza parado.</summary>
        private static Color CorDoEstado(ConnectionSnapshot.PeerRow peer)
        {
            if (!peer.Enabled) return Vermelho;
            if (peer.HandshakeSegundos < 0) return TextoFraco;
            if (peer.HandshakeSegundos <= 180) return Verde;
            if (peer.HandshakeSegundos <= 900) return Ambar;
            return TextoFraco;
        }

        // ---- resumo no titulo do cartao --------------------------------------

        private class Resumo
        {
            public int Online, Ociosos, Parados, Fora, Total;
        }

        /// <summary>
        /// Poe a contagem por estado no titulo do cartao de conexoes.
        /// </summary>
        /// <remarks>
        /// Com trinta linhas na grade, "quantos estao conectados agora" e a
        /// pergunta que se faz ao abrir a tela — e a unica que exigia contar na
        /// mao, linha por linha.
        /// </remarks>
        public static void AtualizarResumo(GroupBox caixa, ConnectionSnapshot snap)
        {
            if (caixa == null || snap == null) return;

            var r = new Resumo { Total = snap.Peers.Count };
            foreach (var p in snap.Peers)
            {
                if (!p.Enabled) r.Fora++;
                else if (p.Online) r.Online++;
                else if (p.HandshakeSegundos < 0) r.Parados++;
                else r.Ociosos++;
            }

            var antigo = caixa.Tag as Resumo;
            if (antigo != null && antigo.Online == r.Online && antigo.Ociosos == r.Ociosos
                && antigo.Parados == r.Parados && antigo.Fora == r.Fora && antigo.Total == r.Total)
                return;   // nada mudou: nao repinta

            caixa.Tag = r;
            caixa.Invalidate(new Rectangle(0, 0, caixa.Width, 22));
        }

        /// <summary>Desenha as contagens como selos a direita do titulo.</summary>
        private static void DesenharResumo(Graphics g, GroupBox caixa, Rectangle r)
        {
            var resumo = caixa.Tag as Resumo;
            if (resumo == null) return;

            var fonte = new Font(FonteUi, 7.5F, FontStyle.Bold);
            var direita = r.Right - 6;

            Action<Color, string> selo = (cor, texto) =>
            {
                var largura = TextRenderer.MeasureText(g, texto, fonte).Width + 16;
                var caixinha = new Rectangle(direita - largura, r.Top + 2, largura, 17);

                using (var caminho = Arredondado(caixinha, 8))
                using (var fundo = new SolidBrush(Color.FromArgb(38, cor)))
                    g.FillPath(fundo, caminho);

                using (var ponto = new SolidBrush(cor))
                    g.FillEllipse(ponto, caixinha.Left + 6, caixinha.Top + 7, 4, 4);

                TextRenderer.DrawText(g, texto, fonte,
                    new Rectangle(caixinha.Left + 12, caixinha.Top, caixinha.Width - 12, caixinha.Height),
                    cor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

                direita -= largura + 5;
            };

            // Da direita para a esquerda, do menos para o mais importante.
            if (resumo.Fora > 0) selo(Vermelho, resumo.Fora + " fora");
            if (resumo.Parados > 0) selo(TextoFraco, resumo.Parados + " sem sinal");
            if (resumo.Ociosos > 0) selo(Ambar, resumo.Ociosos + " ocioso" + (resumo.Ociosos > 1 ? "s" : ""));
            selo(resumo.Online > 0 ? Verde : TextoFraco, resumo.Online + " online");

            fonte.Dispose();
        }

        // ---- caixa de grupo --------------------------------------------------

        private static void VestirCaixa(GroupBox caixa)
        {
            DuploBuffer(caixa);
            caixa.BackColor = NoCabecalho(caixa) ? Color.Transparent : Fundo;
            caixa.ForeColor = TextoFraco;
            caixa.Font = new Font(FonteUi, 8.5F, FontStyle.Bold);
            caixa.Padding = new Padding(1, 6, 1, 1);

            caixa.Paint -= PintarCaixa;
            caixa.Paint += PintarCaixa;
        }

        /// <summary>
        /// Repinta a caixa como cartao: o desenho nativo usa cores do sistema e
        /// fica com uma moldura clara berrante sobre fundo escuro.
        /// </summary>
        private static void PintarCaixa(object sender, PaintEventArgs e)
        {
            var caixa = (GroupBox)sender;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = caixa.ClientRectangle;

            // Uma GroupBox sem titulo nao esta agrupando nada visualmente: no
            // cabecalho ela so junta os botoes da conta. Desenhar um cartao ali
            // criaria uma moldura solta em volta deles.
            if (string.IsNullOrWhiteSpace(caixa.Text))
            {
                var atras = caixa.Parent != null ? caixa.Parent.BackColor : Fundo;
                using (var pincel = new SolidBrush(atras)) g.FillRectangle(pincel, r);
                return;
            }

            var topo = 22;   // faixa do titulo
            var corpo = new Rectangle(r.Left, r.Top + topo, r.Width - 1, r.Height - topo - 1);

            using (var limpa = new SolidBrush(Fundo)) g.FillRectangle(limpa, r);

            using (var caminho = Arredondado(corpo, 10))
            {
                using (var fundo = new SolidBrush(Painel)) g.FillPath(fundo, caminho);
                using (var borda = new Pen(Borda)) g.DrawPath(borda, caminho);
            }

            // Barrinha de acento antes do titulo: ancora o olho no comeco da coluna.
            using (var acento = new LinearGradientBrush(
                       new Rectangle(r.Left + 2, r.Top + 4, 3, 13), Azul, Verde, 90f))
                g.FillRectangle(acento, r.Left + 2, r.Top + 4, 3, 13);

            TextRenderer.DrawText(
                g, (caixa.Text ?? string.Empty).ToUpperInvariant(), caixa.Font,
                new Point(r.Left + 12, r.Top + 3), TextoFraco);

            DesenharResumo(g, caixa, r);
        }

        /// <summary>O controle esta dentro do cabecalho?</summary>
        private static bool NoCabecalho(Control c)
        {
            for (var p = c.Parent; p != null; p = p.Parent)
                if (p.Name == "headerPanel") return true;
            return false;
        }

        // ---- botoes ----------------------------------------------------------

        private static void VestirBotao(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.Transparent;
            b.FlatAppearance.MouseDownBackColor = Color.Transparent;
            b.BackColor = Color.Transparent;
            b.ForeColor = Texto;
            b.Font = new Font(FonteUi, 9F, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            DuploBuffer(b);

            b.Paint -= PintarBotao;
            b.Paint += PintarBotao;
            b.MouseEnter -= Repintar;
            b.MouseEnter += Repintar;
            b.MouseLeave -= Repintar;
            b.MouseLeave += Repintar;
            b.EnabledChanged -= Repintar;
            b.EnabledChanged += Repintar;
        }

        private static void Repintar(object sender, EventArgs e) { ((Control)sender).Invalidate(); }

        /// <summary>
        /// Papel do botao pela cor que o Designer ja deu. Assim a tela nao precisa
        /// ser reescrita para o tema saber o que e acao principal e o que e perigo.
        /// </summary>
        private static Color CorDoBotao(Button b)
        {
            var nome = b.Name ?? string.Empty;

            if (nome == "btStop") return Vermelho;
            if (nome == "btCriarTunel") return AzulProfundo;
            if (nome == "btGerarQr") return Cor(0x1E7D52);
            if (nome == "btGerarConfig") return Cor(0x5B4BD6);
            return Superficie;
        }

        private static bool Secundario(Button b)
        {
            var c = CorDoBotao(b);
            return c == Superficie;
        }

        private static void PintarBotao(object sender, PaintEventArgs e)
        {
            var b = (Button)sender;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
            var baseCor = CorDoBotao(b);
            var sobre = b.ClientRectangle.Contains(b.PointToClient(Cursor.Position));

            using (var limpa = new SolidBrush(b.Parent != null ? b.Parent.BackColor : Fundo))
                g.FillRectangle(limpa, b.ClientRectangle);

            using (var caminho = Arredondado(r, 8))
            {
                if (!b.Enabled)
                {
                    using (var f = new SolidBrush(Cor(0x141C2C))) g.FillPath(f, caminho);
                    using (var p = new Pen(Borda)) g.DrawPath(p, caminho);
                    TextRenderer.DrawText(g, b.Text, b.Font, b.ClientRectangle, Cor(0x4A5670),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return;
                }

                if (Secundario(b))
                {
                    // Contorno: acao de apoio nao pode competir com a principal.
                    var perigo = b.Name == "btSair";
                    var frente = perigo ? Vermelho : (sobre ? Texto : TextoFraco);

                    using (var f = new SolidBrush(sobre ? Cor(0x1A2540) : Superficie)) g.FillPath(f, caminho);
                    using (var p = new Pen(perigo && sobre ? Vermelho : (sobre ? BordaViva : Borda)))
                        g.DrawPath(p, caminho);
                    TextRenderer.DrawText(g, b.Text, b.Font, b.ClientRectangle, frente,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return;
                }

                var topo = Clarear(baseCor, sobre ? 0.30f : 0.16f);
                var baixo = sobre ? baseCor : Escurecer(baseCor, 0.12f);
                using (var f = new LinearGradientBrush(new Rectangle(0, 0, b.Width, b.Height), topo, baixo, 90f))
                    g.FillPath(f, caminho);

                // Fio de luz no topo: da volume sem precisar de sombra.
                using (var brilho = new Pen(Color.FromArgb(sobre ? 110 : 70, Color.White)))
                    g.DrawLine(brilho, r.Left + 8, r.Top + 1, r.Right - 8, r.Top + 1);

                TextRenderer.DrawText(g, b.Text, b.Font, b.ClientRectangle, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // ---- busca -----------------------------------------------------------

        /// <summary>Texto de apoio do campo de busca, quando ele esta vazio.</summary>
        private const string DicaBusca = "Buscar por nome, CPF, plano, titular ou IP…";

        private static void VestirCampo(TextBox campo)
        {
            campo.BorderStyle = BorderStyle.None;
            campo.BackColor = Superficie;
            campo.ForeColor = Texto;
            campo.Font = new Font(FonteUi, 10F);

            if (campo.Name != "txtBusca") return;

            // O texto de dica e desenhado pelo Windows (EM_SETCUEBANNER) em vez de
            // ser posto como Text: assim ele nao vira conteudo do campo e nao
            // precisa ser limpo no foco — sem isso, o filtro passaria a procurar
            // pela propria dica.
            try { SendMessage(campo.Handle, 0x1501, (IntPtr)1, DicaBusca); }
            catch { }

            var painel = campo.Parent;
            if (painel == null) return;

            // A lupa e o espaco a esquerda dela sao desenhados pelo painel, nao por
            // um controle proprio: um PictureBox ali roubaria o clique do campo.
            painel.Padding = new Padding(34, 10, 10, 8);
            painel.Paint -= PintarBusca;
            painel.Paint += PintarBusca;
            DuploBuffer(painel);

            campo.GotFocus -= RepintarPai;
            campo.GotFocus += RepintarPai;
            campo.LostFocus -= RepintarPai;
            campo.LostFocus += RepintarPai;
        }

        private static void RepintarPai(object sender, EventArgs e)
        {
            var c = sender as Control;
            if (c != null && c.Parent != null) c.Parent.Invalidate();
        }

        /// <summary>Desenha o campo de busca: cartao arredondado com lupa.</summary>
        private static void PintarBusca(object sender, PaintEventArgs e)
        {
            var painel = (Control)sender;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var limpa = new SolidBrush(Painel)) g.FillRectangle(limpa, painel.ClientRectangle);

            var campo = painel.Controls["txtBusca"];
            if (campo == null) return;

            // O cartao envolve o campo, nao a faixa: o contador de resultados mora
            // a direita e nao deve parecer parte da caixa de digitar.
            var r = new Rectangle(4, 4,
                                  campo.Right + painel.Padding.Right - 4,
                                  painel.Height - 9);
            if (r.Width <= 0 || r.Height <= 0) return;

            var focado = campo.Focused;
            var comTexto = campo.Text.Length > 0;

            using (var caminho = Arredondado(r, 8))
            {
                using (var fundo = new SolidBrush(Superficie)) g.FillPath(fundo, caminho);
                using (var borda = new Pen(focado ? Azul : Borda)) g.DrawPath(borda, caminho);
            }

            // Lupa: circulo mais cabo. Desenhada a mao para nao trazer imagem junto.
            var cor = focado || comTexto ? Azul : TextoFraco;
            using (var caneta = new Pen(cor, 1.6f))
            {
                var cx = r.Left + 13;
                var cy = r.Top + r.Height / 2 - 1;
                g.DrawEllipse(caneta, cx - 5, cy - 5, 9, 9);
                g.DrawLine(caneta, cx + 3, cy + 3, cx + 7, cy + 7);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr janela, int mensagem, IntPtr wParam, string lParam);

        // ---- rotulos e links -------------------------------------------------

        private static void VestirRotulo(Label l)
        {
            if (l.Name == "lblResultado")
            {
                l.BackColor = Painel;
                l.ForeColor = TextoFraco;
                l.Font = new Font(FonteUi, 8.5F);
                return;
            }

            // Os separadores do cabecalho sao Labels de 1px de altura.
            if (l.Height <= 2)
            {
                l.BackColor = Borda;
                return;
            }

            // O Label branco de 748x118 atras do cabecalho e so pano de fundo.
            if (l.Width > 600 && string.IsNullOrEmpty(l.Text))
            {
                l.BackColor = Color.Transparent;
                l.Visible = false;
                return;
            }

            l.BackColor = Color.Transparent;

            var claro = l.ForeColor.GetBrightness() < 0.45f;
            if (l.ForeColor == Color.RoyalBlue) l.ForeColor = Azul;
            else if (claro) l.ForeColor = Texto;
            else l.ForeColor = TextoFraco;
        }

        private static void VestirLink(LinkLabel l)
        {
            l.BackColor = Color.Transparent;
            l.LinkColor = Azul;
            l.ActiveLinkColor = Verde;
            l.VisitedLinkColor = Azul;
            l.LinkBehavior = LinkBehavior.HoverUnderline;
        }

        // ---- logo ------------------------------------------------------------

        private static void VestirLogo(PictureBox foto)
        {
            foto.BackColor = Color.Transparent;
            if (foto.Image == null) return;

            try { foto.Image = ParaFundoEscuro(foto.Image); }
            catch { /* o logo original continua servindo */ }
        }

        /// <summary>
        /// Reescreve o logo para fundo escuro: o branco vira transparente e as
        /// cores escuras sao clareadas.
        /// </summary>
        /// <remarks>
        /// O arquivo e JPEG, formato sem transparencia: sobre fundo escuro ele
        /// apareceria como um retangulo branco com letras quase invisiveis.
        /// A transparencia sai da distancia ate o branco (o canal mais claro do
        /// pixel), nao da luminancia — assim o verde da marca, que e claro, nao
        /// desaparece junto com o fundo.
        /// </remarks>
        public static Image ParaFundoEscuro(Image original)
        {
            var largura = original.Width;
            var altura = original.Height;

            using (var fonte = new Bitmap(original))
            {
                var saida = new Bitmap(largura, altura, PixelFormat.Format32bppArgb);

                var rInt = new Rectangle(0, 0, largura, altura);
                var dadosEnt = fonte.LockBits(rInt, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var dadosSai = saida.LockBits(rInt, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                try
                {
                    var total = largura * altura;
                    var buffer = new int[total];
                    System.Runtime.InteropServices.Marshal.Copy(dadosEnt.Scan0, buffer, 0, total);

                    for (var i = 0; i < total; i++)
                    {
                        var px = buffer[i];
                        int r = (px >> 16) & 0xFF, gr = (px >> 8) & 0xFF, bl = px & 0xFF;

                        // Distancia ate o branco. Branco puro -> 0 -> some.
                        var menor = Math.Min(r, Math.Min(gr, bl));
                        var alfa = (int)Math.Min(255, (255 - menor) * 1.8);
                        if (alfa <= 6) { buffer[i] = 0; continue; }

                        // Clareia na medida do escuro: o azul-marinho da tipografia
                        // sobe quase ate o branco, o verde da marca fica como esta.
                        var lum = (0.299 * r + 0.587 * gr + 0.114 * bl) / 255.0;
                        var t = Math.Max(0.0, Math.Min(0.86, 1.0 - lum * 1.55));

                        r = (int)(r + (255 - r) * t);
                        gr = (int)(gr + (255 - gr) * t);
                        bl = (int)(bl + (255 - bl) * t);

                        buffer[i] = (alfa << 24) | (r << 16) | (gr << 8) | bl;
                    }

                    System.Runtime.InteropServices.Marshal.Copy(buffer, 0, dadosSai.Scan0, total);
                }
                finally
                {
                    fonte.UnlockBits(dadosEnt);
                    saida.UnlockBits(dadosSai);
                }

                return saida;
            }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr janela, int atributo, ref int valor, int tamanho);

        /// <summary>
        /// Pede ao Windows a barra de titulo escura.
        /// </summary>
        /// <remarks>
        /// Sem isto a janela fica com a barra clara do sistema em cima de uma tela
        /// preta — a unica parte que nao acompanha o resto. O atributo mudou de
        /// numero no meio do Windows 10 (19 antes da build 18985, 20 depois), por
        /// isso as duas tentativas. Em versoes que nao conhecem nenhum dos dois a
        /// chamada falha em silencio e a barra continua clara.
        /// </remarks>
        public static void BarraDeTituloEscura(Form form)
        {
            try
            {
                if (!form.IsHandleCreated) return;

                var sim = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref sim, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref sim, sizeof(int));

                // O DWM so aplica a cor no proximo desenho da moldura. Sem este
                // empurrao, uma janela ja aberta segue com a barra clara ate ser
                // minimizada e restaurada.
                if (form.Visible)
                {
                    form.Width += 1;
                    form.Width -= 1;
                }
            }
            catch { }   // dwmapi ausente: a janela so fica com a barra clara
        }

        // ---- utilidades ------------------------------------------------------

        /// <summary>Retangulo de cantos arredondados.</summary>
        public static GraphicsPath Arredondado(Rectangle r, int raio)
        {
            var caminho = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) { caminho.AddRectangle(r); return caminho; }

            var d = Math.Min(raio * 2, Math.Min(r.Width, r.Height));
            caminho.AddArc(r.Left, r.Top, d, d, 180, 90);
            caminho.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            caminho.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            caminho.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            caminho.CloseFigure();
            return caminho;
        }

        public static Color Clarear(Color c, float t)
        {
            return Color.FromArgb(c.A,
                (int)(c.R + (255 - c.R) * t),
                (int)(c.G + (255 - c.G) * t),
                (int)(c.B + (255 - c.B) * t));
        }

        public static Color Escurecer(Color c, float t)
        {
            return Color.FromArgb(c.A, (int)(c.R * (1 - t)), (int)(c.G * (1 - t)), (int)(c.B * (1 - t)));
        }

        /// <summary>
        /// Liga o buffer duplo, que a DataGridView so expoe como propriedade protegida.
        /// </summary>
        /// <remarks>
        /// Sem isto a grade pisca a cada atualizacao, por redesenhar direto na tela.
        /// E a unica razao para usar reflexao aqui.
        /// </remarks>
        public static void DuploBuffer(Control c)
        {
            if (SystemInformation.TerminalServerSession) return;   // em RDP atrapalha

            try
            {
                var prop = typeof(Control).GetProperty(
                    "DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (prop != null) prop.SetValue(c, true, null);
            }
            catch { }
        }
    }
}
