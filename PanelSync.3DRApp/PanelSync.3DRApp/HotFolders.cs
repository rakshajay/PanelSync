using System;
using System.IO;

namespace PanelSync._3DRApp
{
    internal static class HotFolders
    {
        //[08/28/2025]:Raksha- Bootstrap default hot folders on Desktop (first run)
        public static (string inventorProjects, string inventorExportObj, string threeDRExportIges, string logsRoot, string root) EnsureDefaults()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var root = Path.Combine(desktop, "PanelSyncHot");

            var projects = Path.Combine(root, "Inventor", "Projects");
            var obj = Path.Combine(root, "Inventor", "exports", "obj");
            var iges = Path.Combine(root, "3DR", "exports", "iges");
            var logs = Path.Combine(root, "logs");

            Directory.CreateDirectory(projects);
            Directory.CreateDirectory(obj);
            Directory.CreateDirectory(iges);
            Directory.CreateDirectory(logs);

            return (projects, obj, iges, logs, root);
        }

    }
}
