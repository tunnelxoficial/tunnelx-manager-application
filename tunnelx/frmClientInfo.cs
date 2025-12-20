using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace tunnelx
{
    public class frmClientInfo : Form
    {
        private TextBox txtNome;
        private MaskedTextBox txtCelular;
        private TextBox txtEmail;
        private MaskedTextBox txtCpf;
        private Button btnOk;
        private Button btnCancel;

        public string Nome { get; private set; }
        public string Celular { get; private set; }
        public string Email { get; private set; }
        public string Cpf { get; private set; }

        public frmClientInfo()
        {
            Text = "Dados do Cliente";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 380);
            BackColor = Color.White;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.White
            };
            var lblTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Informações do cliente",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 16, 0)
            };
            header.Controls.Add(lblTitle);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(16, 16, 16, 8),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            for (int i = 0; i < 5; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblNome = new Label { Text = "Nome", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10F) };
            var lblCel = new Label { Text = "Celular", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10F) };
            var lblEmail = new Label { Text = "Email", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10F) };
            var lblCpf = new Label { Text = "CPF", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10F) };

            txtNome = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F) };
            txtCelular = new MaskedTextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F), Mask = "(00) 00000-0000" };
            txtEmail = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F) };
            txtCpf = new MaskedTextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F), Mask = "000.000.000-00" };
            txtCpf.KeyPress += (s, e) =>
            {
                if (e.KeyChar == ',') e.KeyChar = '.';
            };

            btnCancel = new Button
            {
                Text = "Cancelar",
                BackColor = Color.Gainsboro,
                ForeColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat,
                Width = 110,
                Height = 36
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            btnOk = new Button
            {
                Text = "Confirmar",
                BackColor = Color.RoyalBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Width = 120,
                Height = 36
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (!ValidateCpf(txtCpf.Text))
                {
                    MessageBox.Show("CPF inválido.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (!ValidateEmail(txtEmail.Text))
                {
                    MessageBox.Show("Email inválido.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (string.IsNullOrWhiteSpace(txtNome.Text))
                {
                    MessageBox.Show("Informe o nome do cliente.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Nome = txtNome.Text.Trim();
                Celular = txtCelular.Text.Trim();
                Email = txtEmail.Text.Trim();
                Cpf = FormatCpf(txtCpf.Text);
                DialogResult = DialogResult.OK;
                Close();
            };
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true
            };
            buttons.Controls.Add(btnOk);
            buttons.Controls.Add(btnCancel);

            layout.Controls.Add(lblNome, 0, 0);
            layout.Controls.Add(txtNome, 1, 0);
            layout.Controls.Add(lblCel, 0, 1);
            layout.Controls.Add(txtCelular, 1, 1);
            layout.Controls.Add(lblEmail, 0, 2);
            layout.Controls.Add(txtEmail, 1, 2);
            layout.Controls.Add(lblCpf, 0, 3);
            layout.Controls.Add(txtCpf, 1, 3);
            layout.Controls.Add(new Panel(), 0, 4);
            layout.Controls.Add(buttons, 1, 4);

            Controls.Add(layout);
            Controls.Add(header);
        }

        private bool ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                return Regex.IsMatch(email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                    RegexOptions.IgnoreCase);
            }
            catch { return false; }
        }

        private bool ValidateCpf(string cpfMasked)
        {
            var digits = new string((cpfMasked ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length != 11) return false;
            if (new string(digits[0], 11) == digits) return false;
            int sum = 0;
            for (int i = 0; i < 9; i++) sum += (digits[i] - '0') * (10 - i);
            int r = sum % 11;
            int d1 = r < 2 ? 0 : 11 - r;
            if (d1 != (digits[9] - '0')) return false;
            sum = 0;
            for (int i = 0; i < 10; i++) sum += (digits[i] - '0') * (11 - i);
            r = sum % 11;
            int d2 = r < 2 ? 0 : 11 - r;
            if (d2 != (digits[10] - '0')) return false;
            return true;
        }

        private string FormatCpf(string cpfMasked)
        {
            var digits = new string((cpfMasked ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length != 11) return digits;
            return $"{digits.Substring(0, 3)}.{digits.Substring(3, 3)}.{digits.Substring(6, 3)}-{digits.Substring(9, 2)}";
        }
    }
}
