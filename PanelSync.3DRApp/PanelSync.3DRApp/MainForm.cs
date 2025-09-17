using PanelSync.Core.IO;
using PanelSync.Core.Logging;
using PanelSync.Core.Naming;
using PanelSync.Core.Services;
using System;
using System.Diagnostics; //[09/02/2025]:Raksha- For ProcessStartInfo
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
        private TextBox txtZone = new TextBox();
        private TextBox txtDxf = new TextBox();
        private TextBox txtObj = new TextBox();
        private Button btnBrowseDxf = new Button();
        private Button btnBrowseObj = new Button();
        private Button btnViewInInventor = new Button();
        private Button btnStartObjWatch = new Button();
        private RichTextBox rtb = new RichTextBox();
        private TextBox txt3drPath = new TextBox();
        private Button btnBrowse3dr = new Button();
        //[09/14/2025]:Raksha- IGES UI
        private TextBox txtIges = new TextBox();
        private Button btnBrowseIges = new Button();
        private Button btnExportIgesToInventor = new Button();


        private FileSystemWatcher? _objWatcher;

        public MainForm()
        {
            Text = "PanelSync.3DRApp";
            Width = 920; Height = 560; StartPosition = FormStartPosition.CenterScreen;

            var (_, _, _, logs, _) = HotFolders.EnsureDefaults();
            _log = new SimpleFileLogger(Path.Combine(logs, "3drapp.log"));

            BuildUi();
            LoadFields();
            WireEvents();
        }

        private void BuildUi()
        {
            int y = 12;
            Controls.Add(new Label { Left = 12, Top = y + 4, Width = 120, Text = "ProjectId (Guid):" });
            txtProject.Left = 140; txtProject.Top = y; txtProject.Width = 540; Controls.Add(txtProject); y += 32;

            Controls.Add(new Label { Left = 12, Top = y + 4, Width = 120, Text = "Zone:" });
            txtZone.Left = 140; txtZone.Top = y; txtZone.Width = 160; Controls.Add(txtZone); y += 32;

            Controls.Add(new Label { Left = 12, Top = y + 4, Width = 120, Text = "3DR file (.3dr):" });
            txt3drPath.Left = 140; txt3drPath.Top = y; txt3drPath.Width = 540; Controls.Add(txt3drPath);
            btnBrowse3dr.Text = "Browse..."; btnBrowse3dr.Left = 690; btnBrowse3dr.Top = y - 2; btnBrowse3dr.Width = 100; Controls.Add(btnBrowse3dr);
            y += 32;

            Controls.Add(new Label { Left = 12, Top = y + 4, Width = 120, Text = "3DR DXF folder:" });
            txtDxf.Left = 140; txtDxf.Top = y; txtDxf.Width = 540; Controls.Add(txtDxf);
            btnBrowseDxf.Text = "Browse..."; btnBrowseDxf.Left = 690; btnBrowseDxf.Top = y - 2; btnBrowseDxf.Width = 100; Controls.Add(btnBrowseDxf);
            y += 32;

            Controls.Add(new Label { Left = 12, Top = y + 4, Width = 120, Text = "Inventor OBJ folder:" });
            txtObj.Left = 140; txtObj.Top = y; txtObj.Width = 540; Controls.Add(txtObj);
            btnBrowseObj.Text = "Browse..."; btnBrowseObj.Left = 690; btnBrowseObj.Top = y - 2; btnBrowseObj.Width = 100; Controls.Add(btnBrowseObj);
            y += 40;

            //[09/14/2025]:Raksha- 3DR IGES folder
            Controls.Add(new Label { Left = 12, Top = y + 4, Width = 120, Text = "3DR IGES folder:" });
            txtIges.Left = 140; txtIges.Top = y; txtIges.Width = 540; Controls.Add(txtIges);
            btnBrowseIges.Text = "Browse..."; btnBrowseIges.Left = 690; btnBrowseIges.Top = y - 2; btnBrowseIges.Width = 100; Controls.Add(btnBrowseIges);
            y += 40;

            // Button to run IGES export then import into Inventor
            btnExportIgesToInventor.Text = "Export IGES → Inventor";
            btnExportIgesToInventor.Left = 140; btnExportIgesToInventor.Top = y; btnExportIgesToInventor.Width = 200; Controls.Add(btnExportIgesToInventor);
            y += 8; // small spacer

            btnViewInInventor.Text = "View in Inventor";
            btnViewInInventor.Left = 140; btnViewInInventor.Top = y; btnViewInInventor.Width = 200; Controls.Add(btnViewInInventor);

            btnStartObjWatch.Text = "Start watching OBJ → import to 3DR";
            btnStartObjWatch.Left = 360; btnStartObjWatch.Top = y; btnStartObjWatch.Width = 300; Controls.Add(btnStartObjWatch);
            y += 40;

            rtb.Left = 12; rtb.Top = y; rtb.Width = 880; rtb.Height = 360; rtb.ReadOnly = true; Controls.Add(rtb);

            Append("App ready.");
        }

        private void LoadFields()
        {
            txtProject.Text = _cfg.ProjectId;
            txtZone.Text = _cfg.Zone;
            txtDxf.Text = _cfg.ThreeDRExportDxf;
            txtObj.Text = _cfg.InventorExportObj;
            txt3drPath.Text = _cfg.ThreeDRFilePath;
            txtIges.Text = _cfg.ThreeDRExportIges;

        }

        private void SaveFields()
        {
            _cfg.ProjectId = txtProject.Text.Trim();
            _cfg.Zone = txtZone.Text.Trim();
            _cfg.ThreeDRExportDxf = txtDxf.Text.Trim();
            _cfg.InventorExportObj = txtObj.Text.Trim();
            _cfg.ThreeDRFilePath = txt3drPath.Text.Trim();
            _cfg.ThreeDRExportIges = txtIges.Text.Trim(); //[09/14/2025]:Raksha- Save IGES path

            _cfg.Save();
        }

        private void WireEvents()
        {
            btnBrowseDxf.Click += (s, e) => ChooseFolder(txtDxf);
            btnBrowseObj.Click += (s, e) => ChooseFolder(txtObj);
            btnBrowse3dr.Click += (s, e) => ChooseFile(txt3drPath, "3dr files|*.3dr|All files|*.*");
            btnBrowseIges.Click += (s, e) => ChooseFolder(txtIges);
            btnExportIgesToInventor.Click += async (s, e) => await OnExportIgesToInventorAsync();

            btnViewInInventor.Click += async (s, e) => await OnViewInInventorAsync();
            btnStartObjWatch.Click += (s, e) => ToggleObjWatcher();
            FormClosing += (s, e) => { _objWatcher?.Dispose(); };
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

                var groupToExport = string.IsNullOrWhiteSpace(_cfg.Zone) ? "Geometric Group" : _cfg.Zone;
                var baseName = Path.GetFileNameWithoutExtension(txt3drPath.Text);
                var tmpOut = Path.Combine(Path.GetTempPath(), $"{baseName}.igs");

                byte[] igesBytes;
                try
                {
                    igesBytes = await ExportIgesFrom3DRAsync(_cfg.ThreeDRFilePath, groupToExport, tmpOut);
                    Append("Real 3DR IGES export succeeded.");
                    _log.Info("//[09/14/2025]:Raksha- Real 3DR IGES export succeeded");
                }
                catch (Exception ex)
                {
                    _log.Error("//[09/14/2025]:Raksha- IGES export failed", ex);
                    MessageBox.Show("IGES export failed. See log for details.");
                    return;
                }

                var guard = new GuardService(_log);
                var exporter = new ExportService(_log, guard);
                var exportRoot3dr = Path.GetFullPath(Path.Combine(_cfg.ThreeDRExportIges, "..", "..")); // ...\3DR\exports\iges → root
                var igesPath = Path.Combine(_cfg.ThreeDRExportIges, $"{baseName}.igs");
                // Save file directly
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

        //[09/14/2025]:Raksha- Queue IGES import job
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
                Zone = _cfg.Zone,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            };

            var json = JsonSerializer.Serialize(job, new JsonSerializerOptions { WriteIndented = true });
            var jobPath = Path.Combine(jobsDir, $"job_{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}_{Path.GetFileNameWithoutExtension(iptPath)}.json");
            AtomicWriter.WriteAllText(jobPath, json);
        }


        private void ChooseFolder(TextBox box)
        {
            using var dlg = new FolderBrowserDialog();
            dlg.SelectedPath = Directory.Exists(box.Text) ? box.Text : Path.GetDirectoryName(box.Text) ?? "";
            if (dlg.ShowDialog(this) == DialogResult.OK) { box.Text = dlg.SelectedPath; SaveFields(); }
        }

        // -------- 3DR → Inventor (export DXF) ----------

        //[09/02/2025]:Raksha- Resolve script path (supports both \Scripts\ and root)
        private string ResolveScriptPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var p1 = Path.Combine(baseDir, "Scripts", "ExportGeometricGroupToDxf.js");
            if (File.Exists(p1)) return p1;

            var p2 = Path.Combine(baseDir, "ExportGeometricGroupToDxf.js"); // current location in your build
            if (File.Exists(p2)) return p2;
            throw new FileNotFoundException("ExportGeometricGroupToDxf.js not found", p1);
        }

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

        //[09/02/2025]:Raksha- Async 3DR runner: succeed as soon as DXF appears & stabilizes
        //[09/08/2025]:Raksha- Always run 3DR headless with autorun and capture script console
        private async Task<(int exitCode, string stdout, string stderr)> Run3DRAsync(
            string exe, string scriptPath, string threeDrPath, string groupName, string outPath, TimeSpan timeout)
        {
            // Normalize to forward slashes (3DR script docs recommend this)
            string js = scriptPath.Replace("\\", "/");
            string proj = threeDrPath.Replace("\\", "/");
            string outp = outPath.Replace("\\", "/");
            string grp = string.IsNullOrWhiteSpace(groupName) ? "/Geometric Group" : (groupName.StartsWith("/") ? groupName : "/" + groupName);
            //[09/09/2025]:Raksha- unique script log per run
            string log = Path.Combine(Path.GetTempPath(), $"3dr_{Guid.NewGuid():N}.log").Replace("\\", "/");


            // Build the exact CLI that worked in your manual tests
            string args =
                $"--Script=\"{js}\" " +
                $"--ScriptOutput=\"{log}\" " +
                $"--Silent " +
                $"--ScriptAutorun " +
                $"--ScriptParam=\"project='{proj}'; out='{outp}'; groupPath='{grp}'; filterTypes='SLine,SCircle,SPolyline';\"";

            Append("//[09/08/2025]:Raksha- RUN 3DR => " + args);

            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // Use the app folder as WD to avoid any doc-relative oddities
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
            };

            using var p = Process.Start(psi)!;
            var soTask = p.StandardOutput.ReadToEndAsync();
            var seTask = p.StandardError.ReadToEndAsync();

            var started = DateTime.UtcNow;
            while (!p.HasExited)
            {
                if (File.Exists(outPath))
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var stable = await FileStability.WaitUntilStableAsync(outPath, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(200), cts.Token);
                    if (stable) break;
                }
                if (DateTime.UtcNow - started > timeout) { try { p.Kill(entireProcessTree: true); } catch { } break; }
                await Task.Delay(250);
            }

            var so = await soTask;
            var se = await seTask;

            // Also pull the script console so we can compare with your CLI run
            try
            {
                if (File.Exists(log))
                {
                    var console = await File.ReadAllTextAsync(log);
                    Append("//[09/08/2025]:Raksha- 3DR script console:\n" + console);
                }
            }
            catch { /* best effort */ }

            return (p.ExitCode, so, se);
        }


        //[09/02/2025]:Raksha- Launch Cyclone 3DR and return DXF bytes (async, non-blocking)
        private async Task<byte[]> ExportDxfFrom3DRAsync(string threeDrPath, string groupName, string dxfOutPath)
        {
            Append("threeDrPath=" + threeDrPath);
            Append("groupName=" + groupName);
            Append("dxfOutPath=" + dxfOutPath);

            var exe = Get3DRExePath(); //[09/02/2025]:Raksha- Robust exe resolver
            EnsureFile(exe, "3DR.exe");

            var scriptPath = ResolveScriptPath();
            EnsureFile(scriptPath, "3DR export script");
            EnsureFile(threeDrPath, ".3dr project");
            Directory.CreateDirectory(Path.GetDirectoryName(dxfOutPath)!);
            Append("scriptPath=" + scriptPath);
            var r = await Run3DRAsync(exe, scriptPath, threeDrPath, groupName, dxfOutPath, TimeSpan.FromMinutes(2));
            Append($"3DR exit={r.exitCode}");
            if (!string.IsNullOrWhiteSpace(r.stdout)) Append("3DR stdout:\n" + r.stdout);
            if (!string.IsNullOrWhiteSpace(r.stderr)) Append("3DR stderr:\n" + r.stderr);

            // If no DXF produced, fail here so caller can fall back to STUB.
            if (!File.Exists(dxfOutPath))
                throw new Exception("3DR export failed (no DXF produced).");

            // Validate/stabilize file
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); //[09/02/2025]:Raksha- generous timeout
            var ok = await FileStability.WaitUntilStableAsync(dxfOutPath, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(250), cts.Token);
            Append("exists(afterWait)=" + File.Exists(dxfOutPath) + " stable=" + ok);

            //[09/08/2025]:Raksha- Soft-validate DXF before deciding it's corrupted
            var (ok1, why1) = await DxfSoftValidateAsync(dxfOutPath);
            if (!ok1)
            {
                Append("//[09/08/2025]:Raksha- DXF soft-validate failed (" + why1 + "), retrying after short tail window...");
                await Task.Delay(1500);
                var (ok2, why2) = await DxfSoftValidateAsync(dxfOutPath);
                if (!ok2)
                {
                    Append("//[09/08/2025]:Raksha- DXF still failing soft-validate (" + why2 + "). Will FALLBACK to stub.");
                    // (your existing fallback path continues here)
                }
                else
                {
                    Append("//[09/08/2025]:Raksha- DXF soft-validate OK on retry. Proceeding.");
                    // continue with normal return below
                }
            }
            else
            {
                Append("//[09/08/2025]:Raksha- DXF soft-validate OK. Proceeding.");
            }

            if (!ok) throw new IOException("DXF not stable at: " + dxfOutPath);

            //[09/09/2025]:Raksha- size/tail debug (optional)
            Append($"//[09/09/2025]:Raksha- DXF size={new FileInfo(dxfOutPath).Length} bytes");

            //[09/08/2025]:Raksha- sanity: DXF should end with EOF (exporter fully flushed)
            if (!DxfHasEof(dxfOutPath))
            {
                Append("//[09/08/2025]:Raksha- DXF missing EOF — giving exporter a short tail window...");
                await Task.Delay(2000);
                if (!DxfHasEof(dxfOutPath))
                    throw new IOException("DXF missing EOF sentinel; likely truncated.");
            }

            return await File.ReadAllBytesAsync(dxfOutPath);
        }

        private async Task OnViewInInventorAsync()
        {
            Append("opening....");
            try
            {
                SaveFields();
                var projectId = _cfg.ProjectId;
                var zone = _cfg.Zone;

                // Guard: need a .3dr file to derive the IPT name
                if (string.IsNullOrWhiteSpace(txt3drPath.Text) || !File.Exists(txt3drPath.Text))
                {
                    MessageBox.Show("Pick your .3dr file first.");
                    return;
                }

                //[09/02/2025]:Raksha- Try real 3DR export, log everything
                Append("Attempting real 3DR export...");
                _log.Info("//[09/02/2025]:Raksha- Starting 3DR export attempt");

                byte[] dxfBytes;
                try
                {
                    var tmpOut = Path.Combine(Path.GetTempPath(), $"ps_{Guid.NewGuid():N}.dxf");
                    var groupToExport = string.IsNullOrWhiteSpace(zone) ? "Geometric Group" : zone;

                    dxfBytes = await ExportIgesFrom3DRAsync(_cfg.ThreeDRFilePath, groupToExport, tmpOut);
                    Append("Real 3DR export succeeded.");
                    _log.Info("//[09/02/2025]:Raksha- Real 3DR export succeeded");
                }
                catch (Exception ex)
                {
                    Append("Falling back to STUB DXF. See log for details.");
                    _log.Warn("//[09/02/2025]:Raksha- Falling back to STUB. " + ex);
                    dxfBytes = MinimalDxfStub();
                }

                var guard = new GuardService(_log);
                var exporter = new ExportService(_log, guard);
                var exportRoot3dr = Path.GetFullPath(Path.Combine(_cfg.ThreeDRExportDxf, "..", ".."));
                var dxfPath = exporter.SaveRefDxf(exportRoot3dr, projectId, zone, dxfBytes);
                Append("Wrote DXF -> " + dxfPath);

                // 2) Compute target IPT path (same base name as .3dr)
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var hotRoot = Path.Combine(desktop, "PanelSyncHot");
                var projectsDir = Path.Combine(hotRoot, "Inventor", "Projects");
                Directory.CreateDirectory(projectsDir);

                var baseName = Path.GetFileNameWithoutExtension(txt3drPath.Text);
                var targetIpt = Path.Combine(projectsDir, baseName + ".ipt");

                // 3) Queue an Inventor Job (create/open IPT + import this DXF + bring to front)
                QueueInventorJob_OpenOrCreateAndImport(targetIpt, dxfPath, bringToFront: true);
                Append($"Queued Inventor job for [{baseName}]: open/create IPT and import latest DXF.");
            }
            catch (Exception ex)
            {
                _log.Error("//[09/02/2025]:Raksha- ViewInInventor failed", ex);
                Append("ERROR: " + ex.Message);
            }
            await Task.CompletedTask;
        }

        private static byte[] MinimalDxfStub()
        {
            //[09/02/2025]:Raksha- STUB signature for easy detection
            var dxf =
                "999 PANELSYNC_STUB\n" +
                "0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1009\n0\nENDSEC\n" +
                "0\nSECTION\n2\nENTITIES\n0\nENDSEC\n0\nEOF\n";
            return System.Text.Encoding.ASCII.GetBytes(dxf);
        }

        //[09/08/2025]:Raksha- DXF EOF sentinel check (valid DXF ends with 'EOF')
        private static bool DxfHasEof(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 16) return false;
                var tailLen = (int)Math.Min(4096, fs.Length);
                fs.Seek(-tailLen, SeekOrigin.End);
                using var sr = new StreamReader(fs, System.Text.Encoding.ASCII, detectEncodingFromByteOrderMarks: true, bufferSize: tailLen, leaveOpen: true);
                var tail = sr.ReadToEnd();
                return tail.Contains("\nEOF") || tail.Contains("\r\nEOF");
            }
            catch { return false; }
        }


        // -------- Inventor → 3DR (watch OBJ + meta, then import to 3DR) ----------
        private void ToggleObjWatcher()
        {
            if (_objWatcher != null)
            {
                _objWatcher.Dispose();
                _objWatcher = null;
                Append("Stopped OBJ watcher.");
                btnStartObjWatch.Text = "Start watching OBJ → import to 3DR";
                return;
            }

            var folder = _cfg.InventorExportObj;
            if (!Directory.Exists(folder)) { MessageBox.Show("Inventor OBJ folder does not exist."); return; }

            _objWatcher = new FileSystemWatcher(folder, "*.obj")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _objWatcher.Created += async (s, e) => await OnNewObjAsync(e.FullPath);

            Append("Watching OBJ folder: " + folder);
            btnStartObjWatch.Text = "Stop watching OBJ";
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
                _log.Warn("//[09/02/2025]:Raksha- OBJ not stable in time -> " + objPath);
                return;
            }

            var metaPath = PathRules.MetaFor(objPath);
            var metaOk = await FileStability.WaitUntilStableAsync(metaPath,
                         TimeSpan.FromMilliseconds(600),
                         TimeSpan.FromMilliseconds(150),
                         cts.Token);

            if (!metaOk) { Append("Warning: meta not stable/missing -> " + metaPath); }

            //[09/02/2025]:Raksha- IMPLEMENT: 3DR-specific import/refresh.
            ImportObjInto3DR(objPath, metaPath);
        }

        private void ImportObjInto3DR(string objPath, string metaPath)
        {
            //[09/02/2025]:Raksha- IMPLEMENT hook
            Append("➡ (stub) Imported into 3DR: " + objPath);
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

        // -------- Inventor Job Queueing ----------
        private void QueueInventorJob_OpenOrCreateAndImport(string iptPath, string dxfPath, bool bringToFront)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var jobsDir = Path.Combine(desktop, "PanelSyncHot", "Jobs");
            Directory.CreateDirectory(jobsDir);

            var job = new
            {
                Kind = "OpenOrCreateAndImportDXF",
                IptPath = iptPath,
                DxfPath = dxfPath,
                BringToFront = bringToFront,
                ProjectId = _cfg.ProjectId,
                Zone = _cfg.Zone,
                CreatedUtc = DateTime.UtcNow.ToString("o")
            };

            var json = JsonSerializer.Serialize(job, new JsonSerializerOptions { WriteIndented = true });

            // //[09/02/2025]:Raksha- Write atomically so the Inventor add-in only sees complete jobs
            var jobPath = Path.Combine(jobsDir, $"job_{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}_{Path.GetFileNameWithoutExtension(iptPath)}.json");
            AtomicWriter.WriteAllText(jobPath, json);
        }

        //[09/02/2025]:Raksha- Verify a file exists or throw a precise error
        private void EnsureFile(string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(label + " not found", path ?? "(null)");
        }

        //[09/08/2025]:Raksha- Soft DXF validation: check size, EOF, ENTITIES section, and at least one geometry entity
        private static async Task<(bool ok, string why)> DxfSoftValidateAsync(string path, CancellationToken ct = default)
        {
            try
            {
                // quick size gate
                var len = new FileInfo(path).Length;
                if (len < 200) return (false, "too small");

                // read a reasonable chunk (up to 8 MB) – DXFs are usually text
                // if huge, we still scan the first and last chunks
                const int MAX_SCAN = 8 * 1024 * 1024;
                string text;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length <= MAX_SCAN)
                    {
                        var buf = new byte[fs.Length];
                        var _ = await fs.ReadAsync(buf, 0, buf.Length, ct);
                        text = System.Text.Encoding.ASCII.GetString(buf);
                    }
                    else
                    {
                        // head + tail scan
                        var headLen = 2 * 1024 * 1024;
                        var tailLen = 2 * 1024 * 1024;
                        var head = new byte[headLen];
                        var tail = new byte[tailLen];
                        var _1 = await fs.ReadAsync(head, 0, head.Length, ct);
                        fs.Seek(-tailLen, SeekOrigin.End);
                        var _2 = await fs.ReadAsync(tail, 0, tail.Length, ct);
                        text = System.Text.Encoding.ASCII.GetString(head) + "\n...TAIL...\n" + System.Text.Encoding.ASCII.GetString(tail);
                    }
                }

                // normalize line endings + case-insensitive searching
                var t = text.Replace("\r", "\n");
                var tLower = t.ToLowerInvariant();

                // EOF check (already have a helper, but double-check here too)
                if (!(t.Contains("\nEOF") || t.Contains("\nEOF\n")))
                    return (false, "missing EOF");

                // ENTITIES section (DXF pattern: 0\nSECTION\n2\nENTITIES\n)
                if (!(tLower.Contains("\nsection") && tLower.Contains("\nentities")))
                    return (false, "missing ENTITIES section markers");

                // geometry tokens (be permissive: R12 POLYLINE/ VERTEX or R2000+ LWPOLYLINE)
                string[] tokens = { "\n0\nline", "\n0\nlwpolyline", "\n0\npolyline", "\n0\ncircle", "\n0\narc", "\n0\nspline" };
                bool hasGeom = false;
                foreach (var tok in tokens) { if (tLower.Contains(tok)) { hasGeom = true; break; } }
                if (!hasGeom) return (false, "no known entity tokens found");

                return (true, "ok");
            }
            catch (Exception ex)
            {
                return (false, "exception: " + ex.Message);
            }
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
                // IGES has S/G/D/P/T records; cheap scan is fine for triage
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

        //[09/14/2025]:Raksha- Launch 3DR with IGES script and return .igs bytes
        private async Task<byte[]> ExportIgesFrom3DRAsync(string threeDrPath, string groupName, string igesOutPath)
        {
            Append("threeDrPath=" + threeDrPath);
            Append("groupName=" + groupName);
            Append("igesOutPath=" + igesOutPath);

            var exe = Get3DRExePath();
            EnsureFile(exe, "3DR.exe");

            var scriptPath = ResolveIgesScriptPath();
            EnsureFile(scriptPath, "3DR IGES script");
            EnsureFile(threeDrPath, ".3dr project");
            Directory.CreateDirectory(Path.GetDirectoryName(igesOutPath)!);
            Append("scriptPath(iges)=" + scriptPath);

            // Reuse Run3DRAsync with a different --ScriptParam
            string js = scriptPath.Replace("\\", "/");
            string proj = threeDrPath.Replace("\\", "/");
            string outp = igesOutPath.Replace("\\", "/");
            string grp = string.IsNullOrWhiteSpace(groupName) ? "/Geometric Group" : (groupName.StartsWith("/") ? groupName : "/" + groupName);
            string log = Path.Combine(Path.GetTempPath(), $"3dr_{Guid.NewGuid():N}.log").Replace("\\", "/");

            string args =
                $"--Script=\"{js}\" " +
                $"--ScriptOutput=\"{log}\" " +
                $"--Silent " +
                $"--ScriptAutorun " +
                $"--ScriptParam=\"project='{proj}'; out='{outp}'; groupPath='{grp}';\"";

            Append("//[09/14/2025]:Raksha- RUN 3DR (IGES) => " + args);

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

    }
}
