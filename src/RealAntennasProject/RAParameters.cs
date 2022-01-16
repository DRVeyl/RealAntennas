namespace RealAntennas
{
    class RAParameters : GameParameters.CustomParameterNode
    {
        public override string Title => "RealAntennas Settings";
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "RealAntennas";
        public override string DisplaySection => Section;
        public override int SectionOrder => 1;
        public override bool HasPresets => true;

        [GameParameters.CustomParameterUI("Apply Minimum Dish Distance", toolTip = "\"Antenna Cones\" directional antennas cannot point at a target that is too close.")]
        public bool enforceMinDirectionalDistance = true;

        [GameParameters.CustomParameterUI("Performance UI Button", toolTip = "Enable / Disable RealAntennas UI (shows performance metrics), also MOD+I")]
        public bool performanceUI = true;

        [GameParameters.CustomParameterUI("Periodic Debug Logging", toolTip = "Dump RealAntennas state on interval for debugging.")]
        public bool debugWalkLogging = false;

        [GameParameters.CustomFloatParameterUI("Periodic Debug Interval", toolTip = "Interval (sec) at which to send RealAntennas state data to debug log.", minValue = 5f, maxValue = 240f, stepCount = 235, displayFormat = "N0")]
        public float debugWalkInterval = 120;

        [GameParameters.CustomFloatParameterUI("Maximum Min Dish Distance", toolTip = "Beyond this distance, a directional antenna can always point at a target.", minValue = 0f, maxValue = 1e7f, stepCount = 1000, displayFormat = "N1")]
        public float MaxMinDirectionalDistance = 1000;

        [GameParameters.CustomIntParameterUI("Maximum Tech Level", displayFormat = "N0", gameMode = GameParameters.GameMode.SANDBOX | GameParameters.GameMode.SANDBOX, maxValue = 20, minValue = 1, stepSize = 1)]
        public int MaxTechLevel = 10;

        [GameParameters.CustomFloatParameterUI("Rescale transmission rate for stock science", toolTip = "Multiplier to transmission rate for stock science.  Available for balancing purposes: turn it down if science transmits too quickly, or up if too slowly.", minValue = 0.00001f, maxValue = 0.01f, stepCount = 10000, displayFormat = "N5")]
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
