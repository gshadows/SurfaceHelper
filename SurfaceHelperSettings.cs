using Observatory.Framework;
using System.IO;

namespace Observatory.SurfaceHelper
{
    class SurfaceHelperSettings
    {
        public static readonly string DEFAULT_LOG_NAME = "SurfaceHelper.log";
        
        public static readonly SurfaceHelperSettings DEFAULT = new()
        {
            ShipDistance1 = 1750,
            ShipDistance2 = 1900,
            ShipCenterOffset = 0.0,
            OverlayEnabled = true,
            LogFile = DEFAULT_LOG_NAME,
            ApproachWelcome = true,
            SCExitWelcome = false,
            TouchdownWelcome = false,
            TemperatureScaleName = true,
            TemperatureScale = 0,
            HighGravity = 1.5f,
            HighTemperature = 700,
        };


        [SettingNewGroup("Ship Distance")]
        [SettingDisplayName("Ship distance 1-st warning (0 - off)")]
        [SettingNumericBounds(0, 2000, 50)]
        public int ShipDistance1 { get; set; }

        [SettingDisplayName("Ship distance 2-nd warning (0 - off)")]
        [SettingNumericBounds(0, 2000, 50)]
        public int ShipDistance2 { get; set; }

        [SettingDisplayName("Ship center offset from midpoint (0 - center, -1 - cockpit, +1 - disembark point))")]
        [SettingNumericBounds(-2, +2, 0.01, 2)]
        public double ShipCenterOffset { get; set; }


        [SettingNewGroup("Planet Info")]
        [SettingDisplayName("Welcome on approach")]
        public bool ApproachWelcome { get; set; }

        [SettingDisplayName("Welcome on SC exit")]
        public bool SCExitWelcome { get; set; }

        [SettingDisplayName("Welcome on touchdown")]
        public bool TouchdownWelcome { get; set; }

        [SettingDisplayName("Welcome on disembark")]
        public bool DisembarkWelcome { get; set; }

        [SettingDisplayName("Name temp. scale")]
        public bool TemperatureScaleName { get; set; }

        [SettingDisplayName("Temp. scale: 0-K,1-C,2-F")]
        [SettingNumericBounds(0, 2, 1)]
        public int TemperatureScale { get; set; }

        [SettingDisplayName("High gravity")]
        [SettingNumericBounds(0, 10, 0.25, 2)]
        public double HighGravity { get; set; }

        [SettingDisplayName("High temperature")]
        [SettingNumericBounds(0, 1500, 50)]
        public int HighTemperature { get; set; }


        [SettingNewGroup("Other Options")]
        [SettingDisplayName("Enable Ship Distance Overlay")]
        public bool OverlayEnabled { get; set; }


        [SettingDisplayName("Log File")]
        [System.Text.Json.Serialization.JsonIgnore]
        public FileInfo LogFileLocation { get => new FileInfo(LogFile); set => LogFile = value.FullName; }

        [SettingIgnore]
        public string LogFile { get; set; }
    }
}
