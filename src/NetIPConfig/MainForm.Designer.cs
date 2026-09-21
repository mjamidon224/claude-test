#nullable disable

using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace NetIPConfig;

partial class MainForm
{
    private IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new Container();

        lblAdapter = new Label();
        cmbAdapters = new ComboBox();
        btnRefresh = new Button();

        grpCurrent = new GroupBox();
        lblCurStatus = new Label();
        lblCurStatusValue = new Label();
        lblCurSource = new Label();
        lblCurSourceValue = new Label();
        lblCurMac = new Label();
        lblCurMacValue = new Label();
        lblCurAddress = new Label();
        lblCurAddressValue = new Label();
        lblCurMask = new Label();
        lblCurMaskValue = new Label();
        lblCurGateway = new Label();
        lblCurGatewayValue = new Label();
        lblCurDns = new Label();
        lblCurDnsValue = new Label();

        grpAddress = new GroupBox();
        rbDhcp = new RadioButton();
        rbStatic = new RadioButton();
        lblAddress = new Label();
        txtAddress = new TextBox();
        lblMask = new Label();
        txtMask = new TextBox();
        lblGateway = new Label();
        txtGateway = new TextBox();
        lblGatewayHint = new Label();

        grpDns = new GroupBox();
        rbDnsAutomatic = new RadioButton();
        rbDnsManual = new RadioButton();
        lblPreferredDns = new Label();
        txtPreferredDns = new TextBox();
        lblAlternateDns = new Label();
        txtAlternateDns = new TextBox();

        lblLog = new Label();
        txtLog = new TextBox();
        btnLoadCurrent = new Button();
        btnApply = new Button();
        btnClose = new Button();

        statusStrip = new StatusStrip();
        lblElevation = new ToolStripStatusLabel();
        lblRestartElevated = new ToolStripStatusLabel();

        grpCurrent.SuspendLayout();
        grpAddress.SuspendLayout();
        grpDns.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();

        // lblAdapter
        lblAdapter.AutoSize = true;
        lblAdapter.Location = new Point(12, 12);
        lblAdapter.Name = "lblAdapter";
        lblAdapter.Size = new Size(96, 15);
        lblAdapter.TabIndex = 0;
        lblAdapter.Text = "&Network adapter:";

        // cmbAdapters
        cmbAdapters.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        cmbAdapters.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbAdapters.DropDownWidth = 640;
        cmbAdapters.Location = new Point(12, 32);
        cmbAdapters.Name = "cmbAdapters";
        cmbAdapters.Size = new Size(455, 23);
        cmbAdapters.TabIndex = 1;

        // btnRefresh
        btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefresh.Location = new Point(473, 31);
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Size = new Size(95, 25);
        btnRefresh.TabIndex = 2;
        btnRefresh.Text = "&Refresh";
        btnRefresh.UseVisualStyleBackColor = true;

        // grpCurrent
        grpCurrent.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grpCurrent.Controls.Add(lblCurStatus);
        grpCurrent.Controls.Add(lblCurStatusValue);
        grpCurrent.Controls.Add(lblCurSource);
        grpCurrent.Controls.Add(lblCurSourceValue);
        grpCurrent.Controls.Add(lblCurMac);
        grpCurrent.Controls.Add(lblCurMacValue);
        grpCurrent.Controls.Add(lblCurAddress);
        grpCurrent.Controls.Add(lblCurAddressValue);
        grpCurrent.Controls.Add(lblCurMask);
        grpCurrent.Controls.Add(lblCurMaskValue);
        grpCurrent.Controls.Add(lblCurGateway);
        grpCurrent.Controls.Add(lblCurGatewayValue);
        grpCurrent.Controls.Add(lblCurDns);
        grpCurrent.Controls.Add(lblCurDnsValue);
        grpCurrent.Location = new Point(12, 66);
        grpCurrent.Name = "grpCurrent";
        grpCurrent.Size = new Size(556, 185);
        grpCurrent.TabIndex = 3;
        grpCurrent.TabStop = false;
        grpCurrent.Text = "Current configuration";

        AddCurrentRow(lblCurStatus, lblCurStatusValue, "Status:", 25);
        AddCurrentRow(lblCurSource, lblCurSourceValue, "Configured via:", 47);
        AddCurrentRow(lblCurMac, lblCurMacValue, "Physical address:", 69);
        AddCurrentRow(lblCurAddress, lblCurAddressValue, "IPv4 address:", 91);
        AddCurrentRow(lblCurMask, lblCurMaskValue, "Subnet mask:", 113);
        AddCurrentRow(lblCurGateway, lblCurGatewayValue, "Default gateway:", 135);
        AddCurrentRow(lblCurDns, lblCurDnsValue, "DNS servers:", 157);

