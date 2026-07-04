using System;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class FocusFeedback
    {
        public string text = "";
        public string recordSummary = "";
        public string source = "local_template";
        public string tone = "warm";
        public string reactionHint = "idle_breath";
        public float confidence = 1f;
        public bool isDegraded = true;
        public string safetyReason = "local";

        public static FocusFeedback Create(string value, string sourceName, bool degraded, string reason)
        {
            return Create(value, "", sourceName, "warm", "idle_breath", 1f, degraded, reason);
        }

        public static FocusFeedback Create(
            string value,
            string summary,
            string sourceName,
            string toneName,
            string reaction,
            float confidence01,
            bool degraded,
            string reason)
        {
            return new FocusFeedback
            {
                text = string.IsNullOrEmpty(value) ? "这段记录已保存，猫咪会继续安静陪你。" : value,
                recordSummary = string.IsNullOrEmpty(summary) ? "" : summary,
                source = string.IsNullOrEmpty(sourceName) ? "local_template" : sourceName,
                tone = string.IsNullOrEmpty(toneName) ? "warm" : toneName,
                reactionHint = string.IsNullOrEmpty(reaction) ? "idle_breath" : reaction,
                confidence = Math.Max(0f, Math.Min(1f, confidence01)),
                isDegraded = degraded,
                safetyReason = string.IsNullOrEmpty(reason) ? "ok" : reason
            };
        }
    }
}
