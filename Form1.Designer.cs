namespace ProcessHibernator {
    partial class Form1 {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            BtnHibernate = new Button();
            BtnWake = new Button();
            LblStatus = new Label();
            LbApps = new ListBox();
            LbSuspended = new ListBox();
            label1 = new Label();
            label2 = new Label();
            PicGraph = new PictureBox();
            GraphTimer = new System.Windows.Forms.Timer(components);
            label3 = new Label();
            TrayIcon = new NotifyIcon(components);
            TrayMenu = new ContextMenuStrip(components);
            MenuRestore = new ToolStripMenuItem();
            MenuQuit = new ToolStripMenuItem();
            BtnGhostWake = new Button();
            BtnKill = new Button();
            ((System.ComponentModel.ISupportInitialize)PicGraph).BeginInit();
            TrayMenu.SuspendLayout();
            SuspendLayout();
            // 
            // BtnHibernate
            // 
            BtnHibernate.BackColor = Color.FromArgb(60, 60, 60);
            BtnHibernate.FlatAppearance.BorderSize = 0;
            BtnHibernate.FlatStyle = FlatStyle.Flat;
            BtnHibernate.ForeColor = Color.White;
            BtnHibernate.Location = new Point(12, 342);
            BtnHibernate.Name = "BtnHibernate";
            BtnHibernate.Size = new Size(185, 35);
            BtnHibernate.TabIndex = 0;
            BtnHibernate.Text = "Hibernate Selected ↓";
            BtnHibernate.UseVisualStyleBackColor = false;
            BtnHibernate.Click += BtnHibernate_Click;
            // 
            // BtnWake
            // 
            BtnWake.BackColor = Color.FromArgb(60, 60, 60);
            BtnWake.FlatAppearance.BorderSize = 0;
            BtnWake.FlatStyle = FlatStyle.Flat;
            BtnWake.ForeColor = Color.White;
            BtnWake.Location = new Point(408, 342);
            BtnWake.Name = "BtnWake";
            BtnWake.Size = new Size(185, 35);
            BtnWake.TabIndex = 1;
            BtnWake.Text = "Wake Normal ↑";
            BtnWake.UseVisualStyleBackColor = false;
            BtnWake.Click += BtnWake_Click;
            // 
            // LblStatus
            // 
            LblStatus.AutoSize = true;
            LblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LblStatus.ForeColor = Color.DarkGray;
            LblStatus.Location = new Point(12, 385);
            LblStatus.Name = "LblStatus";
            LblStatus.Size = new Size(45, 15);
            LblStatus.TabIndex = 3;
            LblStatus.Text = "Status: ";
            // 
            // LbApps
            // 
            LbApps.BackColor = Color.FromArgb(45, 45, 45);
            LbApps.BorderStyle = BorderStyle.FixedSingle;
            LbApps.DrawMode = DrawMode.OwnerDrawFixed;
            LbApps.ForeColor = Color.White;
            LbApps.FormattingEnabled = true;
            LbApps.ItemHeight = 40;
            LbApps.Location = new Point(12, 32);
            LbApps.Name = "LbApps";
            LbApps.Size = new Size(380, 284);
            LbApps.TabIndex = 5;
            LbApps.DrawItem += Lb_DrawItem;
            // 
            // LbSuspended
            // 
            LbSuspended.BackColor = Color.FromArgb(45, 45, 45);
            LbSuspended.BorderStyle = BorderStyle.FixedSingle;
            LbSuspended.DrawMode = DrawMode.OwnerDrawFixed;
            LbSuspended.ForeColor = Color.White;
            LbSuspended.FormattingEnabled = true;
            LbSuspended.ItemHeight = 40;
            LbSuspended.Location = new Point(408, 32);
            LbSuspended.Name = "LbSuspended";
            LbSuspended.Size = new Size(380, 284);
            LbSuspended.TabIndex = 7;
            LbSuspended.DrawItem += Lb_DrawItem;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.ForeColor = Color.White;
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(106, 15);
            label1.TabIndex = 8;
            label1.Text = "Running Processes";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.ForeColor = Color.White;
            label2.Location = new Point(408, 9);
            label2.Name = "label2";
            label2.Size = new Size(118, 15);
            label2.TabIndex = 9;
            label2.Text = "Suspended Processes";
            // 
            // PicGraph
            // 
            PicGraph.BackColor = Color.Black;
            PicGraph.BorderStyle = BorderStyle.FixedSingle;
            PicGraph.Location = new Point(12, 440);
            PicGraph.Name = "PicGraph";
            PicGraph.Size = new Size(776, 100);
            PicGraph.TabIndex = 10;
            PicGraph.TabStop = false;
            PicGraph.Paint += PicGraph_Paint;
            // 
            // GraphTimer
            // 
            GraphTimer.Enabled = true;
            GraphTimer.Interval = 1000;
            GraphTimer.Tick += GraphTimer_Tick;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.ForeColor = Color.White;
            label3.Location = new Point(12, 422);
            label3.Name = "label3";
            label3.Size = new Size(125, 15);
            label3.TabIndex = 11;
            label3.Text = "System Memory Usage";
            // 
            // TrayIcon
            // 
            TrayIcon.ContextMenuStrip = TrayMenu;
            TrayIcon.Text = "Process Hibernator";
            TrayIcon.Visible = true;
            TrayIcon.DoubleClick += TrayIcon_DoubleClick;
            // 
            // TrayMenu
            // 
            TrayMenu.Items.AddRange(new ToolStripItem[] { MenuRestore, MenuQuit });
            TrayMenu.Name = "TrayMenu";
            TrayMenu.Size = new Size(114, 48);
            // 
            // MenuRestore
            // 
            MenuRestore.Name = "MenuRestore";
            MenuRestore.Size = new Size(113, 22);
            MenuRestore.Text = "Restore";
            MenuRestore.Click += MenuRestore_Click;
            // 
            // MenuQuit
            // 
            MenuQuit.Name = "MenuQuit";
            MenuQuit.Size = new Size(113, 22);
            MenuQuit.Text = "Quit";
            MenuQuit.Click += MenuQuit_Click;
            // 
            // BtnGhostWake
            // 
            BtnGhostWake.BackColor = Color.FromArgb(80, 40, 80);
            BtnGhostWake.FlatAppearance.BorderSize = 0;
            BtnGhostWake.FlatStyle = FlatStyle.Flat;
            BtnGhostWake.ForeColor = Color.White;
            BtnGhostWake.Location = new Point(603, 342);
            BtnGhostWake.Name = "BtnGhostWake";
            BtnGhostWake.Size = new Size(185, 35);
            BtnGhostWake.TabIndex = 12;
            BtnGhostWake.Text = "Wake Ghost (Sandbox) ↑";
            BtnGhostWake.UseVisualStyleBackColor = false;
            BtnGhostWake.Click += BtnGhostWake_Click;
            // 
            // BtnKill
            // 
            BtnKill.BackColor = Color.FromArgb(100, 30, 30);
            BtnKill.FlatAppearance.BorderSize = 0;
            BtnKill.FlatStyle = FlatStyle.Flat;
            BtnKill.ForeColor = Color.White;
            BtnKill.Location = new Point(207, 342);
            BtnKill.Name = "BtnKill";
            BtnKill.Size = new Size(185, 35);
            BtnKill.TabIndex = 13;
            BtnKill.Text = "Kill Process ✖";
            BtnKill.UseVisualStyleBackColor = false;
            BtnKill.Click += BtnKill_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(32, 32, 32);
            ClientSize = new Size(800, 552);
            Controls.Add(BtnKill);
            Controls.Add(BtnGhostWake);
            Controls.Add(label3);
            Controls.Add(PicGraph);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(LbSuspended);
            Controls.Add(LbApps);
            Controls.Add(LblStatus);
            Controls.Add(BtnWake);
            Controls.Add(BtnHibernate);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = SystemIcons.Application;
            MaximizeBox = false;
            Name = "Form1";
            Text = "Process Hibernator";
            Resize += Form1_Resize;
            ((System.ComponentModel.ISupportInitialize)PicGraph).EndInit();
            TrayMenu.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button BtnHibernate;
        private Button BtnWake;
        private Label LblStatus;
        private ListBox LbApps;
        private ListBox LbSuspended;
        private Label label1;
        private Label label2;
        private PictureBox PicGraph;
        private System.Windows.Forms.Timer GraphTimer;
        private Label label3;
        private NotifyIcon TrayIcon;
        private ContextMenuStrip TrayMenu;
        private ToolStripMenuItem MenuRestore;
        private ToolStripMenuItem MenuQuit;
        private Button BtnGhostWake;
        private Button BtnKill;
    }
}