        // grpAddress
        grpAddress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grpAddress.Controls.Add(rbDhcp);
        grpAddress.Controls.Add(rbStatic);
        grpAddress.Controls.Add(lblAddress);
        grpAddress.Controls.Add(txtAddress);
        grpAddress.Controls.Add(lblMask);
        grpAddress.Controls.Add(txtMask);
        grpAddress.Controls.Add(lblGateway);
        grpAddress.Controls.Add(txtGateway);
        grpAddress.Controls.Add(lblGatewayHint);
        grpAddress.Location = new Point(12, 261);
        grpAddress.Name = "grpAddress";
        grpAddress.Size = new Size(556, 182);
        grpAddress.TabIndex = 4;
        grpAddress.TabStop = false;
        grpAddress.Text = "IPv4 address";

        // rbDhcp
        rbDhcp.AutoSize = true;
        rbDhcp.Location = new Point(16, 26);
        rbDhcp.Name = "rbDhcp";
        rbDhcp.Size = new Size(280, 19);
        rbDhcp.TabIndex = 0;
        rbDhcp.Text = "Obtain an IP address automatically (&DHCP)";
        rbDhcp.UseVisualStyleBackColor = true;

        // rbStatic
        rbStatic.AutoSize = true;
        rbStatic.Location = new Point(16, 51);
        rbStatic.Name = "rbStatic";
        rbStatic.Size = new Size(200, 19);
        rbStatic.TabIndex = 1;
        rbStatic.Text = "Use the &following IP address";
        rbStatic.UseVisualStyleBackColor = true;

        AddFieldRow(lblAddress, txtAddress, "IP address:", 81, 2);
        AddFieldRow(lblMask, txtMask, "Subnet mask:", 111, 3);
        AddFieldRow(lblGateway, txtGateway, "Default gateway:", 141, 4);

        // lblGatewayHint
        lblGatewayHint.AutoSize = true;
        lblGatewayHint.ForeColor = SystemColors.GrayText;
        lblGatewayHint.Location = new Point(360, 144);
        lblGatewayHint.Name = "lblGatewayHint";
        lblGatewayHint.Size = new Size(120, 15);
        lblGatewayHint.TabIndex = 5;
        lblGatewayHint.Text = "leave blank for none";

        // grpDns
        grpDns.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grpDns.Controls.Add(rbDnsAutomatic);
        grpDns.Controls.Add(rbDnsManual);
        grpDns.Controls.Add(lblPreferredDns);
        grpDns.Controls.Add(txtPreferredDns);
        grpDns.Controls.Add(lblAlternateDns);
        grpDns.Controls.Add(txtAlternateDns);
        grpDns.Location = new Point(12, 453);
        grpDns.Name = "grpDns";
        grpDns.Size = new Size(556, 152);
        grpDns.TabIndex = 5;
        grpDns.TabStop = false;
        grpDns.Text = "DNS servers";

        // rbDnsAutomatic
        rbDnsAutomatic.AutoSize = true;
        rbDnsAutomatic.Location = new Point(16, 26);
        rbDnsAutomatic.Name = "rbDnsAutomatic";
        rbDnsAutomatic.Size = new Size(300, 19);
        rbDnsAutomatic.TabIndex = 0;
        rbDnsAutomatic.Text = "Obtain DNS server addresses &automatically";
        rbDnsAutomatic.UseVisualStyleBackColor = true;

        // rbDnsManual
        rbDnsManual.AutoSize = true;
        rbDnsManual.Location = new Point(16, 51);
        rbDnsManual.Name = "rbDnsManual";
        rbDnsManual.Size = new Size(260, 19);
        rbDnsManual.TabIndex = 1;
        rbDnsManual.Text = "Use the follo&wing DNS server addresses";
        rbDnsManual.UseVisualStyleBackColor = true;

        AddFieldRow(lblPreferredDns, txtPreferredDns, "Preferred DNS server:", 81, 2);
        AddFieldRow(lblAlternateDns, txtAlternateDns, "Alternate DNS server:", 111, 3);

        // lblLog
        lblLog.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        lblLog.AutoSize = true;
        lblLog.Location = new Point(12, 615);
        lblLog.Name = "lblLog";
        lblLog.Size = new Size(80, 15);
        lblLog.TabIndex = 6;
        lblLog.Text = "Activity log:";

        // txtLog
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtLog.BackColor = SystemColors.Window;
        txtLog.Font = new Font("Consolas", 8.5F);
        txtLog.Location = new Point(12, 634);
        txtLog.Multiline = true;
        txtLog.Name = "txtLog";
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.Size = new Size(556, 94);
        txtLog.TabIndex = 7;
        txtLog.TabStop = false;
        txtLog.WordWrap = false;

