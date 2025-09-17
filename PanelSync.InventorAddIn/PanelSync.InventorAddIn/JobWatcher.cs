//[09/15/2025]:Raksha- Simplified JobWatcher (IGES-only)
using Inventor;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using SysEnv = System.Environment;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;
using PanelSync.Core.Logging;


namespace PanelSync.InventorAddIn
{
    internal class JobWatcher : IDisposable
    {
        private readonly Application _inv;
        private readonly ILog _log;
        private readonly FileSystemWatcher _watcher;

        public JobWatcher(Application inv, ILog log, string folder)
        {
            _inv = inv;
            _log = log;

            _watcher = new FileSystemWatcher(folder, "*.json");
            _watcher.IncludeSubdirectories = false;
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;

            _watcher.Created += OnCreated;
            _watcher.Changed += OnCreated;
            _watcher.Renamed += OnCreated;

            _watcher.EnableRaisingEvents = true;

            _log.Info("//[09/15/2025]:Raksha- JobWatcher watching folder: " + folder);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            _log.Info("// OnCreated fired: " + e.FullPath + " (ChangeType=" + e.ChangeType + ")");
            _log.Info("// Event fired: " + e.ChangeType + " for " + e.FullPath);
            ThreadPool.QueueUserWorkItem(_ => ProcessJobFile(e.FullPath));
        }

        private void ProcessJobFile(string path)
        {
            _log.Info("1");
            try
            {
                _log.Info("//[09/15/2025]:Raksha- Processing job: " + path);
                string json = System.IO.File.ReadAllText(path);
                var job = JsonConvert.DeserializeObject<OpenOrCreateAndImportJob>(json);

                if (job == null || !job.IsValid)
                {
                    _log.Warn("//[09/15/2025]:Raksha- Invalid job file: " + path);
                    return;
                }

                ExecuteOpenCreateImport(job);
                System.IO.File.Delete(path);
            }
            catch (Exception ex)
            {
                _log.Error("//[09/15/2025]:Raksha- Error processing job " + path + ": " + ex);
            }
        }

        private void ExecuteOpenCreateImport(OpenOrCreateAndImportJob job)
        {
            if (job.Kind != "OpenOrCreateAndImportIGES") return;

            PartDocument doc = null;

            // 🔎 Check if the specific file is already open
            foreach (Document d in _inv.Documents)
            {
                if (string.Equals(d.FullFileName, job.IptPath, StringComparison.OrdinalIgnoreCase))
                {
                    doc = (PartDocument)d;
                    _log.Info("//[09/16/2025]:Raksha- Reusing already open IPT -> " + job.IptPath);
                    break;
                }
            }

            // 📂 If not open, then open from disk or create new
            if (doc == null)
            {
                if (System.IO.File.Exists(job.IptPath))
                {
                    _log.Info("//[09/16/2025]:Raksha- Opening IPT from disk -> " + job.IptPath);
                    doc = (PartDocument)_inv.Documents.Open(job.IptPath, true);
                }
                else
                {
                    _log.Info("//[09/16/2025]:Raksha- Creating new IPT -> " + job.IptPath);
                    doc = (PartDocument)_inv.Documents.Add(
                        DocumentTypeEnum.kPartDocumentObject,
                        _inv.FileManager.GetTemplateFile(DocumentTypeEnum.kPartDocumentObject), true);
                    doc.SaveAs(job.IptPath, false);
                }
            }

            // 🧹 Remove any old IGES imports for a clean refresh
            // 🧹 Remove an old IGES import if it points to the same file
            var compDef = doc.ComponentDefinition;
            foreach (ImportedComponent ic in compDef.ReferenceComponents.ImportedComponents)
            {
                try
                {
                    var def = ic.Definition as ImportedComponentDefinition;
                    if (def == null) continue;

                    // Safely get the path (different Inventor builds expose different props)
                    string srcPath = null;
                    try { srcPath = def.FullFileName; } catch { }

                    if (!string.IsNullOrWhiteSpace(srcPath))
                    {
                        var srcName = System.IO.Path.GetFileName(srcPath);
                        var newName = System.IO.Path.GetFileName(job.IgesPath);

                        if (string.Equals(srcName, newName, StringComparison.OrdinalIgnoreCase))
                        {
                            ic.Delete();
                            _log.Info("//[09/16/2025]:Raksha- Removed old IGES import (matched by filename) -> " + srcName);
                        }
                    }
                }
                catch { }
            }

            // ➕ Add the new IGES import
            var importedDef = compDef.ReferenceComponents.ImportedComponents.CreateDefinition(job.IgesPath);
            compDef.ReferenceComponents.ImportedComponents.Add(importedDef);

            doc.Save();

            // 👁️ Always bring the target doc to front after import
            doc.Activate();
            _inv.ActiveView.Update();

            _log.Info("//[09/16/2025]:Raksha- Imported IGES into " + job.IptPath);
        }


        public void Dispose()
        {
            _log.Info("3");
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
        }
    }

    internal class OpenOrCreateAndImportJob
    {
        public string Kind { get; set; }
        public string IptPath { get; set; }
        public string IgesPath { get; set; }
        public bool BringToFront { get; set; } = true;

        public bool IsValid =>
            Kind == "OpenOrCreateAndImportIGES"
            && !string.IsNullOrWhiteSpace(IptPath)
            && !string.IsNullOrWhiteSpace(IgesPath);
    }

    internal static class FileStability
    {
        public static async Task<bool> WaitUntilStableAsync(string path, int firstDelayMs, int pollMs, int timeoutMs)
        {
            var start = DateTime.UtcNow;
            await Task.Delay(firstDelayMs);
            long lastLen = -1; DateTime lastWrite = DateTime.MinValue; int stableCount = 0;
            while (true)
            {
                if (!IOFile.Exists(path)) return false;
                var fi = new FileInfo(path);
                if (fi.Length == lastLen && fi.LastWriteTimeUtc == lastWrite) stableCount++;
                else { stableCount = 0; lastLen = fi.Length; lastWrite = fi.LastWriteTimeUtc; }
                if (stableCount >= 2) return true;
                if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs) return false;
                await Task.Delay(pollMs);
            }
        }
    }
}
