using System;
using UnityEngine;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class LLMBehaviorSuggestion
    {
        public string suggestedLine = "";
        public string moodBias = "calm";
        public float roamWeightBias;
        public float quietIdleWeightBias;
        public float socialResponseWeightBias;
        public bool showBubble;

        public static LLMBehaviorSuggestion Default()
        {
            return new LLMBehaviorSuggestion();
        }

        public static LLMBehaviorSuggestion ClampToWhitelist(LLMBehaviorSuggestion raw)
        {
            if (raw == null)
            {
                return Default();
            }

            LLMBehaviorSuggestion safe = new LLMBehaviorSuggestion
            {
                suggestedLine = string.IsNullOrEmpty(raw.suggestedLine)
                    ? ""
                    : raw.suggestedLine.Substring(0, Mathf.Min(48, raw.suggestedLine.Length)),
                moodBias = SanitizeMood(raw.moodBias),
                roamWeightBias = Mathf.Clamp(raw.roamWeightBias, -0.35f, 0.35f),
                quietIdleWeightBias = Mathf.Clamp(raw.quietIdleWeightBias, -0.35f, 0.35f),
                socialResponseWeightBias = Mathf.Clamp(raw.socialResponseWeightBias, -0.35f, 0.35f),
                showBubble = raw.showBubble
            };

            return safe;
        }

        private static string SanitizeMood(string mood)
        {
            switch ((mood ?? string.Empty).ToLowerInvariant())
            {
                case "calm":
                case "curious":
                case "affectionate":
                case "alert":
                    return mood.ToLowerInvariant();
                default:
                    return "calm";
            }
        }
    }
}