        // btnLoadCurrent
        btnLoadCurrent.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnLoadCurrent.Location = new Point(12, 738);
        btnLoadCurrent.Name = "btnLoadCurrent";
        btnLoadCurrent.Size = new Size(180, 30);
        btnLoadCurrent.TabIndex = 8;
        btnLoadCurrent.Text = "&Load current settings";
        btnLoadCurrent.UseVisualStyleBackColor = true;

        // btnApply
        btnApply.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnApply.Location = new Point(356, 738);
        btnApply.Name = "btnApply";
        btnApply.Size = new Size(100, 30);
        btnApply.TabIndex = 9;
        btnApply.Text = "&Apply";
        btnApply.UseVisualStyleBackColor = true;

        // btnClose
        btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnClose.DialogResult = DialogResult.Cancel;
        btnClose.Location = new Point(468, 738);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(100, 30);
        btnClose.TabIndex = 10;
        btnClose.Text = "&Close";
        btnClose.UseVisualStyleBackColor = true;

        // statusStrip
        statusStrip.Items.Add(lblElevation);
        statusStrip.Items.Add(lblRestartElevated);
        statusStrip.Location = new Point(0, 780);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(580, 22);
        statusStrip.SizingGrip = false;
        statusStrip.TabIndex = 11;

        lblElevation.Name = "lblElevation";
        lblElevation.Text = "";

        lblRestartElevated.IsLink = true;
        lblRestartElevated.Name = "lblRestartElevated";
        lblRestartElevated.Text = "Restart as administrator";
        lblRestartElevated.Visible = false;

        // MainForm
        AcceptButton = btnApply;
        CancelButton = btnClose;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(580, 802);
        Controls.Add(lblAdapter);
        Controls.Add(cmbAdapters);
        Controls.Add(btnRefresh);
        Controls.Add(grpCurrent);
        Controls.Add(grpAddress);
        Controls.Add(grpDns);
        Controls.Add(lblLog);
        Controls.Add(txtLog);
        Controls.Add(btnLoadCurrent);
        Controls.Add(btnApply);
        Controls.Add(btnClose);
        Controls.Add(statusStrip);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "IPv4 Settings Manager";

        statusStrip.ResumeLayout(false);
        grpDns.ResumeLayout(false);
        grpAddress.ResumeLayout(false);
        grpCurrent.ResumeLayout(false);
        ResumeLayout(false);
    }

    /// <summary>Caption plus value label for one read-only row in the summary box.</summary>
    private static void AddCurrentRow(Label caption, Label value, string text, int top)
    {
        caption.AutoSize = true;
        caption.Location = new Point(14, top);
        caption.Text = text;
        caption.TabStop = false;

        value.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        value.AutoEllipsis = true;
        value.Location = new Point(140, top);
        value.Size = new Size(404, 17);
        value.Text = "—";
        value.TabStop = false;
    }

    /// <summary>Caption plus entry box for one editable row.</summary>
    private static void AddFieldRow(Label caption, TextBox box, string text, int top, int tabIndex)
    {
        caption.AutoSize = true;
        caption.Location = new Point(36, top + 3);
        caption.Text = text;
        caption.TabStop = false;

        box.Location = new Point(180, top);
        box.MaxLength = 15;
        box.Name = "txt" + text.Replace(":", string.Empty).Replace(" ", string.Empty);
        box.Size = new Size(170, 23);
        box.TabIndex = tabIndex;
    }

    private Label lblAdapter;
    private ComboBox cmbAdapters;
    private Button btnRefresh;

    private GroupBox grpCurrent;
    private Label lblCurStatus;
    private Label lblCurStatusValue;
    private Label lblCurSource;
    private Label lblCurSourceValue;
    private Label lblCurMac;
    private Label lblCurMacValue;
    private Label lblCurAddress;
    private Label lblCurAddressValue;
    private Label lblCurMask;
    private Label lblCurMaskValue;
    private Label lblCurGateway;
    private Label lblCurGatewayValue;
    private Label lblCurDns;
    private Label lblCurDnsValue;

    private GroupBox grpAddress;
    private RadioButton rbDhcp;
    private RadioButton rbStatic;
    private Label lblAddress;
    private TextBox txtAddress;
    private Label lblMask;
    private TextBox txtMask;
    private Label lblGateway;
    private TextBox txtGateway;
    private Label lblGatewayHint;

    private GroupBox grpDns;
    private RadioButton rbDnsAutomatic;
    private RadioButton rbDnsManual;
    private Label lblPreferredDns;
    private TextBox txtPreferredDns;
    private Label lblAlternateDns;
    private TextBox txtAlternateDns;

    private Label lblLog;
    private TextBox txtLog;
    private Button btnLoadCurrent;
    private Button btnApply;
    private Button btnClose;

    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblElevation;
    private ToolStripStatusLabel lblRestartElevated;
}
