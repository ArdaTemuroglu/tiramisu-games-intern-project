namespace ArcadeRacing
{
    public enum RaceMode
    {
        TimeTrial,
        DriftChallenge
    }

    public static class RaceModeUtility
    {
        public static string GetDisplayName(RaceMode mode)
        {
            return mode == RaceMode.DriftChallenge
                ? "Drift Challenge"
                : "Time Trial";
        }
    }
}
