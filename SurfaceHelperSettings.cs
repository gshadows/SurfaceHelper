using Observatory.Framework;
using System.IO;

namespace Observatory.SurfaceHelper
{
    class SurfaceHelperSettings
    {
        public static readonly SurfaceHelperSettings DEFAULT = new()
        {
            ShipDistance1 = 1750,
            ShipDistance2 = 1900,
            OverlayEnabled = true,
            LogFile = "SurfaceHelper.log",//"D:\\PROJECTS\\ObservatoryCorePlugins\\SurfaceHelper\\SurfaceHelper.log",
        };

        [SettingDisplayName("Ship distance 1-st warning (0 - off)")]
        [SettingNumericBounds(0, 2000, 50)]
        public int ShipDistance1 { get; set; }

        [SettingDisplayName("Ship distance 2-nd warning (0 - off)")]
        [SettingNumericBounds(0, 2000, 50)]
        public int ShipDistance2 { get; set; }

        [SettingDisplayName("Enable Ship Distance Overlay")]
        public bool OverlayEnabled { get; set; }


        [SettingDisplayName("Log File")]
        [System.Text.Json.Serialization.JsonIgnore]
        public FileInfo LogFileLocation { get => new FileInfo(LogFile); set => LogFile = value.FullName; }

        [SettingIgnore]
        public string LogFile { get; set; }
    }
}
