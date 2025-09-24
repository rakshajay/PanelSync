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
        public string InventorProjects { get; set; } = "";

        //[09/14/2025]:Raksha- Added IGES path
        public string ThreeDRExportIges { get; set; } = "";


        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PanelSync", "3DRApp", "appsettings.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig();
                    var (projDef, objDef, igesDef, logsDef, _) = HotFolders.EnsureDefaults();

                    if (string.IsNullOrWhiteSpace(cfg.InventorProjects)) cfg.InventorProjects = projDef;
                    if (string.IsNullOrWhiteSpace(cfg.InventorExportObj)) cfg.InventorExportObj = objDef;
                    if (string.IsNullOrWhiteSpace(cfg.ThreeDRExportIges)) cfg.ThreeDRExportIges = igesDef;
                    if (string.IsNullOrWhiteSpace(cfg.LogsRoot)) cfg.LogsRoot = logsDef;

                    Directory.CreateDirectory(cfg.InventorProjects);
                    Directory.CreateDirectory(cfg.InventorExportObj);
                    Directory.CreateDirectory(cfg.ThreeDRExportIges);
                    Directory.CreateDirectory(cfg.LogsRoot);
                    return cfg;
                }
            }
            catch { /* fallback */ }

            var fresh = new AppConfig();
            var (proj, obj, iges, logs, _) = HotFolders.EnsureDefaults();
            fresh.InventorProjects = proj;
            fresh.InventorExportObj = obj;
            fresh.ThreeDRExportIges = iges;
            fresh.LogsRoot = logs;
            return fresh;
        }


        public void Save()
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
