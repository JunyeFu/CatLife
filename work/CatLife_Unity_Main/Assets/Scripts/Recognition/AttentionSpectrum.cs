namespace CatLife.Recognition
{
    public enum AttentionBand
    {
        Distracted,
        Transitioning,
        Stable
    }

    public enum AttentionTrend
    {
        Falling,
        Steady,
        Rising
    }

    public struct AttentionSpectrumResult
    {
        public AttentionBand band;
        public AttentionTrend trend;
    }

    public static class AttentionSpectrum
    {
        public static AttentionSpectrumResult Evaluate(float focus, float arousal, float distraction, float previousFocus)
        {
            AttentionBand band = distraction >= .55f || arousal >= .72f
                ? AttentionBand.Distracted
                : focus >= .68f && distraction <= .25f
                    ? AttentionBand.Stable
                    : AttentionBand.Transitioning;
            float delta = focus - previousFocus;
            AttentionTrend trend = delta >= .08f
                ? AttentionTrend.Rising
                : delta <= -.08f
                    ? AttentionTrend.Falling
                    : AttentionTrend.Steady;
            return new AttentionSpectrumResult { band = band, trend = trend };
        }
    }
}
