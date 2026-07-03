using System;

namespace CatLife.Recognition
{
    [Serializable]
    public struct RealtimeFeatureSnapshot
    {
        public float realtimeSinceStartup;
        public bool isFocusSessionActive;
        public float secondsSinceLastInteraction;
        public float secondsSinceLastFocusStart;
        public float tapRate1s;
        public float tapRate5s;
        public int pageSwitches30s;
        public float focusScore01;
        public float arousal01;
        public float distraction01;
        public string localEventSummary;
    }
}
