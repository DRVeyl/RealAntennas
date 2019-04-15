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

        [GameParameters.CustomParameterUI("All Antennas Relay", toolTip = "Turning this off does nothing.")]
        public bool allAntennasRelay = true;

        [GameParameters.CustomParameterUI("Apply Minimum Dish Distance", toolTip = "\"Antenna Cones\" directional antennas cannot point at a target that is too close.")]
        public bool enforceMinDirectionalDistance = true;

        [GameParameters.CustomFloatParameterUI("Maximum Min Dish Distance", toolTip = "Beyond this distance, a directional antenna can always point at a target.", minValue = 0f, maxValue = 1e7f, stepCount = 1000, displayFormat = "N1")]
        public float MaxMinDirectionalDistance = 1000;

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
