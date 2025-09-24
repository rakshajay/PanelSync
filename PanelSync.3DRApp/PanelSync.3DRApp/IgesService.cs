using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PanelSync.Core.IO;
using PanelSync.Core.Logging;

namespace PanelSync._3DRApp
{
    internal sealed class IgesService
    {
        private readonly AppConfig _cfg;
        private readonly ILog _log;
        private readonly Action<string> _append;

        public IgesService(AppConfig cfg, ILog log, Action<string> append)
        {
            _cfg = cfg; _log = log; _append = append;
        }

        public async Task ExportIgesToInventorAsync(string threeDrPath, bool exportAll)
        {
            _append("🟢 Clicked Export IGES → Import to Inventor button.");
            _append("✅ Mode Selected: " + (exportAll ? "Export all items" : "Manual: Export visible items only"));

            if (string.IsNullOrWhiteSpace(threeDrPath) || !File.Exists(threeDrPath))
            {
                System.Windows.Forms.MessageBox.Show("Pick your .3dr file first.");
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(threeDrPath);
            var igesOutForScript = Path.Combine(_cfg.ThreeDRExportIges, $"{baseName}.igs");

            byte[] igesBytes;
            try
            {
                igesBytes = await ExportIgesFrom3DRAsync(threeDrPath, igesOutForScript, exportAll);
                _append("3DR IGES export succeeded ✨.");
                _log.Info("3DR IGES export succeeded");
            }
            catch (Exception ex)
            {
                _log.Error("IGES export failed", ex);
                System.Windows.Forms.MessageBox.Show("IGES export failed. See log for details.");
                return;
            }

            var igesPath = Path.Combine(_cfg.ThreeDRExportIges, $"{baseName}.igs");
            await File.WriteAllBytesAsync(igesPath, igesBytes);
            _append($"IGES file ready at: {igesPath}");

            var projectsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "PanelSyncHot", "Inventor", "Projects");
            Directory.CreateDirectory(projectsDir);

            var targetIpt = Path.Combine(projectsDir, baseName + ".ipt");

            if (File.Exists(targetIpt))
                _append($"Found existing Inventor part file: {baseName}.ipt");
            else
                _append($"No existing IPT found → creating new file: {baseName}.ipt");

            QueueInventorJob_OpenOrCreateAndImportIGES(targetIpt, igesPath, true);
            _append("➡️ IGES import job queued for Inventor.");
            _append("👀 if Inventor isn’t open — open it first, then click again.");
        }

        private string ResolveIgesScriptPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var p1 = Path.Combine(baseDir, "Scripts", "ExportGeometricGroupToIges.js");
            if (File.Exists(p1)) return p1;
            var p2 = Path.Combine(baseDir, "ExportGeometricGroupToIges.js");
            if (File.Exists(p2)) return p2;
            throw new FileNotFoundException("ExportGeometricGroupToIges.js not found", p1);
        }

        private async Task<byte[]> ExportIgesFrom3DRAsync(string threeDrPath, string igesOutPath, bool exportAll)
        {
            var exe = Get3DRExePath();
            EnsureFile(exe, "3DR.exe");

            var scriptPath = ResolveIgesScriptPath();
            EnsureFile(scriptPath, "3DR IGES script");
            EnsureFile(threeDrPath, ".3dr project");
            Directory.CreateDirectory(Path.GetDirectoryName(igesOutPath)!);

            string js = scriptPath.Replace("\\", "/");
            string proj = threeDrPath.Replace("\\", "/");
            string outp = igesOutPath.Replace("\\", "/");
            string log = Path.Combine(Path.GetTempPath(), $"3dr_{Guid.NewGuid():N}.log").Replace("\\", "/");

            string args =
                $"--Script=\"{js}\" " +
                $"--ScriptOutput=\"{log}\" " +
                $"--Silent " +
                $"--ScriptAutorun " +
                $"--ScriptParam=\"project='{proj}'; out='{outp}'; exportAll={(exportAll ? 1 : 0)};\"";

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

            if (!File.Exists(igesOutPath))
                throw new Exception("3DR IGES export failed (no .igs produced).");

            var fi = new FileInfo(igesOutPath);
            if (fi.Length < 200) // adjust threshold as needed
            {
                _append("⚠️ IGES file is empty — nothing exported from 3DR.");
                _log.Warn("IGES file appears empty: " + igesOutPath);
                throw new Exception("No meshes found in 3DR export.");
            }


            var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var ok = await FileStability.WaitUntilStableAsync(igesOutPath, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(250), cts2.Token);
            if (!ok) throw new IOException("IGES not stable at: " + igesOutPath);

            return await File.ReadAllBytesAsync(igesOutPath);
        }

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
                if (File.Exists(c)) return c;

            throw new FileNotFoundException("3DR.exe not found", "(auto)");
        }

        private void EnsureFile(string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException(label + " not found", path ?? "(null)");
        }

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
    }
}
