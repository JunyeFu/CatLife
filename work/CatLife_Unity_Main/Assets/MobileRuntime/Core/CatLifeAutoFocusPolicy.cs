namespace CatLife.Mobile
{
    public sealed class CatLifeAutoFocusPolicy
    {
        private readonly float adaptationSeconds;
        private readonly float confidenceThreshold;
        private float stableSeconds;
        private bool consumed;

        public CatLifeAutoFocusPolicy(float adaptationSeconds, float confidenceThreshold)
        {
            this.adaptationSeconds = adaptationSeconds;
            this.confidenceThreshold = confidenceThreshold;
        }

        public void ResetDwell() => stableSeconds = 0f;

        public bool ShouldStart(CatLifeSessionPhase phase, bool stable, float confidence, float deltaSeconds)
        {
            if (consumed || phase != CatLifeSessionPhase.Normal)
            {
                ResetDwell();
                return false;
            }

            if (!stable || confidence < confidenceThreshold)
            {
                stableSeconds = 0f;
                return false;
            }

            stableSeconds += deltaSeconds;
            if (stableSeconds < adaptationSeconds)
            {
                return false;
            }

            consumed = true;
            return true;
        }
    }
}
