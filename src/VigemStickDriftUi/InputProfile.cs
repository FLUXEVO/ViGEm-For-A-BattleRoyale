using System.Collections.Generic;

namespace VigemStickDriftUi
{
    internal sealed class InputProfile
    {
        public string Name { get; set; } = "Default";
        public string ControllerType { get; set; } = "PS4";
        public int BaseDriftPercent { get; set; }
        public string BasePullDirection { get; set; } = "Center";
        public int HoldExtraDriftPercent { get; set; } = 8;
        public int JitterDelayMs { get; set; } = 120;
        public int JitterStrengthPercent { get; set; } = 18;
        public int ScrollDisengageMs { get; set; } = 500;
        public bool WheelDisengageEnabled { get; set; } = true;
        public string JitterHoldKey { get; set; } = "Shift";
        public string JitterPatternType { get; set; } = "Shake";
        public string CustomPatternPoints { get; set; } = "-18:0:10|18:0:10|-10:10:10|10:-10:10";
        public List<string> DisableKeys { get; set; } = new();
        public Dictionary<string, string> KeyBindings { get; set; } = new();
        public Dictionary<string, bool> TurboEnabled { get; set; } = new();
        public Dictionary<string, int> TurboHz { get; set; } = new();
    }
}
