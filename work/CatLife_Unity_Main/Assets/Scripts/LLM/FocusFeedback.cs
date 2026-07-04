using System;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class FocusFeedback
    {
        public string text = "";
        public string source = "local_template";
        public bool isDegraded = true;
        public string safetyReason = "local";

        public static FocusFeedback Create(string value, string sourceName, bool degraded, string reason)
        {
            return new FocusFeedback
            {
                text = string.IsNullOrEmpty(value) ? "这段记录已保存，猫咪会继续安静陪你。" : value,
                source = string.IsNullOrEmpty(sourceName) ? "local_template" : sourceName,
                isDegraded = degraded,
                safetyReason = string.IsNullOrEmpty(reason) ? "ok" : reason
            };
        }
    }
}
