using System;
using System.IO;

namespace PanelSync._3DRApp
{
    internal static class HotFolders
    {
        //[08/28/2025]:Raksha- Bootstrap default hot folders on Desktop (first run)
        public static (string threeDRExportDxf, string inventorExportObj, string logsRoot, string root) EnsureDefaults()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var root = Path.Combine(desktop, "PanelSyncHot");
            var dxf = Path.Combine(root, "3DR", "exports", "dxf");
            var obj = Path.Combine(root, "Inventor", "exports", "obj");
            var logs = Path.Combine(root, "logs");

            Directory.CreateDirectory(dxf);
            Directory.CreateDirectory(obj);
            Directory.CreateDirectory(logs);
            return (dxf, obj, logs, root);
        }
    }
}
