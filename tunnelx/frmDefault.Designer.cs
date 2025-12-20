namespace tunnelx
{
    partial class frmDefault
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle13 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle14 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle16 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle15 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle17 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle18 = new System.Windows.Forms.DataGridViewCellStyle();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.btSair = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btGerarQr = new System.Windows.Forms.Button();
            this.btGerarConfig = new System.Windows.Forms.Button();
            this.btCriarTunel = new System.Windows.Forms.Button();
            this.btStop = new System.Windows.Forms.Button();
            this.dtgInterfaces = new System.Windows.Forms.DataGridView();
            this.chkInterfaces = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.txtINterface = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.status = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblGridListaInterfaces = new System.Windows.Forms.Label();
            this.actionsLayout = new System.Windows.Forms.TableLayoutPanel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.button2 = new System.Windows.Forms.Button();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tmrStatus = new System.Windows.Forms.Timer(this.components);
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.dtgConexoesAtivas = new System.Windows.Forms.DataGridView();
            this.colActions = new System.Windows.Forms.DataGridViewButtonColumn();
            this.colClient = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPeer = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colEndpoint = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHandshake = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colRx = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTx = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.mainLayout = new System.Windows.Forms.TableLayoutPanel();
            this.headerPanel = new System.Windows.Forms.Panel();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dtgInterfaces)).BeginInit();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dtgConexoesAtivas)).BeginInit();
            this.mainLayout.SuspendLayout();
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.BackColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(-4, -3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(748, 118);
            this.label1.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.BackColor = System.Drawing.Color.Gainsboro;
            this.label2.Location = new System.Drawing.Point(378, 64);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(200, 1);
            this.label2.TabIndex = 2;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.BackColor = System.Drawing.Color.White;
            this.label3.Font = new System.Drawing.Font("Trebuchet MS", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.Black;
            this.label3.Location = new System.Drawing.Point(370, 9);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(190, 25);
            this.label3.TabIndex = 3;
            this.label3.Text = "Dyllan Nicolau da Silva";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btSair
            // 
            this.btSair.BackColor = System.Drawing.Color.MistyRose;
            this.btSair.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btSair.Font = new System.Drawing.Font("Trebuchet MS", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btSair.ForeColor = System.Drawing.Color.Tomato;
            this.btSair.Location = new System.Drawing.Point(67, 53);
            this.btSair.Name = "btSair";
            this.btSair.Size = new System.Drawing.Size(71, 33);
            this.btSair.TabIndex = 4;
            this.btSair.Text = "➡️ Sair";
            this.btSair.UseVisualStyleBackColor = false;
            // 
            // label4
            // 
            this.label4.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label4.BackColor = System.Drawing.Color.White;
            this.label4.Font = new System.Drawing.Font("Trebuchet MS", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.ForeColor = System.Drawing.Color.DimGray;
            this.label4.Location = new System.Drawing.Point(370, 34);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(190, 25);
            this.label4.TabIndex = 5;
            this.label4.Text = "dyllannicolau@gmail.com";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.AutoSize = true;
            this.label5.BackColor = System.Drawing.Color.White;
            this.label5.Font = new System.Drawing.Font("Trebuchet MS", 7.8F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.ForeColor = System.Drawing.Color.DarkGray;
            this.label5.Location = new System.Drawing.Point(445, 67);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(139, 16);
            this.label5.TabIndex = 6;
            this.label5.Text = "licença expira em 365 dias";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.AutoSize = true;
            this.label6.BackColor = System.Drawing.Color.White;
            this.label6.Font = new System.Drawing.Font("Trebuchet MS", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.ForeColor = System.Drawing.Color.RoyalBlue;
            this.label6.Location = new System.Drawing.Point(555, 9);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(29, 22);
            this.label6.TabIndex = 7;
            this.label6.Text = "👤";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label7
            // 
            this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label7.AutoSize = true;
            this.label7.BackColor = System.Drawing.Color.White;
            this.label7.Font = new System.Drawing.Font("Trebuchet MS", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.ForeColor = System.Drawing.Color.RoyalBlue;
            this.label7.Location = new System.Drawing.Point(554, 35);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(32, 22);
            this.label7.TabIndex = 8;
            this.label7.Text = "✉️";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.Azure;
            this.button1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.button1.FlatAppearance.BorderSize = 0;
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Font = new System.Drawing.Font("Trebuchet MS", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.ForeColor = System.Drawing.Color.RoyalBlue;
            this.button1.Location = new System.Drawing.Point(10, 16);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(128, 33);
            this.button1.TabIndex = 9;
            this.button1.Text = "Minha conta";
            this.button1.UseVisualStyleBackColor = false;
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.BackColor = System.Drawing.Color.White;
            this.linkLabel1.Font = new System.Drawing.Font("Trebuchet MS", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkLabel1.Location = new System.Drawing.Point(504, 81);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(80, 16);
            this.linkLabel1.TabIndex = 11;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "Renovar agora";
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.BackColor = System.Drawing.Color.WhiteSmoke;
            this.linkLabel2.Font = new System.Drawing.Font("Trebuchet MS", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkLabel2.Location = new System.Drawing.Point(137, 79);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(0, 16);
            this.linkLabel2.TabIndex = 12;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.dtgInterfaces);
            this.groupBox1.Controls.Add(this.lblGridListaInterfaces);
            this.groupBox1.Controls.Add(this.actionsLayout);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(3, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(254, 515);
            this.groupBox1.TabIndex = 14;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Lista de Interfaces";
            // 
            // btGerarQr
            // 
            this.btGerarQr.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btGerarQr.BackColor = System.Drawing.Color.SeaGreen;
            this.btGerarQr.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btGerarQr.FlatAppearance.BorderSize = 0;
            this.btGerarQr.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btGerarQr.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btGerarQr.ForeColor = System.Drawing.Color.White;
            this.btGerarQr.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btGerarQr.Name = "btGerarQr";
            this.btGerarQr.Size = new System.Drawing.Size(237, 40);
            this.btGerarQr.TabIndex = 13;
            this.btGerarQr.Text = "Gerar QR para App";
            this.btGerarQr.UseVisualStyleBackColor = false;
            this.btGerarQr.Click += new System.EventHandler(this.btGerarQr_Click);
            // 
            // btGerarConfig
            // 
            this.btGerarConfig.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btGerarConfig.BackColor = System.Drawing.Color.MediumSlateBlue;
            this.btGerarConfig.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btGerarConfig.FlatAppearance.BorderSize = 0;
            this.btGerarConfig.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btGerarConfig.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btGerarConfig.ForeColor = System.Drawing.Color.White;
            this.btGerarConfig.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btGerarConfig.Name = "btGerarConfig";
            this.btGerarConfig.Size = new System.Drawing.Size(239, 40);
            this.btGerarConfig.TabIndex = 12;
            this.btGerarConfig.Text = "Gerar Config Cliente";
            this.btGerarConfig.UseVisualStyleBackColor = false;
            this.btGerarConfig.Click += new System.EventHandler(this.btGerarConfig_Click);
            // 
            // btCriarTunel
            // 
            this.btCriarTunel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btCriarTunel.BackColor = System.Drawing.Color.RoyalBlue;
            this.btCriarTunel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btCriarTunel.FlatAppearance.BorderSize = 0;
            this.btCriarTunel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btCriarTunel.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btCriarTunel.ForeColor = System.Drawing.Color.White;
            this.btCriarTunel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btCriarTunel.Name = "btCriarTunel";
            this.btCriarTunel.Size = new System.Drawing.Size(239, 44);
            this.btCriarTunel.TabIndex = 11;
            this.btCriarTunel.Text = "Criar Túnel";
            this.btCriarTunel.UseVisualStyleBackColor = false;
            this.btCriarTunel.Click += new System.EventHandler(this.btCriarTunel_Click);
            // 
            // btStop
            // 
            this.btStop.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btStop.BackColor = System.Drawing.Color.Red;
            this.btStop.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btStop.FlatAppearance.BorderSize = 0;
            this.btStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btStop.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btStop.ForeColor = System.Drawing.Color.White;
            this.btStop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btStop.Name = "btStop";
            this.btStop.Size = new System.Drawing.Size(239, 44);
            this.btStop.TabIndex = 12;
            this.btStop.Text = "Parar Túnel";
            this.btStop.UseVisualStyleBackColor = false;
            this.btStop.Click += new System.EventHandler(this.btStop_Click);
            // 
            // dtgInterfaces
            // 
            this.dtgInterfaces.AllowUserToAddRows = false;
            this.dtgInterfaces.AllowUserToOrderColumns = true;
            dataGridViewCellStyle13.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
            this.dtgInterfaces.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle13;
            this.dtgInterfaces.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dtgInterfaces.BackgroundColor = System.Drawing.Color.White;
            this.dtgInterfaces.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dtgInterfaces.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            this.dtgInterfaces.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle14.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle14.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            dataGridViewCellStyle14.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle14.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            dataGridViewCellStyle14.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            dataGridViewCellStyle14.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.dtgInterfaces.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle14;
            this.dtgInterfaces.ColumnHeadersHeight = 36;
            this.dtgInterfaces.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dtgInterfaces.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.chkInterfaces,
            this.txtINterface,
            this.status});
            this.dtgInterfaces.Cursor = System.Windows.Forms.Cursors.Hand;
            dataGridViewCellStyle16.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle16.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle16.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle16.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            dataGridViewCellStyle16.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            dataGridViewCellStyle16.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            dataGridViewCellStyle16.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dtgInterfaces.DefaultCellStyle = dataGridViewCellStyle16;
            this.dtgInterfaces.EnableHeadersVisualStyles = false;
            this.dtgInterfaces.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dtgInterfaces.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dtgInterfaces.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(230)))), ((int)(((byte)(230)))));
            this.dtgInterfaces.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dtgInterfaces.Name = "dtgInterfaces";
            this.dtgInterfaces.ReadOnly = true;
            this.dtgInterfaces.RowHeadersVisible = false;
            this.dtgInterfaces.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dtgInterfaces.RowTemplate.Height = 30;
            this.dtgInterfaces.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dtgInterfaces.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dtgInterfaces.Size = new System.Drawing.Size(245, 333);
            this.dtgInterfaces.TabIndex = 1;
            // 
            // chkInterfaces
            // 
            this.chkInterfaces.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.chkInterfaces.HeaderText = "";
            this.chkInterfaces.MinimumWidth = 50;
            this.chkInterfaces.Name = "chkInterfaces";
            this.chkInterfaces.ReadOnly = true;
            this.chkInterfaces.Width = 50;
            this.chkInterfaces.Visible = false;
            // 
            // txtINterface
            // 
            this.txtINterface.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.txtINterface.HeaderText = "Interface";
            this.txtINterface.Name = "txtINterface";
            this.txtINterface.ReadOnly = true;
            // 
            // status
            // 
            this.status.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            dataGridViewCellStyle15.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            this.status.DefaultCellStyle = dataGridViewCellStyle15;
            this.status.HeaderText = "Status";
            this.status.MinimumWidth = 40;
            this.status.Name = "status";
            this.status.ReadOnly = true;
            this.status.Width = 40;
            // 
            // lblGridListaInterfaces
            // 
            this.lblGridListaInterfaces.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblGridListaInterfaces.Name = "lblGridListaInterfaces";
            this.lblGridListaInterfaces.Height = 28;
            this.lblGridListaInterfaces.TabIndex = 0;
            this.lblGridListaInterfaces.Text = "...";
            this.lblGridListaInterfaces.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // actionsLayout
            // 
            this.actionsLayout.AutoSize = true;
            this.actionsLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.actionsLayout.ColumnCount = 1;
            this.actionsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.actionsLayout.Controls.Add(this.btGerarQr, 0, 0);
            this.actionsLayout.Controls.Add(this.btGerarConfig, 0, 1);
            this.actionsLayout.Controls.Add(this.btCriarTunel, 0, 2);
            this.actionsLayout.Controls.Add(this.btStop, 0, 3);
            this.actionsLayout.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.actionsLayout.Location = new System.Drawing.Point(3, 329);
            this.actionsLayout.Name = "actionsLayout";
            this.actionsLayout.Padding = new System.Windows.Forms.Padding(0);
            this.actionsLayout.RowCount = 4;
            this.actionsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.actionsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.actionsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.actionsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.actionsLayout.Size = new System.Drawing.Size(248, 183);
            this.actionsLayout.TabIndex = 102;
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.button2);
            this.groupBox2.Controls.Add(this.button1);
            this.groupBox2.Controls.Add(this.btSair);
            this.groupBox2.Location = new System.Drawing.Point(590, 5);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(147, 95);
            this.groupBox2.TabIndex = 15;
            this.groupBox2.TabStop = false;
            // 
            // button2
            // 
            this.button2.BackColor = System.Drawing.Color.Honeydew;
            this.button2.Cursor = System.Windows.Forms.Cursors.Hand;
            this.button2.Font = new System.Drawing.Font("Trebuchet MS", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button2.ForeColor = System.Drawing.Color.ForestGreen;
            this.button2.Location = new System.Drawing.Point(10, 53);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(53, 33);
            this.button2.TabIndex = 10;
            this.button2.Text = "⚙️";
            this.toolTip1.SetToolTip(this.button2, "Configurações");
            this.button2.UseVisualStyleBackColor = false;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::tunnelx.Properties.Resources.logo_horizontal;
            this.pictureBox1.Location = new System.Drawing.Point(0, -2);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(249, 115);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // toolTip1
            // 
            this.toolTip1.BackColor = System.Drawing.Color.Black;
            this.toolTip1.ForeColor = System.Drawing.Color.White;
            this.toolTip1.ToolTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            this.toolTip1.ToolTipTitle = "Info";
            // 
            // tmrStatus
            // 
            this.tmrStatus.Interval = 1000;
            this.tmrStatus.Tick += new System.EventHandler(this.tmrStatus_Tick);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.dtgConexoesAtivas);
            this.groupBox3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox3.Location = new System.Drawing.Point(263, 3);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(478, 515);
            this.groupBox3.TabIndex = 16;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Conexões Ativas";
            // 
            // dtgConexoesAtivas
            // 
            this.dtgConexoesAtivas.AllowUserToAddRows = false;
            this.dtgConexoesAtivas.AllowUserToOrderColumns = true;
            this.dtgConexoesAtivas.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dtgConexoesAtivas.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dtgConexoesAtivas.BackgroundColor = System.Drawing.Color.White;
            this.dtgConexoesAtivas.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dtgConexoesAtivas.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.RaisedHorizontal;
            this.dtgConexoesAtivas.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle17.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle17.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            dataGridViewCellStyle17.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle17.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            dataGridViewCellStyle17.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            dataGridViewCellStyle17.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.dtgConexoesAtivas.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle17;
            this.dtgConexoesAtivas.ColumnHeadersHeight = 32;
            this.dtgConexoesAtivas.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dtgConexoesAtivas.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colClient,
            this.colPeer,
            this.colEndpoint,
            this.colHandshake,
            this.colRx,
            this.colTx,
            this.colActions});
            this.dtgConexoesAtivas.Cursor = System.Windows.Forms.Cursors.Hand;
            dataGridViewCellStyle18.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle18.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle18.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle18.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            dataGridViewCellStyle18.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            dataGridViewCellStyle18.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            dataGridViewCellStyle18.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dtgConexoesAtivas.DefaultCellStyle = dataGridViewCellStyle18;
            this.dtgConexoesAtivas.EnableHeadersVisualStyles = false;
            this.dtgConexoesAtivas.Location = new System.Drawing.Point(3, 19);
            this.dtgConexoesAtivas.Name = "dtgConexoesAtivas";
            this.dtgConexoesAtivas.ReadOnly = true;
            this.dtgConexoesAtivas.RowHeadersVisible = false;
            this.dtgConexoesAtivas.RowTemplate.Height = 28;
            this.dtgConexoesAtivas.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dtgConexoesAtivas.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dtgConexoesAtivas.Size = new System.Drawing.Size(472, 493);
            this.dtgConexoesAtivas.TabIndex = 0;
            this.dtgConexoesAtivas.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dtgConexoesAtivas_CellContentClick);
            // 
            // colActions
            // 
            this.colActions.HeaderText = "Ações";
            this.colActions.Name = "colActions";
            this.colActions.Text = "Ações";
            this.colActions.UseColumnTextForButtonValue = true;
            this.colActions.Width = 80;
            // 
            // colPeer
            // 
            this.colPeer.HeaderText = "Peer";
            this.colPeer.Name = "colPeer";
            this.colPeer.ReadOnly = true;
            // 
            // colClient
            // 
            this.colClient.HeaderText = "Cliente";
            this.colClient.Name = "colClient";
            this.colClient.ReadOnly = true;
            // 
            // colEndpoint
            // 
            this.colEndpoint.HeaderText = "Endpoint";
            this.colEndpoint.Name = "colEndpoint";
            this.colEndpoint.ReadOnly = true;
            // 
            // colHandshake
            // 
            this.colHandshake.HeaderText = "Handshake";
            this.colHandshake.Name = "colHandshake";
            this.colHandshake.ReadOnly = true;
            // 
            // colRx
            // 
            this.colRx.HeaderText = "Rx";
            this.colRx.Name = "colRx";
            this.colRx.ReadOnly = true;
            // 
            // colTx
            // 
            this.colTx.HeaderText = "Tx";
            this.colTx.Name = "colTx";
            this.colTx.ReadOnly = true;
            // 
            // mainLayout
            // 
            this.mainLayout.ColumnCount = 2;
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 35F));
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.mainLayout.Controls.Add(this.groupBox1, 0, 0);
            this.mainLayout.Controls.Add(this.groupBox3, 1, 0);
            this.mainLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainLayout.Location = new System.Drawing.Point(0, 0);
            this.mainLayout.Margin = new System.Windows.Forms.Padding(0);
            this.mainLayout.Name = "mainLayout";
            this.mainLayout.RowCount = 1;
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayout.Size = new System.Drawing.Size(744, 521);
            this.mainLayout.TabIndex = 101;
            // 
            // headerPanel
            // 
            this.headerPanel.BackColor = System.Drawing.Color.White;
            this.headerPanel.Controls.Add(this.groupBox2);
            this.headerPanel.Controls.Add(this.label4);
            this.headerPanel.Controls.Add(this.label3);
            this.headerPanel.Controls.Add(this.label5);
            this.headerPanel.Controls.Add(this.label7);
            this.headerPanel.Controls.Add(this.label6);
            this.headerPanel.Controls.Add(this.label2);
            this.headerPanel.Controls.Add(this.pictureBox1);
            this.headerPanel.Controls.Add(this.label1);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Size = new System.Drawing.Size(744, 118);
            this.headerPanel.TabIndex = 100;
            // 
            // frmDefault
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(744, 521);
            this.Controls.Add(this.mainLayout);
            this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.linkLabel2);
            this.Controls.Add(this.linkLabel1);
            this.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MinimumSize = new System.Drawing.Size(760, 560);
            this.Name = "frmDefault";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Fomulário de MVP do TunnelX";
            this.Load += new System.EventHandler(this.frmTeste_Load);
            this.groupBox1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dtgInterfaces)).EndInit();
            this.groupBox2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dtgConexoesAtivas)).EndInit();
            this.mainLayout.ResumeLayout(false);
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btSair;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.LinkLabel linkLabel2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label lblGridListaInterfaces;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.DataGridView dtgInterfaces;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.DataGridViewCheckBoxColumn chkInterfaces;
        private System.Windows.Forms.DataGridViewTextBoxColumn txtINterface;
        private System.Windows.Forms.DataGridViewTextBoxColumn status;
        private System.Windows.Forms.Button btCriarTunel;
        private System.Windows.Forms.Timer tmrStatus;
        private System.Windows.Forms.Button btStop;
        private System.Windows.Forms.Button btGerarConfig;
        private System.Windows.Forms.Button btGerarQr;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.DataGridView dtgConexoesAtivas;
        private System.Windows.Forms.DataGridViewButtonColumn colActions;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPeer;
        private System.Windows.Forms.DataGridViewTextBoxColumn colClient;
        private System.Windows.Forms.DataGridViewTextBoxColumn colEndpoint;
        private System.Windows.Forms.DataGridViewTextBoxColumn colHandshake;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRx;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTx;
        private System.Windows.Forms.TableLayoutPanel mainLayout;
        private System.Windows.Forms.Panel headerPanel;
        private System.Windows.Forms.TableLayoutPanel actionsLayout;
    }
}
