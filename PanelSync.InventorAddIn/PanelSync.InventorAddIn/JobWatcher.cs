//[08/28/2025]:Raksha- Watches PanelSyncHot/Jobs for JSON jobs and executes them.
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Inventor;
//[08/28/2025]:Raksha- Disambiguate System types
using SysEnv = System.Environment;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;

namespace PanelSync.InventorAddin
{
    internal sealed class JobWatcher : IDisposable
    {
        private readonly Application _inv;
        private readonly FileSystemWatcher _fsw;
        private readonly SimpleFileLogger _log;

        public JobWatcher(Application inv, string jobsDir, SimpleFileLogger log)
        {
            _inv = inv;
            _log = log;

            _fsw = new FileSystemWatcher(jobsDir, "job_*.json");
            _fsw.IncludeSubdirectories = false;
            _fsw.EnableRaisingEvents = true;
            _fsw.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
            _fsw.Created += async (s, e) => await OnNewJobAsync(e.FullPath);
            _fsw.Changed += async (s, e) => await OnNewJobAsync(e.FullPath);
        }

        public void Dispose() { if (_fsw != null) _fsw.Dispose(); }

        private async Task OnNewJobAsync(string jobPath)
        {
            try
            {
                if (!FileStability.WaitUntilStableAsync(jobPath, 600, 150, 8000).GetAwaiter().GetResult())
                    return;

                var json = IOFile.ReadAllText(jobPath, Encoding.UTF8);
                var job = OpenOrCreateAndImportJob.Parse(json);
                if (job == null || !job.IsValid) { _log.Warn("//[08/28/2025]:Raksha- Invalid job: " + jobPath); return; }

                ExecuteOpenCreateImport(job);
                IOFile.Delete(jobPath);
            }
            catch (Exception ex)
            {
                _log.Error("//[08/28/2025]:Raksha- Job error: " + ex.Message);
            }
        }

        private void ExecuteOpenCreateImport(OpenOrCreateAndImportJob job)
        {
            var doc = OpenOrCreateIpt(job.IptPath);

            if (!FileStability.WaitUntilStableAsync(job.DxfPath, 600, 150, 8000).GetAwaiter().GetResult())
            { _log.Warn("//[08/28/2025]:Raksha- DXF not stable: " + job.DxfPath); return; }

            ImportDxfIntoPart((PartDocument)doc, job.DxfPath);

            doc.Save();
            if (job.BringToFront) { doc.Activate(); _inv.ActiveView.Update(); }
            _log.Info("//[08/28/2025]:Raksha- Imported DXF into " + job.IptPath);
        }

        private Document OpenOrCreateIpt(string iptPath)
        {
            foreach (Document d in _inv.Documents)
                if (string.Equals(d.FullFileName, iptPath, StringComparison.OrdinalIgnoreCase))
                    return d;

            if (IOFile.Exists(iptPath))
                return _inv.Documents.Open(iptPath, true);

            var part = (PartDocument)_inv.Documents.Add(
                DocumentTypeEnum.kPartDocumentObject,
                _inv.FileManager.GetTemplateFile(DocumentTypeEnum.kPartDocumentObject), true);

            var dir = IOPath.GetDirectoryName(iptPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            part.SaveAs(iptPath, false);
            return (Document)part;  // explicit cast fixes CS0266
        }

        private void ImportDxfIntoPart(PartDocument part, string dxfPath)
        {
            TranslatorAddIn dxfAddin = null;
            foreach (ApplicationAddIn addin in _inv.ApplicationAddIns)
            {
                var name = (addin.DisplayName ?? "").ToLowerInvariant();
                if (name.Contains("dxf")) { dxfAddin = addin as TranslatorAddIn; break; }
            }
            if (dxfAddin == null) { _log.Warn("//[08/28/2025]:Raksha- DXF Translator not found."); return; }

            var ctx = _inv.TransientObjects.CreateTranslationContext();
            ctx.Type = IOMechanismEnum.kFileBrowseIOMechanism;

            var options = _inv.TransientObjects.CreateNameValueMap();
            options.Add("ImportIntoSketch", true);
            options.Add("AutoScale", true);
            options.Add("ApplyConstraints", false);
            options.Add("ConstrainEndPoints", false);

            var data = _inv.TransientObjects.CreateDataMedium();
            data.FileName = dxfPath;

            object target = (object)part;
            dxfAddin.Open(data, ctx, options, out target);
        }
    }

    internal sealed class OpenOrCreateAndImportJob
    {
        public string Kind = "";
        public string IptPath = "";
        public string DxfPath = "";
        public bool BringToFront = true;

        public bool IsValid
        {
            get
            {
                return Kind == "OpenOrCreateAndImportDXF"
                    && !string.IsNullOrWhiteSpace(IptPath)
                    && !string.IsNullOrWhiteSpace(DxfPath);
            }
        }

        public static OpenOrCreateAndImportJob Parse(string json)
        {
            Func<string, string> Take = key =>
                Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;

            var job = new OpenOrCreateAndImportJob();
            job.Kind = Take("Kind");
            job.IptPath = Take("IptPath");
            job.DxfPath = Take("DxfPath");
            job.BringToFront = Regex.IsMatch(json, "\"BringToFront\"\\s*:\\s*true", RegexOptions.IgnoreCase);
            return job;
        }
    }

    internal sealed class SimpleFileLogger
    {
        private readonly string _path;
        public SimpleFileLogger(string path) { _path = path; }
        public void Info(string m) { Write("INFO", m); }
        public void Warn(string m) { Write("WARN", m); }
        public void Error(string m) { Write("ERROR", m); }
        private void Write(string level, string msg)
        {
            try
            {
                var dir = IOPath.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                IOFile.AppendAllText(_path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + msg + "\r\n", Encoding.UTF8);
            }
            catch { }
        }
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
