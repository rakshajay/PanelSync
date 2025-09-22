using System;
using System.IO;

namespace PanelSync.Core.Naming
{
    public static class PathRules
    {
        //[08/26/2025]:Raksha- Build standard timestamp (local file system friendly).
        public static string StampNow() => DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

        //[09/14/2025]:Raksha- IGES naming like PROJ_Group_YYYYMMDDTHHMMSSZ.igs
        public static string BuildRefIgesName(string projectId, DateTime utc)
        {
            return string.Format("{0}_{1}_{2}.igs",
                projectId,
                utc.ToString("yyyyMMddTHHmmssZ"));
        }

        public static string BuildPanelModelName(Guid projectId, string panelId, string rev, string ext, DateTime utc)
        {
            // ex: <proj>_<panel>_r<rev>_<ts>.<ext>
            return string.Format("{0}_{1}_r{2}_{3}.{4}",
                projectId.ToString("N"),
                panelId,
                rev,
                utc.ToString("yyyyMMddTHHmmssZ"),
                ext.ToLowerInvariant());
        }

        public static string MetaFor(string artifactPath)
        {
            // ex: foo.obj -> foo_meta.json (sidecar)
            var dir = Path.GetDirectoryName(artifactPath) ?? ".";
            var nameNoExt = Path.GetFileNameWithoutExtension(artifactPath);
            return Path.Combine(dir, nameNoExt + "_meta.json");
        }

    }
}
