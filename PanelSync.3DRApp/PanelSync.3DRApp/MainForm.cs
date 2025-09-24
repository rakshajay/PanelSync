using PanelSync.Core.Logging;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PanelSync._3DRApp
{
    public sealed class MainForm : Form
    {
        private readonly AppConfig _cfg = AppConfig.Load();
        private readonly ILog _log;

        private TextBox txtProject = new TextBox();
        private Button btnStartObjWatch = new Button();
        private RichTextBox rtb = new RichTextBox();
        private TextBox txt3drPath = new TextBox();
        private Button btnBrowse3dr = new Button();
        private Button btnExportIgesToInventor = new Button();
        private CheckBox chkManualSelection;
        private CheckBox chkExportAll;

        private InventorLogWatcher? _invLogWatcher;

        private IgesService _igesService;
        private ObjService _objService;

        private bool _updatingModeUI;

        public MainForm()
        {
            Text = "PanelSync 3DR<->Inventor";
            Width = 920; Height = 560; StartPosition = FormStartPosition.CenterScreen;

            var (_, _, _, logs, _) = HotFolders.EnsureDefaults();
            _log = new SimpleFileLogger(Path.Combine(logs, "3drapp.log"));

            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            BuildUi();
            LoadFields();
            WireEvents();
            UpdateExportModeUI();

            _invLogWatcher = new InventorLogWatcher(Append);

            _igesService = new IgesService(_cfg, _log, Append);
            _objService = new ObjService(_cfg, _log, Append);

            FormClosing += (s, e) =>
            {
                _invLogWatcher?.Dispose();
                _objService.Dispose();
            };
        }

        private void BuildUi()
        {
            Font = new Font("Segoe UI", 10F);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10),
                AutoSize = true
            };
            Controls.Add(mainLayout);

            // ---- Project Settings ----
            var grpProject = new GroupBox { Text = "Project Settings", Dock = DockStyle.Top, AutoSize = true };
            var tblProject = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
            tblProject.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            tblProject.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            tblProject.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

            void AddRow(string label, Control text, Control browse)
            {
                int r = tblProject.RowCount++;
                tblProject.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tblProject.Controls.Add(new Label { Text = label, Anchor = AnchorStyles.Left, AutoSize = true }, 0, r);
                text.Dock = DockStyle.Fill;
                tblProject.Controls.Add(text, 1, r);
                browse.Dock = DockStyle.Fill;
                tblProject.Controls.Add(browse, 2, r);
            }

            btnBrowse3dr.Text = "Browse...";
            btnBrowse3dr.Width = 100;
            AddRow("3DR file (.3dr):", txt3drPath, btnBrowse3dr);

            grpProject.Controls.Add(tblProject);
            mainLayout.Controls.Add(grpProject);

            // ---- Export Options ----
            chkManualSelection = new CheckBox();
            chkExportAll = new CheckBox();
            var grpOptions = new GroupBox { Text = "Export Options", Dock = DockStyle.Top, AutoSize = true };
            var tblOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            chkManualSelection.AutoSize = true;
            chkExportAll.AutoSize = true;
            chkManualSelection.Text = "Manual selection (visible only)";
            chkManualSelection.Checked = true;
            chkExportAll.Text = "Export all (lines, circles, polylines, multilines, planes, rectangles)";
            tblOptions.Controls.Add(chkManualSelection);
            tblOptions.Controls.Add(chkExportAll);
            grpOptions.Controls.Add(tblOptions);
            mainLayout.Controls.Add(grpOptions);

            // ---- Actions ----
            var grpActions = new GroupBox { Text = "Actions", Dock = DockStyle.Top, AutoSize = true };
            var tblActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            btnExportIgesToInventor.Text = "Export IGES → import to Inventor";
            btnExportIgesToInventor.Width = 200; btnExportIgesToInventor.Height = 32;
            btnStartObjWatch.Text = "Export OBJ → import to 3DR";
            btnStartObjWatch.Width = 260; btnStartObjWatch.Height = 32;
            tblActions.Controls.Add(btnExportIgesToInventor);
            tblActions.Controls.Add(btnStartObjWatch);
            grpActions.Controls.Add(tblActions);
            mainLayout.Controls.Add(grpActions);

            // ---- Log Console ----
            rtb.Dock = DockStyle.Fill;
            rtb.Font = new Font("Consolas", 12F);
            rtb.ReadOnly = true;
            var grpLog = new GroupBox { Text = "Log Console", Dock = DockStyle.Fill };
            grpLog.Controls.Add(rtb);
            mainLayout.Controls.Add(grpLog);

            grpProject.BackColor = Color.Transparent;
            grpOptions.BackColor = Color.Transparent;
            grpActions.BackColor = Color.Transparent;
            grpLog.BackColor = Color.Transparent;

            rtb.BackColor = Color.FromArgb(30, 0, 60);
            rtb.ForeColor = Color.Lime;

            Append("App ready.");
        }

        private void LoadFields()
        {
            txtProject.Text = _cfg.ProjectId;
            txt3drPath.Text = _cfg.ThreeDRFilePath;
        }

        private void SaveFields()
        {
            _cfg.ProjectId = txtProject.Text.Trim();
            _cfg.ThreeDRFilePath = txt3drPath.Text.Trim();
            _cfg.Save();
        }

        private void WireEvents()
        {
            btnBrowse3dr.Click += (s, e) => ChooseFile(txt3drPath, "3dr files|*.3dr|All files|*.*");

            chkExportAll.CheckedChanged += (s, e) =>
            {
                if (chkExportAll.Checked) chkManualSelection.Checked = false;
                UpdateExportModeUI();
            };
            chkManualSelection.CheckedChanged += (s, e) =>
            {
                if (chkManualSelection.Checked) chkExportAll.Checked = false;
                UpdateExportModeUI();
            };

            btnExportIgesToInventor.Click += async (s, e) =>
            {
                rtb.Clear();   // clear log console before new run
                await _igesService.ExportIgesToInventorAsync(txt3drPath.Text, chkExportAll.Checked);
            };

            btnStartObjWatch.Click += (s, e) =>
            {
                rtb.Clear();   // clear log console before new run
                _objService.ExportObjTo3DR(txt3drPath.Text);
            };

        }

        private void UpdateExportModeUI()
        {
            if (_updatingModeUI) return;
            _updatingModeUI = true;
            try
            {
                if (chkExportAll.Checked && chkManualSelection.Checked)
                    chkManualSelection.Checked = false;
                if (!chkExportAll.Checked && !chkManualSelection.Checked)
                    chkManualSelection.Checked = true;
            }
            finally { _updatingModeUI = false; }
        }

        private void ChooseFile(TextBox box, string filter)
        {
            using var dlg = new OpenFileDialog { Filter = filter, CheckFileExists = true };
            if (File.Exists(box.Text)) dlg.FileName = box.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK) { box.Text = dlg.FileName; SaveFields(); }
        }

        private void Append(string msg)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Append(msg))); return; }
            rtb.AppendText($"{DateTime.Now:HH:mm:ss} {msg}\n");
        }
    }
}
