//[09/15/2025]:Raksha- JobWatcher (IGES + OBJ support)
using Inventor;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            //foreach (ApplicationAddIn addin in _inv.ApplicationAddIns)
            //{
            //    _log.Info($"AddIn: {addin.DisplayName} [{addin.ClassIdString}]");
            //}

            _log.Info("//[09/15/2025]:Raksha- JobWatcher watching folder: " + folder);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            _log.Info("// OnCreated fired: " + e.FullPath + " (ChangeType=" + e.ChangeType + ")");
            ThreadPool.QueueUserWorkItem(_ => ProcessJobFile(e.FullPath));
        }

        private void ProcessJobFile(string path)
        {
            try
            {
                _log.Info("//[09/15/2025]:Raksha- Processing job: " + path);
                string json = System.IO.File.ReadAllText(path);

                // Peek job kind
                var kindOnly = JsonConvert.DeserializeObject<dynamic>(json);
                string kind = kindOnly?.Kind;

                if (string.IsNullOrWhiteSpace(kind))
                {
                    _log.Warn("//[09/17/2025]:Raksha- Job missing Kind: " + path);
                    return;
                }

                if (kind == "OpenOrCreateAndImportIGES")
                {
                    var job = JsonConvert.DeserializeObject<OpenOrCreateAndImportJob>(json);
                    if (job != null && job.IsValid)
                        ExecuteOpenCreateImport(job);
                    else
                        _log.Warn("//[09/15/2025]:Raksha- Invalid IGES job: " + path);
                }
                else if (kind == "ExportPanelAsOBJ")
                {
                    var job = JsonConvert.DeserializeObject<ExportPanelAsObjJob>(json);
                    if (job != null && job.IsValid)
                        ExecuteExportPanelAsObj(job);
                    else
                        _log.Warn("//[09/17/2025]:Raksha- Invalid OBJ job: " + path);
                }
                else
                {
                    _log.Warn("//[09/17/2025]:Raksha- Unknown job kind: " + kind);
                }

                System.IO.File.Delete(path);
            }
            catch (Exception ex)
            {
                _log.Error("//[09/15/2025]:Raksha- Error processing job " + path + ": " + ex);
            }
        }

        // === IGES Import ===
        private void ExecuteOpenCreateImport(OpenOrCreateAndImportJob job)
        {
            if (job.Kind != "OpenOrCreateAndImportIGES") return;

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
            //[09/22/2025]:Raksha- Force import as millimeters
            //importedDef.ReferenceModel = true;
            // Units are handled on the Document itself:
            doc.UnitsOfMeasure.LengthUnits = UnitsTypeEnum.kMillimeterLengthUnits;
            compDef.ReferenceComponents.ImportedComponents.Add(importedDef);


            doc.Save();
            if (job.BringToFront) { doc.Activate(); _inv.ActiveView.Update(); }

            _log.Info("//[09/15/2025]:Raksha- Imported IGES into " + job.IptPath);
        }

        // === OBJ Export ===
        private void ExecuteExportPanelAsObj(ExportPanelAsObjJob job)
        {
            try
            {
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
                    _log.Warn("//[09/17/2025]:Raksha- No open document found for OBJ export: " + job.IptPath);
                    return;
                }

                var meta = new PanelSync.Core.Models.PanelMeta
                {
                    ProjectId = Guid.NewGuid().ToString("N"),
                    PanelId = job.PanelId,
                    Rev = job.Rev,
                    ExportedAtUtc = DateTime.UtcNow,
                    Source = new PanelSync.Core.Models.SourceInfo { App = "Inventor", Version = _inv.SoftwareVersion.DisplayVersion }
                };

                // Translator: OBJ export
                var ctx = _inv.TransientObjects.CreateTranslationContext();
                ctx.Type = IOMechanismEnum.kFileBrowseIOMechanism;

                var data = _inv.TransientObjects.CreateDataMedium();
                var outDir = job.OutFolder;
                Directory.CreateDirectory(outDir);
                // Unique name: base_panelid_rev_yyyymmddThhmmssZ.obj
                var baseName = System.IO.Path.GetFileNameWithoutExtension(job.IptPath);
                var objName = $"{baseName}_{job.PanelId}_r{job.Rev}_{DateTime.UtcNow:yyyyMMddTHHmmssZ}.obj";
                var objPath = System.IO.Path.Combine(outDir, objName);
                data.FileName = objPath;

                var options = _inv.TransientObjects.CreateNameValueMap();
                //[09/18/2025]:Raksha- Use OBJ Export Translator
                var addin = _inv.ApplicationAddIns.ItemById["{F539FB09-FC01-4260-A429-1818B14D6BAC}"];
                var trans = (TranslatorAddIn)addin;

                if (trans.HasSaveCopyAsOptions[doc, ctx, options])
                    trans.SaveCopyAs(doc, ctx, options, data);

                // Save OBJ + sidecar meta
                var guard = new PanelSync.Core.Services.GuardService(_log);
                var exp = new PanelSync.Core.Services.ExportService(_log, guard);
                var bytes = System.IO.File.ReadAllBytes(objPath);
                exp.SavePanelModel(job.OutFolder, meta, bytes, "obj");

                _log.Info("//[09/17/2025]:Raksha- Exported OBJ -> " + objPath);
                if (job.BringToFront) { doc.Activate(); _inv.ActiveView.Update(); }
            }
            catch (Exception ex)
            {
                _log.Error("//[09/17/2025]:Raksha- ExportPanelAsOBJ failed", ex);
            }
        }

        public void Dispose()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
        }
    }

    // === Job DTOs ===
    internal class ExportPanelAsObjJob
    {
        public string Kind { get; set; } = "ExportPanelAsOBJ";
        public string IptPath { get; set; }
        public string OutFolder { get; set; }
        public string PanelId { get; set; } = "P001";
        public string Rev { get; set; } = "A";
        public bool BringToFront { get; set; } = true;

        public bool IsValid =>
            Kind == "ExportPanelAsOBJ"
            && !string.IsNullOrWhiteSpace(IptPath)
            && !string.IsNullOrWhiteSpace(OutFolder);
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
