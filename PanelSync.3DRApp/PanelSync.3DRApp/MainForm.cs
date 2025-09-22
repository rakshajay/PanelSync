using System.Drawing;
using PanelSync.Core.IO;
using PanelSync.Core.Logging;
using PanelSync.Core.Naming;
using PanelSync.Core.Services;
using System;
using System.Diagnostics; //[09/17/2025]:Raksha- For ProcessStartInfo
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PanelSync._3DRApp
{
    public sealed class MainForm : Form
    {
        private readonly AppConfig _cfg = AppConfig.Load();
        private readonly ILog _log;

        private TextBox txtProject = new TextBox();
        private TextBox txtObj = new TextBox();
        private Button btnBrowseObj = new Button();
        private Button btnStartObjWatch = new Button();
        private RichTextBox rtb = new RichTextBox();
        private TextBox txt3drPath = new TextBox();
        private Button btnBrowse3dr = new Button();
        //[09/14/2025]:Raksha- IGES UI
        private TextBox txtIges = new TextBox();
        private Button btnBrowseIges = new Button();
        private Button btnExportIgesToInventor = new Button();
        private CheckBox chkManualSelection;
        private CheckBox chkExportAll;

        private FileSystemWatcher? _objWatcher;

        //[09/17/2025]:Raksha- Guard flag to avoid recursive CheckedChanged loops
        private bool _updatingModeUI;

        public MainForm()
        {
            Text = "PanelSync 3DR<->Inventor";
            Width = 920; Height = 560; StartPosition = FormStartPosition.CenterScreen;

            var (_, _, logs, _) = HotFolders.EnsureDefaults();
            _log = new SimpleFileLogger(Path.Combine(logs, "3drapp.log"));

            // Set form icon to match application icon
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            BuildUi();
            LoadFields();
            WireEvents();
            UpdateExportModeUI(); //[09/17/2025]:Raksha- Ensure Manual default is enforced on startup
        }

        private void BuildUi()
        {
            Font = new Font("Segoe UI", 10F); //[09/17/2025]:Raksha- Modern font

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

                browse.Dock = DockStyle.Fill;  //[09/17/2025]:Raksha- ensures consistent button alignment
                tblProject.Controls.Add(browse, 2, r);
            }

            btnBrowse3dr = new Button();
            btnBrowseObj = new Button();
            btnBrowseIges = new Button();

            // Set labels 
            btnBrowse3dr.Text = "Browse...";
            btnBrowseObj.Text = "Browse...";
            btnBrowseIges.Text = "Browse...";

            int browseBtnWidth = 100;
            btnBrowse3dr.Width = browseBtnWidth;
            btnBrowseObj.Width = browseBtnWidth;
            btnBrowseIges.Width = browseBtnWidth;

            AddRow("ProjectId (Guid):", txtProject, new Label()); // no browse button
            AddRow("3DR file (.3dr):", txt3drPath, btnBrowse3dr);
            AddRow("Inventor OBJ folder:", txtObj, btnBrowseObj);
            AddRow("3DR IGES folder:", txtIges, btnBrowseIges);

            grpProject.Controls.Add(tblProject);
            mainLayout.Controls.Add(grpProject);

            // ---- Export Options ----
            chkManualSelection = new CheckBox();
            chkExportAll = new CheckBox();

            var grpOptions = new GroupBox { Text = "Export Options", Dock = DockStyle.Top, AutoSize = true };
            var tblOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            chkManualSelection.AutoSize = true;
            chkExportAll.AutoSize = true;
            chkManualSelection.Text = "Manual selection (visible only)"; chkManualSelection.Checked = true;
            chkExportAll.Text = "Export all (lines, circles, polylines, multilines, planes, rectangles)";
            tblOptions.Controls.Add(chkManualSelection);
            tblOptions.Controls.Add(chkExportAll);
            grpOptions.Controls.Add(tblOptions);
            mainLayout.Controls.Add(grpOptions);

            // ---- Actions ----
            var grpActions = new GroupBox { Text = "Actions", Dock = DockStyle.Top, AutoSize = true };
            var tblActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            btnExportIgesToInventor.Text = "Export IGES → import to Inventor"; btnExportIgesToInventor.Width = 200; btnExportIgesToInventor.Height = 32;
            btnStartObjWatch.Text = "Export OBJ → import to 3DR"; btnStartObjWatch.Width = 260; btnStartObjWatch.Height = 32;
            tblActions.Controls.Add(btnExportIgesToInventor);
            tblActions.Controls.Add(btnStartObjWatch);
            grpActions.Controls.Add(tblActions);
            mainLayout.Controls.Add(grpActions);

            // ---- Log Console ----
            rtb.Dock = DockStyle.Fill;
            rtb.Font = new Font("Consolas", 9F);
            rtb.ReadOnly = true;
            var grpLog = new GroupBox { Text = "Log Console", Dock = DockStyle.Fill };
            grpLog.Controls.Add(rtb);
            mainLayout.Controls.Add(grpLog);

            //Apply purple theme

            grpProject.BackColor = Color.Transparent;
            grpOptions.BackColor = Color.Transparent;
            grpActions.BackColor = Color.Transparent;
            grpLog.BackColor = Color.Transparent;


            rtb.BackColor = Color.FromArgb(30, 0, 60); // deep purple
            rtb.ForeColor = Color.Lime;                // green text

            Append("App ready.");
        }


        private void LoadFields()
        {
            txtProject.Text = _cfg.ProjectId;
            txtObj.Text = _cfg.InventorExportObj;
            txt3drPath.Text = _cfg.ThreeDRFilePath;
            txtIges.Text = _cfg.ThreeDRExportIges;
        }

        private void SaveFields()
        {
            _cfg.ProjectId = txtProject.Text.Trim();
            _cfg.InventorExportObj = txtObj.Text.Trim();
            _cfg.ThreeDRFilePath = txt3drPath.Text.Trim();
            _cfg.ThreeDRExportIges = txtIges.Text.Trim(); //[09/14/2025]:Raksha- Save IGES path
            _cfg.Save();
        }

        private void WireEvents()
        {
            btnBrowseObj.Click += (s, e) => ChooseFolder(txtObj);
            btnBrowse3dr.Click += (s, e) => ChooseFile(txt3drPath, "3dr files|*.3dr|All files|*.*");
            btnBrowseIges.Click += (s, e) => ChooseFolder(txtIges);

            btnExportIgesToInventor.Click += async (s, e) => await OnExportIgesToInventorAsync();

            //[09/17/2025]:Raksha- Wire both toggles; keep them mutually exclusive
            chkExportAll.CheckedChanged += chkExportAll_CheckedChanged;
            chkManualSelection.CheckedChanged += chkManualSelection_CheckedChanged;

            btnStartObjWatch.Click += (s, e) => OnExportObjTo3DR();

            FormClosing += (s, e) => { _objWatcher?.Dispose(); };
        }

        //[09/17/2025]:Raksha- Ensure only one mode checked, and if none -> Manual
        private void UpdateExportModeUI()
        {
            if (_updatingModeUI) return;
            _updatingModeUI = true;
            try
            {
                // Enforce mutual exclusivity
                if (chkExportAll.Checked && chkManualSelection.Checked)
                    chkManualSelection.Checked = false;

                if (!chkExportAll.Checked && !chkManualSelection.Checked)
                    chkManualSelection.Checked = true; //[09/17/2025]:Raksha- Default/fallback

            }
            finally { _updatingModeUI = false; }
        }

        private void chkExportAll_CheckedChanged(object? sender, EventArgs e)
        {
            //[09/17/2025]:Raksha- If user turns on ExportAll, turn off Manual
            if (chkExportAll.Checked) chkManualSelection.Checked = false;
            UpdateExportModeUI();
        }

        private void chkManualSelection_CheckedChanged(object? sender, EventArgs e)
        {
            //[09/17/2025]:Raksha- If user turns on Manual, turn off ExportAll
            if (chkManualSelection.Checked) chkExportAll.Checked = false;
            UpdateExportModeUI();
        }

        //[09/14/2025]:Raksha- Resolve IGES exporter script
        private string ResolveIgesScriptPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var p1 = Path.Combine(baseDir, "Scripts", "ExportGeometricGroupToIges.js");
            if (File.Exists(p1)) return p1;
            var p2 = Path.Combine(baseDir, "ExportGeometricGroupToIges.js");
            if (File.Exists(p2)) return p2;
            throw new FileNotFoundException("ExportGeometricGroupToIges.js not found", p1);
        }

        //[09/14/2025]:Raksha- End-to-end: export IGES then import into Inventor
        private async Task OnExportIgesToInventorAsync()
        {
            Append("opening (IGES)....");
            try
            {
                SaveFields();

                if (string.IsNullOrWhiteSpace(txt3drPath.Text) || !File.Exists(txt3drPath.Text))
                {
                    MessageBox.Show("Pick your .3dr file first.");
                    return;
                }

                var baseName = Path.GetFileNameWithoutExtension(txt3drPath.Text);
                var tmpOut = Path.Combine(Path.GetTempPath(), $"{baseName}.igs");

                byte[] igesBytes;
                try
                {
                    igesBytes = await ExportIgesFrom3DRAsync(_cfg.ThreeDRFilePath, tmpOut);
                    Append("Real 3DR IGES export succeeded.");
                    _log.Info("//[09/14/2025]:Raksha- Real 3DR IGES export succeeded");
                }
                catch (Exception ex)
                {
                    _log.Error("//[09/14/2025]:Raksha- IGES export failed", ex);
                    MessageBox.Show("IGES export failed. See log for details.");
                    return;
                }

                var exportRoot3dr = Path.GetFullPath(Path.Combine(_cfg.ThreeDRExportIges, "..", "..")); // ...\3DR\exports\iges → root
                var igesPath = Path.Combine(_cfg.ThreeDRExportIges, $"{baseName}.igs");
                await File.WriteAllBytesAsync(igesPath, igesBytes);
                Append("Wrote IGES -> " + igesPath);

                // Target IPT (same base as .3dr)
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var hotRoot = Path.Combine(desktop, "PanelSyncHot");
                var projectsDir = Path.Combine(hotRoot, "Inventor", "Projects");
                Directory.CreateDirectory(projectsDir);
                var targetIpt = Path.Combine(projectsDir, baseName + ".ipt");

                QueueInventorJob_OpenOrCreateAndImportIGES(targetIpt, igesPath, bringToFront: true);
                Append($"Queued Inventor job for [{baseName}]: If Inventor is not open, please open it to process this job");
            }
            catch (Exception ex)
            {
                _log.Error("//[09/14/2025]:Raksha- ExportIgesToInventor failed", ex);
                Append("ERROR: " + ex.Message);
            }
            await Task.CompletedTask;
        }

        //[09/17/2025]:Raksha- End-to-end: export OBJ then import into 3DR
        private void OnExportObjTo3DR()
        {

            try
            {
                SaveFields();
                StartObjWatcherOnce();

                if (string.IsNullOrWhiteSpace(txt3drPath.Text) || !File.Exists(txt3drPath.Text))
                {
                    MessageBox.Show("Pick your .3dr file first.");
                    return;
                }

                var baseName = Path.GetFileNameWithoutExtension(txt3drPath.Text);
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var hotRoot = Path.Combine(desktop, "PanelSyncHot");
                var projectsDir = Path.Combine(hotRoot, "Inventor", "Projects");
                Directory.CreateDirectory(projectsDir);
                var targetIpt = Path.Combine(projectsDir, baseName + ".ipt");

                QueueInventorJob_ExportOBJ(targetIpt, _cfg.InventorExportObj);
                Append($"//[09/17/2025]:Raksha- Queued OBJ export job for [{baseName}] → Inventor OBJ folder");
            }
            catch (Exception ex)
            {
                _log.Error("//[09/17/2025]:Raksha- ExportObjTo3DR failed", ex);
                Append("ERROR: " + ex.Message);
            }
            Append("OnExportObjTo3DR");
        }

        //[09/14/2025]:Raksha- Queue IGES import job
        private void QueueInventorJob_OpenOrCreateAndImportIGES(string iptPath, string igesPath, bool bringToFront)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var jobsDir = Path.Combine(desktop, "PanelSyncHot", "Jobs");
            Directory.CreateDirectory(jobsDir);

            var job = new
            {
                Kind = "OpenOrCreateAndImportIGES",
                IptPath = iptPath,
                IgesPath = igesPath,
                BringToFront = bringToFront,
                ProjectId = _cfg.ProjectId,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            };

            var json = JsonSerializer.Serialize(job, new JsonSerializerOptions { WriteIndented = true });
            var jobPath = Path.Combine(jobsDir, $"job_{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}_{Path.GetFileNameWithoutExtension(iptPath)}.json");
            AtomicWriter.WriteAllText(jobPath, json);
        }
        private void QueueInventorJob_ExportOBJ(string iptPath, string outFolder)
        {

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var jobsDir = Path.Combine(desktop, "PanelSyncHot", "Jobs");
            Directory.CreateDirectory(jobsDir);

            var job = new
            {
                Kind = "ExportPanelAsOBJ",
                IptPath = iptPath,
                OutFolder = outFolder,
                PanelId = "P001",
                Rev = "A",
                BringToFront = true,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            };

            var json = JsonSerializer.Serialize(job, new JsonSerializerOptions { WriteIndented = true });
            var jobPath = Path.Combine(jobsDir, $"job_{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}_OBJ.json");
            AtomicWriter.WriteAllText(jobPath, json);
        }

        private void ChooseFolder(TextBox box)
        {
            using var dlg = new FolderBrowserDialog();
            dlg.SelectedPath = Directory.Exists(box.Text) ? box.Text : Path.GetDirectoryName(box.Text) ?? "";
            if (dlg.ShowDialog(this) == DialogResult.OK) { box.Text = dlg.SelectedPath; SaveFields(); }
        }

        // -------- 3DR → Inventor (export IGS) ----------

        //[09/02/2025]:Raksha- Find Cyclone 3DR exe in common locations
        private string Get3DRExePath()
        {
            string[] candidates =
            {
                @"E:\Installed software\Cyclone 3DR\3DR.exe",
                @"C:\Program Files\Leica\Cyclone 3DR\3DR.exe",
                @"C:\Program Files\Leica Geosystems\Cyclone 3DR\3DR.exe",
                @"C:\Program Files (x86)\Leica\Cyclone 3DR\3DR.exe"
            };

            foreach (var c in candidates)
                if (File.Exists(c))
                {
                    _log.Debug("//[09/02/2025]:Raksha- 3DR exe found => " + c);
                    return c;
                }

            throw new FileNotFoundException("3DR.exe not found in known locations. Please install or update path in code.", "(auto)");
        }

        //[09/14/2025]:Raksha- Launch 3DR with IGES script and return .igs bytes
        private async Task<byte[]> ExportIgesFrom3DRAsync(string threeDrPath, string igesOutPath)
        {
            Append("threeDrPath=" + threeDrPath);
            Append("igesOutPath=" + igesOutPath);

            var exe = Get3DRExePath();
            EnsureFile(exe, "3DR.exe");

            var scriptPath = ResolveIgesScriptPath();
            EnsureFile(scriptPath, "3DR IGES script");
            EnsureFile(threeDrPath, ".3dr project");
            Directory.CreateDirectory(Path.GetDirectoryName(igesOutPath)!);
            Append("scriptPath(iges)=" + scriptPath);

            // Rebuild args with export mode flag
            string js = scriptPath.Replace("\\", "/");
            string proj = threeDrPath.Replace("\\", "/");
            string outp = igesOutPath.Replace("\\", "/");
            string log = Path.Combine(Path.GetTempPath(), $"3dr_{Guid.NewGuid():N}.log").Replace("\\", "/");

            bool exportAll = chkExportAll.Checked; //[09/17/2025]:Raksha- Read current mode
            Append("//[09/17/2025]:Raksha- Mode => " + (exportAll ? "ExportAll" : "Manual"));

            string args =
                $"--Script=\"{js}\" " +
                $"--ScriptOutput=\"{log}\" " +
                $"--Silent " +
                $"--ScriptAutorun " +
                $"--ScriptParam=\"project='{proj}'; out='{outp}'; exportAll={(exportAll ? 1 : 0)};\"";

            Append("//[09/17/2025]:Raksha- RUN 3DR (IGES) => " + args);

            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
            };

            using var p = Process.Start(psi)!;
            var soTask = p.StandardOutput.ReadToEndAsync();
            var seTask = p.StandardError.ReadToEndAsync();

            var started = DateTime.UtcNow;
            while (!p.HasExited)
            {
                if (File.Exists(igesOutPath))
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var stable = await FileStability.WaitUntilStableAsync(igesOutPath, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200), cts.Token);
                    if (stable) break;
                }
                if (DateTime.UtcNow - started > TimeSpan.FromMinutes(2)) { try { p.Kill(entireProcessTree: true); } catch { } break; }
                await Task.Delay(250);
            }

            var so = await soTask;
            var se = await seTask;

            try
            {
                if (File.Exists(log))
                {
                    var console = await File.ReadAllTextAsync(log);
                    Append("//[09/14/2025]:Raksha- 3DR IGES script console:\n" + console);
                }
            }
            catch { }

            if (!File.Exists(igesOutPath))
                throw new Exception("3DR IGES export failed (no .igs produced).");

            var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var ok = await FileStability.WaitUntilStableAsync(igesOutPath, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(250), cts2.Token);
            if (!ok) throw new IOException("IGES not stable at: " + igesOutPath);

            var (ok2, why2) = await IgesSoftValidateAsync(igesOutPath);
            if (!ok2) Append("//[09/14/2025]:Raksha- IGES soft-validate: " + why2);

            return await File.ReadAllBytesAsync(igesOutPath);
        }

        // -------- Inventor Job Queueing ----------

        //[09/02/2025]:Raksha- Verify a file exists or throw a precise error
        private void EnsureFile(string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(label + " not found", path ?? "(null)");
        }

        //[09/14/2025]:Raksha- Minimal IGES sanity (sections S/G/D/P/T + size)
        private static async Task<(bool ok, string why)> IgesSoftValidateAsync(string path, CancellationToken ct = default)
        {
            try
            {
                var len = new FileInfo(path).Length;
                if (len < 512) return (false, "too small");

                string text;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, System.Text.Encoding.ASCII, detectEncodingFromByteOrderMarks: true))
                    text = await sr.ReadToEndAsync();

                var t = text.ToUpperInvariant();
                bool hasS = t.Contains("\nS") || t.StartsWith("S");
                bool hasG = t.Contains("\nG");
                bool hasD = t.Contains("\nD");
                bool hasP = t.Contains("\nP");
                bool hasT = t.Contains("\nT");
                if (!(hasS && hasG && hasD && hasP && hasT)) return (false, "missing IGES section tags");
                return (true, "ok");
            }
            catch (Exception ex) { return (false, "exception: " + ex.Message); }
        }

        // -------- Inventor → 3DR (watch OBJ + meta, then import to 3DR) ----------
        private void StartObjWatcherOnce()
        {
            if (_objWatcher != null) return; // already running
            var folder = _cfg.InventorExportObj;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            _objWatcher = new FileSystemWatcher(folder, "*.obj")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _objWatcher.Created += async (s, e) => await OnNewObjAsync(e.FullPath);

            Append("OBJ watcher armed on " + folder);
        }

        private async Task OnNewObjAsync(string objPath)
        {
            Append("Detected OBJ -> " + objPath);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var ok = await FileStability.WaitUntilStableAsync(objPath,
                         TimeSpan.FromMilliseconds(600),
                         TimeSpan.FromMilliseconds(150),
                         cts.Token);
            if (!ok)
            {
                Append("Skipped (not stable): " + objPath);
                _log.Warn("//[09/22/2025]:Raksha- OBJ not stable in time -> " + objPath);
                return;
            }

            // Write latest_obj.js for 3DR scripting console
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var hotRoot = Path.Combine(desktop, "PanelSyncHot");
                var jsPath = Path.Combine(hotRoot, "latest_obj.js");

                var jsContent =
                    "//[09/22/2025]:Raksha- Auto-generated script\n\n" +
                    $"var objPath = \"{objPath.Replace("\\", "/")}\";\n\n" +
                    "function log(m) { try { print(m); } catch (_) { } }\n\n" +
                    "if (!objPath) { throw \"objPath is missing!\"; }\n\n" +
                    "log(\"Importing OBJ: \" + objPath);\n" +
                    "var rc = SPoly.FromFile(objPath);\n" +
                    "if (!rc || rc.ErrorCode !== 0) { throw \"SPoly.FromFile failed: \" + JSON.stringify(rc); }\n" +
                    "var mesh = rc.PolyTbl[0];\n" +
                    "mesh.AddToDoc();\n" +
                    "log(\"Added mesh: \" + mesh.GetName());\n" +
                    "try { var vs = SViewSet.New(true); vs.Update(true); } catch(_) {}\n" +
                    "SaveDoc(\"\", true);\n" +
                    "log(\"Saved project after import.\");\n";

                File.WriteAllText(jsPath, jsContent);
                Append("Updated latest_obj.js -> " + jsPath);
            }
            catch (Exception ex)
            {
                _log.Error("//[09/22/2025]:Raksha- Failed to write latest_obj.js", ex);
                Append("ERROR: Could not write latest_obj.js (" + ex.Message + ")");
            }

            var metaPath = PathRules.MetaFor(objPath);
            var metaOk = await FileStability.WaitUntilStableAsync(metaPath,
                         TimeSpan.FromMilliseconds(600),
                         TimeSpan.FromMilliseconds(150),
                         cts.Token);

            if (!metaOk) { Append("Warning: meta not stable/missing -> " + metaPath); }

            // still run import if desired
            //ImportObjInto3DR(objPath, metaPath);
        }


        private void ImportObjInto3DR(string objPath, string metaPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_cfg.ThreeDRFilePath) || !File.Exists(_cfg.ThreeDRFilePath))
                {
                    Append("3DR file path missing. Set it in Project Settings.");
                    return;
                }

                Append("Importing OBJ into 3DR -> " + objPath);

                var exe = Get3DRExePath();
                EnsureFile(exe, "3DR.exe");

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var scriptPath = Path.Combine(baseDir, "ImportObjTo3dr.js");
                EnsureFile(scriptPath, "3DR OBJ import script");
                Append("scriptPath=" + scriptPath);

                string js = scriptPath.Replace("\\", "/");
                string proj = _cfg.ThreeDRFilePath.Replace("\\", "/");
                string obj = objPath.Replace("\\", "/");
                string current = proj; // //[09/19/2025]:Raksha- Pass current open project
                string log = Path.Combine(Path.GetTempPath(), $"3dr_import_{Guid.NewGuid():N}.log").Replace("\\", "/");
                Append("Importing OBJ into 3DR project: " + proj);

                string args =
                    $"--Script=\"{js}\" " +
                    $"--ScriptOutput=\"{log}\" " +
                    $"--ScriptAutorun " +
                    $"--ScriptParam=\"project='{proj}'; obj='{obj}'; current='{current}';\"";

                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var p = Process.Start(psi)!;
                p.WaitForExit(60_000); // wait up to 1 min
                Append("➡ Imported OBJ into live 3DR session: " + objPath);

                if (File.Exists(log))
                {
                    Append("3DR script log:\n" + File.ReadAllText(log));
                }
            }
            catch (Exception ex)
            {
                _log.Error("//[09/19/2025]:Raksha- ImportObjInto3DR(GUI) failed", ex);
                Append("ERROR: OBJ import failed - " + ex.Message);
            }
        }


        private void Append(string msg)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Append(msg))); return; }
            rtb.AppendText($"{DateTime.Now:HH:mm:ss} {msg}\n");
        }

        private void ChooseFile(TextBox box, string filter)
        {
            using var dlg = new OpenFileDialog { Filter = filter, CheckFileExists = true };
            if (File.Exists(box.Text)) dlg.FileName = box.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK) { box.Text = dlg.FileName; SaveFields(); }
        }
    }
}
