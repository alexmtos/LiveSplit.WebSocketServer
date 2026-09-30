namespace LiveSplit.UI.Components;

partial class Settings
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

    #region Component Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
        this.chkAutoStart = new System.Windows.Forms.CheckBox();
        this.lblPort = new System.Windows.Forms.Label();
        this.txtPort = new System.Windows.Forms.TextBox();
        this.lblBindMode = new System.Windows.Forms.Label();
        this.cmbBindMode = new System.Windows.Forms.ComboBox();
        this.lblLocalIPCaption = new System.Windows.Forms.Label();
        this.lblLocalIP = new System.Windows.Forms.Label();
        this.lblLocalIPNote = new System.Windows.Forms.Label();
        this.lblToken = new System.Windows.Forms.Label();
        this.tokenPanel = new System.Windows.Forms.TableLayoutPanel();
        this.txtToken = new System.Windows.Forms.TextBox();
        this.btnGenerateToken = new System.Windows.Forms.Button();
        this.lblOrigins = new System.Windows.Forms.Label();
        this.txtOrigins = new System.Windows.Forms.TextBox();
        this.lblRefreshInterval = new System.Windows.Forms.Label();
        this.numRefreshInterval = new System.Windows.Forms.NumericUpDown();
        this.chkReadOnly = new System.Windows.Forms.CheckBox();
        this.chkAllowFileCommands = new System.Windows.Forms.CheckBox();
        this.lblConnectionUrl = new System.Windows.Forms.Label();
        this.txtConnectionUrl = new System.Windows.Forms.TextBox();
        this.lblRestartNote = new System.Windows.Forms.Label();
        this.tableLayoutPanel1.SuspendLayout();
        this.tokenPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.numRefreshInterval)).BeginInit();
        this.SuspendLayout();
        //
        // tableLayoutPanel1
        //
        this.tableLayoutPanel1.AutoSize = true;
        this.tableLayoutPanel1.ColumnCount = 2;
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.tableLayoutPanel1.Controls.Add(this.chkAutoStart, 0, 0);
        this.tableLayoutPanel1.Controls.Add(this.lblPort, 0, 1);
        this.tableLayoutPanel1.Controls.Add(this.txtPort, 1, 1);
        this.tableLayoutPanel1.Controls.Add(this.lblBindMode, 0, 2);
        this.tableLayoutPanel1.Controls.Add(this.cmbBindMode, 1, 2);
        this.tableLayoutPanel1.Controls.Add(this.lblLocalIPCaption, 0, 3);
        this.tableLayoutPanel1.Controls.Add(this.lblLocalIP, 1, 3);
        this.tableLayoutPanel1.Controls.Add(this.lblLocalIPNote, 1, 4);
        this.tableLayoutPanel1.Controls.Add(this.lblToken, 0, 5);
        this.tableLayoutPanel1.Controls.Add(this.tokenPanel, 1, 5);
        this.tableLayoutPanel1.Controls.Add(this.lblOrigins, 0, 6);
        this.tableLayoutPanel1.Controls.Add(this.txtOrigins, 1, 6);
        this.tableLayoutPanel1.Controls.Add(this.lblRefreshInterval, 0, 7);
        this.tableLayoutPanel1.Controls.Add(this.numRefreshInterval, 1, 7);
        this.tableLayoutPanel1.Controls.Add(this.chkReadOnly, 0, 8);
        this.tableLayoutPanel1.Controls.Add(this.chkAllowFileCommands, 0, 9);
        this.tableLayoutPanel1.Controls.Add(this.lblConnectionUrl, 0, 10);
        this.tableLayoutPanel1.Controls.Add(this.txtConnectionUrl, 1, 10);
        this.tableLayoutPanel1.Controls.Add(this.lblRestartNote, 0, 11);
        this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
        this.tableLayoutPanel1.Location = new System.Drawing.Point(7, 7);
        this.tableLayoutPanel1.Name = "tableLayoutPanel1";
        this.tableLayoutPanel1.RowCount = 12;
        for (int i = 0; i < 12; i++)
        {
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        }

        this.tableLayoutPanel1.Size = new System.Drawing.Size(462, 400);
        this.tableLayoutPanel1.TabIndex = 0;
        //
        // chkAutoStart
        //
        this.chkAutoStart.AutoSize = true;
        this.tableLayoutPanel1.SetColumnSpan(this.chkAutoStart, 2);
        this.chkAutoStart.Margin = new System.Windows.Forms.Padding(7, 3, 3, 3);
        this.chkAutoStart.Name = "chkAutoStart";
        this.chkAutoStart.TabIndex = 0;
        this.chkAutoStart.Text = "Start the server automatically when the layout is loaded";
        this.chkAutoStart.UseVisualStyleBackColor = true;
        //
        // lblPort
        //
        this.lblPort.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.lblPort.AutoSize = true;
        this.lblPort.Name = "lblPort";
        this.lblPort.Text = "Port:";
        //
        // txtPort
        //
        this.txtPort.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
        this.txtPort.Name = "txtPort";
        this.txtPort.TabIndex = 1;
        //
        // lblBindMode
        //
        this.lblBindMode.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.lblBindMode.AutoSize = true;
        this.lblBindMode.Name = "lblBindMode";
        this.lblBindMode.Text = "Accept connections from:";
        //
        // cmbBindMode
        //
        this.cmbBindMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
        this.cmbBindMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbBindMode.FormattingEnabled = true;
        this.cmbBindMode.Name = "cmbBindMode";
        this.cmbBindMode.TabIndex = 2;
        //
        // lblLocalIPCaption
        //
        this.lblLocalIPCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.lblLocalIPCaption.AutoSize = true;
        this.lblLocalIPCaption.Name = "lblLocalIPCaption";
        this.lblLocalIPCaption.Text = "Local IP:";
        //
        // lblLocalIP
        //
        this.lblLocalIP.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.lblLocalIP.AutoSize = true;
        this.lblLocalIP.Name = "lblLocalIP";
        this.lblLocalIP.Text = "IP Address";
        //
        // lblLocalIPNote
        //
        this.lblLocalIPNote.AutoSize = true;
        this.lblLocalIPNote.MaximumSize = new System.Drawing.Size(300, 0);
        this.lblLocalIPNote.Name = "lblLocalIPNote";
        this.lblLocalIPNote.Text = "This is NOT your public IP. With most network setups, only devices on your network can use this address to find your computer.";
        //
        // lblToken
        //
        this.lblToken.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.lblToken.AutoSize = true;
        this.lblToken.Name = "lblToken";
        this.lblToken.Text = "Token (optional):";
        //
        // tokenPanel
        //
        this.tokenPanel.AutoSize = true;
        this.tokenPanel.ColumnCount = 2;
        this.tokenPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.tokenPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
        this.tokenPanel.Controls.Add(this.txtToken, 0, 0);
        this.tokenPanel.Controls.Add(this.btnGenerateToken, 1, 0);
        this.tokenPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tokenPanel.Margin = new System.Windows.Forms.Padding(0);
        this.tokenPanel.Name = "tokenPanel";
        this.tokenPanel.RowCount = 1;
        this.tokenPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.tokenPanel.TabIndex = 3;
        //
        // txtToken
        //
        this.txtToken.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
        this.txtToken.Name = "txtToken";
        this.txtToken.TabIndex = 0;
        //
        // btnGenerateToken
        //
        this.btnGenerateToken.AutoSize = true;
        this.btnGenerateToken.Name = "btnGenerateToken";
        this.btnGenerateToken.TabIndex = 1;
        this.btnGenerateToken.Text = "Generate";
        this.btnGenerateToken.UseVisualStyleBackColor = true;
        //
        // lblOrigins
        //
        this.lblOrigins.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.lblOrigins.AutoSize = true;
        this.lblOrigins.Name = "lblOrigins";
        this.lblOrigins.Text = "Allowed web origins:";
        //
        // txtOrigins
        //
        this.txtOrigins.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
        this.txtOrigins.Name = "txtOrigins";
        this.txtOrigins.TabIndex = 4;
        //
        // lblRefreshInterval
        //
        this.lblRefreshInterval.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.lblRefreshInterval.AutoSize = true;
        this.lblRefreshInterval.Name = "lblRefreshInterval";
        this.lblRefreshInterval.Text = "Resend state every (s):";
        //
        // numRefreshInterval
        //
        this.numRefreshInterval.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.numRefreshInterval.Maximum = new decimal(new int[] { 3600, 0, 0, 0 });
        this.numRefreshInterval.Name = "numRefreshInterval";
        this.numRefreshInterval.Size = new System.Drawing.Size(80, 20);
        this.numRefreshInterval.TabIndex = 5;
        //
        // chkReadOnly
        //
        this.chkReadOnly.AutoSize = true;
        this.tableLayoutPanel1.SetColumnSpan(this.chkReadOnly, 2);
        this.chkReadOnly.Margin = new System.Windows.Forms.Padding(7, 3, 3, 3);
        this.chkReadOnly.Name = "chkReadOnly";
        this.chkReadOnly.TabIndex = 6;
        this.chkReadOnly.Text = "Read only (clients can read the state but not control LiveSplit)";
        this.chkReadOnly.UseVisualStyleBackColor = true;
        //
        // chkAllowFileCommands
        //
        this.chkAllowFileCommands.AutoSize = true;
        this.tableLayoutPanel1.SetColumnSpan(this.chkAllowFileCommands, 2);
        this.chkAllowFileCommands.Margin = new System.Windows.Forms.Padding(7, 3, 3, 3);
        this.chkAllowFileCommands.Name = "chkAllowFileCommands";
        this.chkAllowFileCommands.TabIndex = 7;
        this.chkAllowFileCommands.Text = "Allow clients to save and open splits, layouts and screenshots";
        this.chkAllowFileCommands.UseVisualStyleBackColor = true;
        //
        // lblConnectionUrl
        //
        this.lblConnectionUrl.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.lblConnectionUrl.AutoSize = true;
        this.lblConnectionUrl.Name = "lblConnectionUrl";
        this.lblConnectionUrl.Text = "Connect with:";
        //
        // txtConnectionUrl
        //
        this.txtConnectionUrl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
        this.txtConnectionUrl.Name = "txtConnectionUrl";
        this.txtConnectionUrl.ReadOnly = true;
        this.txtConnectionUrl.TabIndex = 8;
        //
        // lblRestartNote
        //
        this.lblRestartNote.AutoSize = true;
        this.tableLayoutPanel1.SetColumnSpan(this.lblRestartNote, 2);
        this.lblRestartNote.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
        this.lblRestartNote.MaximumSize = new System.Drawing.Size(450, 0);
        this.lblRestartNote.Name = "lblRestartNote";
        this.lblRestartNote.Text = "Changes to the port, the network access and the resend interval apply the next time the server starts.";
        //
        // Settings
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.Controls.Add(this.tableLayoutPanel1);
        this.Name = "Settings";
        this.Padding = new System.Windows.Forms.Padding(7);
        this.Size = new System.Drawing.Size(476, 420);
        this.tableLayoutPanel1.ResumeLayout(false);
        this.tableLayoutPanel1.PerformLayout();
        this.tokenPanel.ResumeLayout(false);
        this.tokenPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this.numRefreshInterval)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    private System.Windows.Forms.CheckBox chkAutoStart;
    private System.Windows.Forms.Label lblPort;
    private System.Windows.Forms.TextBox txtPort;
    private System.Windows.Forms.Label lblBindMode;
    private System.Windows.Forms.ComboBox cmbBindMode;
    private System.Windows.Forms.Label lblLocalIPCaption;
    private System.Windows.Forms.Label lblLocalIP;
    private System.Windows.Forms.Label lblLocalIPNote;
    private System.Windows.Forms.Label lblToken;
    private System.Windows.Forms.TableLayoutPanel tokenPanel;
    private System.Windows.Forms.TextBox txtToken;
    private System.Windows.Forms.Button btnGenerateToken;
    private System.Windows.Forms.Label lblOrigins;
    private System.Windows.Forms.TextBox txtOrigins;
    private System.Windows.Forms.Label lblRefreshInterval;
    private System.Windows.Forms.NumericUpDown numRefreshInterval;
    private System.Windows.Forms.CheckBox chkReadOnly;
    private System.Windows.Forms.CheckBox chkAllowFileCommands;
    private System.Windows.Forms.Label lblConnectionUrl;
    private System.Windows.Forms.TextBox txtConnectionUrl;
    private System.Windows.Forms.Label lblRestartNote;
}
