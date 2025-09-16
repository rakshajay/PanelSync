using System;
using System.IO;
using System.Text.Json;

namespace PanelSync._3DRApp
{
    internal sealed class AppConfig
    {
        public string ProjectId { get; set; } = Guid.NewGuid().ToString("N");
        public string Zone { get; set; } = "ZONEA";
        public string ThreeDRExportDxf { get; set; } = "";
        public string InventorExportObj { get; set; } = "";
        public string LogsRoot { get; set; } = "";
        public string ThreeDRFilePath { get; set; } = "";

        //[09/14/2025]:Raksha- Added IGES path
        public string ThreeDRExportIges { get; set; } = "";


        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PanelSync", "3DRApp", "appsettings.json");

        public static AppConfig Load()
        {
            try { if (File.Exists(ConfigPath)) return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig(); }
            catch { }
            var cfg = new AppConfig();
            var (dxf, obj,igess, logs, _) = HotFolders.EnsureDefaults();
            //[09/14/2025]:Raksha- Default IGES sibling of DXF: ...\3DR\exports\iges
            var iges = Path.Combine(Path.GetDirectoryName(dxf)!, "iges");
            Directory.CreateDirectory(iges);
            cfg.ThreeDRExportIges = iges;

            cfg.ThreeDRExportDxf = dxf;
            cfg.InventorExportObj = obj;
            cfg.LogsRoot = logs;
            return cfg;
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
