//[09/15/2025]:Raksha- JobWatcher (IGES + OBJ support)
using Inventor;
using Newtonsoft.Json;
using PanelSync.Core.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using SysEnv = System.Environment;

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

            _log.Info("JobWatcher watching folder: " + folder);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            //_log.Info("OnCreated fired: " + e.FullPath + " (ChangeType=" + e.ChangeType + ")");
            ThreadPool.QueueUserWorkItem(_ => ProcessJobFile(e.FullPath));
        }

        private void ProcessJobFile(string path)
        {
            try
            {
                //_log.Info("Processing job: " + path);
                string json = IOFile.ReadAllText(path);

                // Peek job kind
                var kindOnly = JsonConvert.DeserializeObject<dynamic>(json);
                string kind = kindOnly?.Kind;

                if (string.IsNullOrWhiteSpace(kind))
                {
                    _log.Warn("Job missing Kind: " + path);
                    return;
                }

                if (kind == "OpenOrCreateAndImportIGES")
                {
                    var job = JsonConvert.DeserializeObject<OpenOrCreateAndImportJob>(json);
                    if (job != null && job.IsValid)
                        ExecuteOpenCreateImport(job);
                    else
                        _log.Warn("Invalid IGES job: " + path);
                }
                else if (kind == "ExportPanelAsOBJ")
                {
                    var job = JsonConvert.DeserializeObject<ExportPanelAsObjJob>(json);
                    if (job != null && job.IsValid)
                        ExecuteExportPanelAsObj(job);
                    else
                        _log.Warn("Invalid OBJ job: " + path);
                }
                else
                {
                    _log.Warn("Unknown job kind: " + kind);
                }

                //IOFile.Delete(path);
            }
            catch (Exception ex)
            {
                _log.Error("Error processing job " + path, ex);
            }
        }

        // === IGES Import ===
        private void ExecuteOpenCreateImport(OpenOrCreateAndImportJob job)
        {
            if (job.Kind != "OpenOrCreateAndImportIGES") return;

            if (_inv == null)
            {
                _log.Warn("Inventor is not running. Please open Inventor before importing IGES.");
                return;
            }

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
            doc.UnitsOfMeasure.LengthUnits = UnitsTypeEnum.kMillimeterLengthUnits;
            compDef.ReferenceComponents.ImportedComponents.Add(importedDef);

            doc.Save();
            if (job.BringToFront) { doc.Activate(); _inv.ActiveView.Update(); }

            _log.Info($"✅ IGES import complete: {System.IO.Path.GetFileName(job.IptPath)}");
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
                    _log.Warn("OBJ export skipped: " + job.IptPath + " is not open in Inventor. Please open it first.");
                    return;
                }
                var compDef = doc.ComponentDefinition as PartComponentDefinition;
                if (compDef == null || compDef.SurfaceBodies == null || compDef.SurfaceBodies.Count == 0)
                {
                    _log.Warn("OBJ export skipped: no solid bodies found in Inventor document. Please check and save your part.");
                    return;
                }

                // Translator: OBJ export
                var ctx = _inv.TransientObjects.CreateTranslationContext();
                ctx.Type = IOMechanismEnum.kFileBrowseIOMechanism;

                var data = _inv.TransientObjects.CreateDataMedium();
                var outDir = job.OutFolder;
                Directory.CreateDirectory(outDir);

                // 🔄 Simple clean filename (no timestamp)
                var baseName = System.IO.Path.GetFileNameWithoutExtension(job.IptPath);
                var objName = $"{baseName}_{job.PanelId}_r{job.Rev}.obj";
                var objPath = System.IO.Path.Combine(outDir, objName);

                // 🔄 Always overwrite old file
                if (System.IO.File.Exists(objPath))
                {
                    try { System.IO.File.Delete(objPath); } catch { /* ignore */ }
                }

                data.FileName = objPath;

                var options = _inv.TransientObjects.CreateNameValueMap();
                var addin = _inv.ApplicationAddIns.ItemById["{F539FB09-FC01-4260-A429-1818B14D6BAC}"];
                var trans = (TranslatorAddIn)addin;
                int solidCount = doc.ComponentDefinition.SurfaceBodies
                   .OfType<SurfaceBody>()
                   .Count(b => b.IsSolid);

                if (solidCount == 0)
                {
                    _log.Warn("⚠️ No solid bodies found in this Inventor document → nothing to export as OBJ.");
                    return;
                }
                else
                {
                    _log.Info($"OBJ will export solid bodies from {System.IO.Path.GetFileName(doc.FullFileName)}");
                }


                if (trans.HasSaveCopyAsOptions[doc, ctx, options])
                {
                    SetOpt(options, "ExportAllSolids", true);
                    SetOpt(options, "ExportSelection", 0);
                    SetOpt(options, "Resolution", 5);
                    SetOpt(options, "SurfaceType", 0);

                    trans.SaveCopyAs(doc, ctx, options, data);
                }

                // Force timestamp refresh so Explorer shows new export time
                System.IO.File.SetLastWriteTimeUtc(objPath, DateTime.UtcNow);

                _log.Info("Exported OBJ -> " + objPath);
                if (job.BringToFront) { doc.Activate(); _inv.ActiveView.Update(); }
            }
            catch (Exception ex)
            {
                _log.Error("ExportPanelAsOBJ failed", ex);
            }
        }

        //[09/23/2025]:Raksha- Helper to set translator option safely (no HasKey)
        private void SetOpt(NameValueMap opts, string key, object value)
        {
            try { opts.Value[key] = value; }                   // try set
            catch { try { opts.Add(key, value); } catch { } }  // else add
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

    //[09/23/2025]:Raksha- Lightweight stability check for job files
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
