using System;
using System.IO;
using System.Text.Json;

namespace PanelSync._3DRApp
{
    internal sealed class AppConfig
    {
        public string ProjectId { get; set; } = Guid.NewGuid().ToString("N");
        public string Group { get; set; } = "Geometric Group";
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
            var (obj,igess, logs, _) = HotFolders.EnsureDefaults();
            var iges = cfg.ThreeDRExportIges;
            Directory.CreateDirectory(iges);

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
