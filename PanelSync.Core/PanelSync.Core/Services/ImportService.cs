using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PanelSync.Core.Logging;
using PanelSync.Core.Naming;
using PanelSync.Core.IO;

namespace PanelSync.Core.Services
{
    //[08/27/2025]:Raksha- Picks latest stable IGS/OBJ by naming pattern.
    public class ImportService
    {
        private readonly ILog _log;
        public ImportService(ILog log) { _log = log; }

        public async Task<string> GetLatestStableAsync(string exportRoot, string projectId, string ext, CancellationToken ct)
        {
            var dir = Path.Combine(exportRoot, "exports", ext.ToLowerInvariant());
            if (!Directory.Exists(dir)) return null;

            // match prefix "projectId_" and ext
            var files = Directory.GetFiles(dir, projectId + "_*." + ext, SearchOption.TopDirectoryOnly);
            var latest = files.OrderByDescending(f => f).FirstOrDefault();
            if (latest == null) return null;

            _log.Debug("//[08/27/2025]:Raksha- Latest candidate -> " + latest);

            // wait for stability (handles external writers)  :contentReference[oaicite:13]{index=13}
            var ok = await FileStability.WaitUntilStableAsync(latest,
                         TimeSpan.FromMilliseconds(500),
                         TimeSpan.FromMilliseconds(150),
                         ct);
            return ok ? latest : null;
        }
    }
}
