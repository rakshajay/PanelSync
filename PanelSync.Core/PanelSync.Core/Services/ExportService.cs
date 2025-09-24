using Newtonsoft.Json;
using PanelSync.Core.IO;
using PanelSync.Core.Logging;
using PanelSync.Core.Models;
using PanelSync.Core.Naming;
using PanelSync.Core.Security;
using System;
using System.IO;

namespace PanelSync.Core.Services
{
    //[08/27/2025]:Raksha- Writes IGS/OBJ+Meta with atomic replace.
    public class ExportService
    {
        private readonly ILog _log;
        private readonly GuardService _guard;

        public ExportService(ILog log, GuardService guard)
        {
            _log = log;
            _guard = guard;
        }
        public string SavePanelModel(string exportRoot, PanelMeta meta, byte[] modelBytes, string ext)
        {
            // Parse meta.ProjectId (string) to Guid for BuildPanelModelName
            if (!Guid.TryParse(meta.ProjectId, out Guid projectGuid))
                throw new ArgumentException("meta.ProjectId is not a valid Guid", nameof(meta.ProjectId));

            var name = PathRules.BuildPanelModelName(projectGuid, meta.PanelId, meta.Rev, ext, meta.ExportedAtUtc); // :contentReference[oaicite:9]{index=9}
            var outDir = Path.Combine(exportRoot, "exports", ext.ToLowerInvariant());
            _guard.EnsureFolder(outDir);
            var modelPath = Path.Combine(outDir, name);

            _log.Info("Exporting model -> " + modelPath);
            AtomicWriter.WriteAllBytes(modelPath, modelBytes); // atomic write  :contentReference[oaicite:10]{index=10}

            // fill hash + meta path
            meta.Model.File = modelPath;
            meta.Model.Hash = Hasher.FileSha256(modelPath);    // content hash  :contentReference[oaicite:11]{index=11}

            var metaPath = PathRules.MetaFor(modelPath);  //[08/27/2025]:Raksha- sidecar json
            var json = JsonConvert.SerializeObject(meta, Formatting.Indented);
            AtomicWriter.WriteAllText(metaPath, json);


            return modelPath;
        }

        //[09/14/2025]:Raksha- Save IGES into exports\iges (atomic)
        public string SaveRefIges(string exportRoot, string projectId, string Group, byte[] igesBytes)
        {
            var name = PathRules.BuildRefIgesName(projectId, DateTime.UtcNow);
            var outDir = Path.Combine(exportRoot, "exports", "iges");
            _guard.EnsureFolder(outDir);
            var path = Path.Combine(outDir, name);
            _log.Info("Exporting ref IGES -> " + path);
            AtomicWriter.WriteAllBytes(path, igesBytes);
            return path;
        }

    }
}
