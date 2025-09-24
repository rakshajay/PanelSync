using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PanelSync.Core.IO;
using PanelSync.Core.Logging;

namespace PanelSync._3DRApp
{
    internal sealed class ObjService : IDisposable
    {
        private readonly AppConfig _cfg;
        private readonly ILog _log;
        private readonly Action<string> _append;
        private FileSystemWatcher? _objWatcher;

        public ObjService(AppConfig cfg, ILog log, Action<string> append)
        {
            _cfg = cfg; _log = log; _append = append;
        }

        public void ExportObjTo3DR(string threeDrPath)
        {
            _append("🟢 Clicked Export OBJ → Import to 3DR button.");

            if (string.IsNullOrWhiteSpace(threeDrPath) || !File.Exists(threeDrPath))
            {
                System.Windows.Forms.MessageBox.Show("Pick your .3dr file first.");
                return;
            }

            StartObjWatcherOnce();

            var baseName = Path.GetFileNameWithoutExtension(threeDrPath);
            var targetIpt = Path.Combine(_cfg.InventorProjects, baseName + ".ipt");

            QueueInventorJob_ExportOBJ(targetIpt, _cfg.InventorExportObj);

            _append($"➡️ Export job queued for {baseName}.ipt.");
            _append("⌛ Inventor will generate OBJ shortly…");
            _append("👀 if Inventor isn’t open — open it first, then click again.");
        }

        private void StartObjWatcherOnce()
        {
            if (_objWatcher != null) return;
            var folder = _cfg.InventorExportObj;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            _objWatcher = new FileSystemWatcher(folder, "*.obj")
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _objWatcher.Created += async (s, e) => await OnNewObjAsync(e.FullPath);
        }

        private async Task OnNewObjAsync(string objPath)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var ok = await FileStability.WaitUntilStableAsync(objPath, TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(150), cts.Token);

            if (!ok)
            {
                _append("Skipped (not stable): " + objPath);
                _log.Warn("OBJ not stable in time -> " + objPath);
                return;
            }

            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var hotRoot = Path.Combine(desktop, "PanelSyncHot");
                var jsPath = Path.Combine(hotRoot, "latest_obj.js");
                var jsContent =
@"// Auto-generated script

var objPath = """ + objPath.Replace("\\", "/") + @""";

function log(m) { try { print(m); } catch (_) { } }

if (!objPath) { throw ""objPath is missing!""; }

log(""Importing OBJ: "" + objPath);
var rc = SPoly.FromFile(objPath);
if (!rc || rc.ErrorCode !== 0) { throw ""SPoly.FromFile failed: "" + JSON.stringify(rc); }
if (!rc.PolyTbl || rc.PolyTbl.length === 0) { throw ""No meshes found in OBJ""; }

for (var i = 0; i < rc.PolyTbl.length; i++) {
    var mesh = rc.PolyTbl[i];
    mesh.AddToDoc();
    log(""Added mesh: "" + mesh.GetName() + "" ("" + i + "")"");
}

try { var vs = SViewSet.New(true); vs.Update(true); } catch(_) {}
SaveDoc("""", true);
log(""Saved project after import."");
";


                File.WriteAllText(jsPath, jsContent);
                _append("👉 In 3DR, run the latest_obj.js script to import your OBJ.");
            }
            catch (Exception ex)
            {
                _log.Error("Failed to write latest_obj.js", ex);
                _append("ERROR: Could not write latest_obj.js (" + ex.Message + ")");
            }
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

        public void Dispose()
        {
            if (_objWatcher != null)
            {
                _objWatcher.EnableRaisingEvents = false;
                _objWatcher.Dispose();
                _objWatcher = null;
            }
        }
    }
}
