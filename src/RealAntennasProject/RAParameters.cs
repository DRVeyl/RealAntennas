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

        [GameParameters.CustomIntParameterUI("Maximum Tech Level", displayFormat = "N0", gameMode = GameParameters.GameMode.SANDBOX | GameParameters.GameMode.SANDBOX, maxValue = 20, minValue = 1, stepSize = 1)]
        public int MaxTechLevel = 10;

        [GameParameters.CustomFloatParameterUI("Default Packet Interval (s)", toolTip = "Default interval between science packets.  Increase if encountering bug with science not being credited.", minValue = 0.1f, maxValue = 5.0f, stepCount = 50, displayFormat = "N1")]
        public float DefaultPacketInterval = 1.0f;

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
