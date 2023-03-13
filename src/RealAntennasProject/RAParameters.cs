namespace RealAntennas
{
    class RAParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Local.Setting_Title;  // "RealAntennas Settings"
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "RealAntennas";
        public override string DisplaySection => Section;
        public override int SectionOrder => 1;
        public override bool HasPresets => true;

        [GameParameters.CustomParameterUI("#RA_Setting_enforceMinDirectionalDistance", toolTip = "#RA_Setting_enforceMinDirectionalDistance_tooltip")]  // Apply Minimum Dish Distance|\"Antenna Cones\" directional antennas cannot point at a target that is too close.
        public bool enforceMinDirectionalDistance = true;

        [GameParameters.CustomParameterUI("#RA_Setting_performanceUI", toolTip = "#RA_Setting_performanceUI_tooltip")]  // Performance UI Button | Enable / Disable RealAntennas UI (shows performance metrics), also MOD+I
        public bool performanceUI = true;

        [GameParameters.CustomParameterUI("#RA_Setting_debugWalkLogging", toolTip = "#RA_Setting_debugWalkLogging_tooltip")]  // Periodic Debug Logging | Dump RealAntennas state on interval for debugging.
        public bool debugWalkLogging = false;

        [GameParameters.CustomFloatParameterUI("#RA_Setting_debugWalkInterval", toolTip = "#RA_Setting_debugWalkInterval_tooltip", minValue = 5f, maxValue = 240f, stepCount = 235, displayFormat = "N0")] // Periodic Debug Interval | Interval (sec) at which to send RealAntennas state data to debug log.
        public float debugWalkInterval = 120;

        [GameParameters.CustomFloatParameterUI("#RA_Setting_MaxMinDirectionalDistance", toolTip = "#RA_Setting_MaxMinDirectionalDistance_tooltip", minValue = 0f, maxValue = 1e7f, stepCount = 1000, displayFormat = "N1")]  // Maximum Min Dish Distance | Beyond this distance, a directional antenna can always point at a target.
        public float MaxMinDirectionalDistance = 1000;

        [GameParameters.CustomIntParameterUI("#RA_Setting_MaxTechLevel", displayFormat = "N0", gameMode = GameParameters.GameMode.SANDBOX | GameParameters.GameMode.SANDBOX, maxValue = 20, minValue = 1, stepSize = 1)] // Maximum Tech Level
        public int MaxTechLevel = 10;

        [GameParameters.CustomFloatParameterUI("#RA_Setting_StockRateModifier", toolTip = "#RA_Setting_StockRateModifier_tooltip", minValue = 0.00001f, maxValue = 0.01f, stepCount = 10000, displayFormat = "N5")]  // Rescale transmission rate for stock science | Multiplier to transmission rate for stock science.  Available for balancing purposes: turn it down if science transmits too quickly, or up if too slowly.
        public float StockRateModifier = 0.01f;

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    enforceMinDirectionalDistance = false;
                    break;
                case GameParameters.Preset.Normal:
                case GameParameters.Preset.Moderate:
                case GameParameters.Preset.Hard:
                    enforceMinDirectionalDistance = true;
                    break;
            }
        }
    }
}
