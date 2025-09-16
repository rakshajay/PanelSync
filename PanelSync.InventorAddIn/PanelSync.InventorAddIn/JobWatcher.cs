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

            // open or create target IPT
            PartDocument doc = null;
            foreach (Document d in _inv.Documents)
            {
                if (string.Equals(d.FullFileName, job.IptPath, StringComparison.OrdinalIgnoreCase))
                {
                    doc = (PartDocument)d;
                    break;
                }
            }
            if (doc == null)
            {
                doc = (PartDocument)_inv.Documents.Add(DocumentTypeEnum.kPartDocumentObject,
                                                       _inv.FileManager.GetTemplateFile(DocumentTypeEnum.kPartDocumentObject));
                doc.SaveAs(job.IptPath, false);
            }

            var compDef = doc.ComponentDefinition;
            var importedDef = compDef.ReferenceComponents.ImportedComponents.CreateDefinition(job.IgesPath);

            // True = associative link (updates if IGES changes), False = convert once
            //importedDef.ReferenceModel = false;
            //importedDef.IncludeAll();

            compDef.ReferenceComponents.ImportedComponents.Add(importedDef);

            doc.Save();
            if (job.BringToFront) { doc.Activate(); _inv.ActiveView.Update(); }

            _log.Info("// Imported IGES into " + job.IptPath);
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
