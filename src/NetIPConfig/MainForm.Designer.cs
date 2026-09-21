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
        btnTheme = new Button();

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

        grpProfiles = new GroupBox();
        cmbProfiles = new ComboBox();
        btnProfileSave = new Button();
        btnProfileDelete = new Button();

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
        btnScan = new Button();
        btnApply = new Button();
        btnClose = new Button();

        pnlContent = new Panel();
        pnlBottom = new Panel();
        pnlStatus = new Panel();
        lblElevation = new Label();
        lnkRestartElevated = new LinkLabel();

        grpCurrent.SuspendLayout();
        grpProfiles.SuspendLayout();
        grpAddress.SuspendLayout();
        grpDns.SuspendLayout();
        pnlContent.SuspendLayout();
        pnlBottom.SuspendLayout();
        pnlStatus.SuspendLayout();
        SuspendLayout();

        // lblAdapter
        lblAdapter.AutoSize = true;
        lblAdapter.Location = new Point(12, 12);
        lblAdapter.Name = "lblAdapter";
        lblAdapter.TabIndex = 0;
        lblAdapter.Text = "&Network adapter:";

        // btnTheme
        btnTheme.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnTheme.Location = new Point(448, 7);
        btnTheme.Name = "btnTheme";
        btnTheme.Size = new Size(120, 26);
        btnTheme.TabIndex = 1;
        btnTheme.Text = "Dark &mode";
        btnTheme.UseVisualStyleBackColor = true;

        // cmbAdapters
        cmbAdapters.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        cmbAdapters.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbAdapters.DropDownWidth = 640;
        cmbAdapters.Location = new Point(12, 39);
        cmbAdapters.Name = "cmbAdapters";
        cmbAdapters.Size = new Size(455, 23);
        cmbAdapters.TabIndex = 2;

        // btnRefresh
        btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefresh.Location = new Point(473, 38);
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Size = new Size(95, 25);
        btnRefresh.TabIndex = 3;
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
        grpCurrent.Location = new Point(12, 73);
        grpCurrent.Name = "grpCurrent";
        grpCurrent.Size = new Size(556, 185);
        grpCurrent.TabIndex = 4;
        grpCurrent.TabStop = false;
        grpCurrent.Text = "Current configuration";

        AddCurrentRow(lblCurStatus, lblCurStatusValue, "Status:", 25);
        AddCurrentRow(lblCurSource, lblCurSourceValue, "Configured via:", 47);
        AddCurrentRow(lblCurMac, lblCurMacValue, "Physical address:", 69);
        AddCurrentRow(lblCurAddress, lblCurAddressValue, "IPv4 address:", 91);
        AddCurrentRow(lblCurMask, lblCurMaskValue, "Subnet mask:", 113);
        AddCurrentRow(lblCurGateway, lblCurGatewayValue, "Default gateway:", 135);
        AddCurrentRow(lblCurDns, lblCurDnsValue, "DNS servers:", 157);

        // grpProfiles
        grpProfiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grpProfiles.Controls.Add(cmbProfiles);
        grpProfiles.Controls.Add(btnProfileSave);
        grpProfiles.Controls.Add(btnProfileDelete);
        grpProfiles.Location = new Point(12, 266);
        grpProfiles.Name = "grpProfiles";
        grpProfiles.Size = new Size(556, 66);
        grpProfiles.TabIndex = 5;
        grpProfiles.TabStop = false;
        grpProfiles.Text = "Saved profiles";

        // cmbProfiles
        cmbProfiles.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        cmbProfiles.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbProfiles.DropDownWidth = 400;
        cmbProfiles.Location = new Point(16, 26);
        cmbProfiles.Name = "cmbProfiles";
        cmbProfiles.Size = new Size(280, 23);
        cmbProfiles.TabIndex = 0;

        // btnProfileSave
        btnProfileSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnProfileSave.Location = new Point(304, 25);
        btnProfileSave.Name = "btnProfileSave";
        btnProfileSave.Size = new Size(140, 25);
        btnProfileSave.TabIndex = 1;
        btnProfileSave.Text = "Save as pro&file...";
        btnProfileSave.UseVisualStyleBackColor = true;

        // btnProfileDelete
        btnProfileDelete.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnProfileDelete.Location = new Point(452, 25);
        btnProfileDelete.Name = "btnProfileDelete";
        btnProfileDelete.Size = new Size(88, 25);
        btnProfileDelete.TabIndex = 2;
        btnProfileDelete.Text = "De&lete";
        btnProfileDelete.UseVisualStyleBackColor = true;

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
        grpAddress.Location = new Point(12, 340);
        grpAddress.Name = "grpAddress";
        grpAddress.Size = new Size(556, 182);
        grpAddress.TabIndex = 6;
        grpAddress.TabStop = false;
        grpAddress.Text = "IPv4 address";

        // rbDhcp
        rbDhcp.AutoSize = true;
        rbDhcp.Location = new Point(16, 26);
        rbDhcp.Name = "rbDhcp";
        rbDhcp.TabIndex = 0;
        rbDhcp.Text = "Obtain an IP address automatically (&DHCP)";
        rbDhcp.UseVisualStyleBackColor = true;

        // rbStatic
        rbStatic.AutoSize = true;
        rbStatic.Location = new Point(16, 51);
        rbStatic.Name = "rbStatic";
        rbStatic.TabIndex = 1;
        rbStatic.Text = "Use the &following IP address";
        rbStatic.UseVisualStyleBackColor = true;

        AddFieldRow(lblAddress, txtAddress, "IP address:", 81, 2);
        AddFieldRow(lblMask, txtMask, "Subnet mask:", 111, 3);
        AddFieldRow(lblGateway, txtGateway, "Default gateway:", 141, 4);

        // lblGatewayHint
        lblGatewayHint.AutoSize = true;
        lblGatewayHint.Location = new Point(360, 144);
        lblGatewayHint.Name = "lblGatewayHint";
        lblGatewayHint.Tag = Theme.DimTag;
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
        grpDns.Location = new Point(12, 530);
        grpDns.Name = "grpDns";
        grpDns.Size = new Size(556, 152);
        grpDns.TabIndex = 7;
        grpDns.TabStop = false;
        grpDns.Text = "DNS servers";

        // rbDnsAutomatic
        rbDnsAutomatic.AutoSize = true;
        rbDnsAutomatic.Location = new Point(16, 26);
        rbDnsAutomatic.Name = "rbDnsAutomatic";
        rbDnsAutomatic.TabIndex = 0;
        rbDnsAutomatic.Text = "Obtain DNS server addresses &automatically";
        rbDnsAutomatic.UseVisualStyleBackColor = true;

        // rbDnsManual
        rbDnsManual.AutoSize = true;
        rbDnsManual.Location = new Point(16, 51);
        rbDnsManual.Name = "rbDnsManual";
        rbDnsManual.TabIndex = 1;
        rbDnsManual.Text = "Use the follo&wing DNS server addresses";
        rbDnsManual.UseVisualStyleBackColor = true;

        AddFieldRow(lblPreferredDns, txtPreferredDns, "Preferred DNS server:", 81, 2);
        AddFieldRow(lblAlternateDns, txtAlternateDns, "Alternate DNS server:", 111, 3);

        // lblLog
        lblLog.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        lblLog.AutoSize = true;
        lblLog.Location = new Point(12, 692);
        lblLog.Name = "lblLog";
        lblLog.TabIndex = 8;
        lblLog.Text = "Activity log:";

        // txtLog
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtLog.Font = new Font("Consolas", 8.5F);
        txtLog.Location = new Point(12, 711);
        txtLog.Multiline = true;
        txtLog.Name = "txtLog";
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.Size = new Size(556, 74);
        txtLog.TabIndex = 9;
        txtLog.TabStop = false;
        txtLog.WordWrap = false;

        // btnLoadCurrent
        btnLoadCurrent.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        btnLoadCurrent.Location = new Point(12, 7);
        btnLoadCurrent.Name = "btnLoadCurrent";
        btnLoadCurrent.Size = new Size(180, 30);
        btnLoadCurrent.TabIndex = 10;
        btnLoadCurrent.Text = "&Load current settings";
        btnLoadCurrent.UseVisualStyleBackColor = true;

        // btnScan
        btnScan.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        btnScan.Location = new Point(200, 7);
        btnScan.Name = "btnScan";
        btnScan.Size = new Size(150, 30);
        btnScan.TabIndex = 11;
        btnScan.Text = "&Scan network...";
        btnScan.UseVisualStyleBackColor = true;

        // btnApply
        btnApply.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnApply.Location = new Point(356, 7);
        btnApply.Name = "btnApply";
        btnApply.Size = new Size(100, 30);
        btnApply.TabIndex = 12;
        btnApply.Text = "&Apply";
        btnApply.UseVisualStyleBackColor = true;

        // btnClose
        btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClose.DialogResult = DialogResult.Cancel;
        btnClose.Location = new Point(468, 7);
        btnClose.Name = "btnClose";
        btnClose.Size = new Size(100, 30);
        btnClose.TabIndex = 13;
        btnClose.Text = "&Close";
        btnClose.UseVisualStyleBackColor = true;

        // pnlContent — scrolls when the window is shorter than the form's natural height,
        // so the app stays usable on a 1366x768 laptop screen.
        pnlContent.AutoScroll = true;
        pnlContent.AutoScrollMinSize = new Size(500, 790);
        pnlContent.Controls.Add(lblAdapter);
        pnlContent.Controls.Add(btnTheme);
        pnlContent.Controls.Add(cmbAdapters);
        pnlContent.Controls.Add(btnRefresh);
        pnlContent.Controls.Add(grpCurrent);
        pnlContent.Controls.Add(grpProfiles);
        pnlContent.Controls.Add(grpAddress);
        pnlContent.Controls.Add(grpDns);
        pnlContent.Controls.Add(lblLog);
        pnlContent.Controls.Add(txtLog);
        pnlContent.Dock = DockStyle.Fill;
        pnlContent.Name = "pnlContent";
        pnlContent.TabIndex = 0;

        // pnlBottom — keeps the action buttons visible whatever the content does.
        pnlBottom.Controls.Add(btnLoadCurrent);
        pnlBottom.Controls.Add(btnScan);
        pnlBottom.Controls.Add(btnApply);
        pnlBottom.Controls.Add(btnClose);
        pnlBottom.Dock = DockStyle.Bottom;
        pnlBottom.Name = "pnlBottom";
        pnlBottom.Size = new Size(580, 44);
        pnlBottom.TabIndex = 1;

        // pnlStatus — a panel rather than a StatusStrip, because a StatusStrip's
        // professional renderer ignores BackColor and cannot be themed dark.
        pnlStatus.Controls.Add(lnkRestartElevated);
        pnlStatus.Controls.Add(lblElevation);
        pnlStatus.Dock = DockStyle.Bottom;
        pnlStatus.Name = "pnlStatus";
        pnlStatus.Size = new Size(580, 26);
        pnlStatus.TabIndex = 13;

        // lblElevation
        lblElevation.Dock = DockStyle.Fill;
        lblElevation.Name = "lblElevation";
        lblElevation.Padding = new Padding(10, 0, 0, 0);
        lblElevation.TabIndex = 0;
        lblElevation.Text = "";
        lblElevation.TextAlign = ContentAlignment.MiddleLeft;

        // lnkRestartElevated
        lnkRestartElevated.AutoSize = true;
        lnkRestartElevated.Dock = DockStyle.Right;
        lnkRestartElevated.Name = "lnkRestartElevated";
        lnkRestartElevated.Padding = new Padding(0, 0, 10, 0);
        lnkRestartElevated.TabIndex = 1;
        lnkRestartElevated.Text = "Restart as administrator";
        lnkRestartElevated.TextAlign = ContentAlignment.MiddleRight;
        lnkRestartElevated.Visible = false;

        // MainForm
        AcceptButton = btnApply;
        CancelButton = btnClose;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(580, 861);
        Controls.Add(pnlContent);
        Controls.Add(pnlBottom);
        Controls.Add(pnlStatus);
        MinimumSize = new Size(540, 420);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "IPv4 Settings Manager";

        pnlStatus.ResumeLayout(false);
        pnlBottom.ResumeLayout(false);
        pnlContent.ResumeLayout(false);
        grpDns.ResumeLayout(false);
        grpAddress.ResumeLayout(false);
        grpProfiles.ResumeLayout(false);
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
        box.Size = new Size(170, 23);
        box.TabIndex = tabIndex;
    }

    private Label lblAdapter;
    private ComboBox cmbAdapters;
    private Button btnRefresh;
    private Button btnTheme;

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

    private GroupBox grpProfiles;
    private ComboBox cmbProfiles;
    private Button btnProfileSave;
    private Button btnProfileDelete;

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
    private Button btnScan;
    private Button btnApply;
    private Button btnClose;

    private Panel pnlContent;
    private Panel pnlBottom;
    private Panel pnlStatus;
    private Label lblElevation;
    private LinkLabel lnkRestartElevated;
}
