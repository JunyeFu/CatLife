using System;

namespace CatLife.Recognition
{
    [Serializable]
    public sealed class BehaviorEvent
    {
        public const string ExpectedSchemaVersion = "catlife.behavior_event.v1";

        public string schemaVersion = ExpectedSchemaVersion;
        public string eventType = "";
        public string routeId = "";
        public string zoneId = "";
        public string sceneState = "";
        public string source = "unity";
        public long tsMs;
        public int durationMs = -1;
        public float deltaLen = -1f;
        public float scrollDy;
        public float velocity;
    }
}
