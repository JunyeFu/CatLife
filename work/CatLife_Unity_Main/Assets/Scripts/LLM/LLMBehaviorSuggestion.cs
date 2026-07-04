using System;
using UnityEngine;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class LLMBehaviorSuggestion
    {
        public const string ExpectedVersion = "catlife.bluelm.feedback.v1";

        private const int MaxSuggestedLineChars = 48;
        private const float MaxWeightBias = 0.35f;

        public string version = ExpectedVersion;
        public string suggestedLine = "";
        public string moodBias = "calm";
        public float roamWeightBias;
        public float quietIdleWeightBias;
        public float socialResponseWeightBias;
        public string recommendedLocalAction = "none";
        public bool rawTextRequested;
        public bool coordinateCommandIncluded;
        public bool animatorCommandIncluded;
        public bool navMeshCommandIncluded;
        public bool transformCommandIncluded;
        public bool privacyInferenceIncluded;
        public bool showBubble;

        public static LLMBehaviorSuggestion Default()
        {
            return new LLMBehaviorSuggestion();
        }

        public static LLMBehaviorSuggestion ClampToWhitelist(LLMBehaviorSuggestion raw)
        {
            LLMBehaviorSuggestion safe;
            string reason;
            return TryBuildSafe(raw, out safe, out reason) ? safe : Default();
        }

        public static bool TryBuildSafe(
            LLMBehaviorSuggestion raw,
            out LLMBehaviorSuggestion safe,
            out string reason)
        {
            safe = Default();
            if (raw == null)
            {
                reason = "llm_output_missing";
                return false;
            }

            if (!string.IsNullOrEmpty(raw.version) && raw.version != ExpectedVersion)
            {
                reason = "unexpected_schema_version";
                return false;
            }

            if (raw.rawTextRequested ||
                raw.coordinateCommandIncluded ||
                raw.animatorCommandIncluded ||
                raw.navMeshCommandIncluded ||
                raw.transformCommandIncluded ||
                raw.privacyInferenceIncluded)
            {
                reason = "unsafe_output_flags";
                return false;
            }

            string safeLine = SanitizeLine(raw.suggestedLine);
            if (ContainsBlockedOutputText(safeLine))
            {
                reason = "blocked_output_text";
                return false;
            }

            string safeMood = SanitizeMood(raw.moodBias);
            string safeAction = SanitizeRecommendedAction(raw.recommendedLocalAction);
            float safeRoam = Mathf.Clamp(raw.roamWeightBias, -MaxWeightBias, MaxWeightBias);
            float safeQuiet = Mathf.Clamp(raw.quietIdleWeightBias, -MaxWeightBias, MaxWeightBias);
            float safeSocial = Mathf.Clamp(raw.socialResponseWeightBias, -MaxWeightBias, MaxWeightBias);

            safe = new LLMBehaviorSuggestion
            {
                version = ExpectedVersion,
                suggestedLine = safeLine,
                moodBias = safeMood,
                roamWeightBias = safeRoam,
                quietIdleWeightBias = safeQuiet,
                socialResponseWeightBias = safeSocial,
                recommendedLocalAction = safeAction,
                rawTextRequested = false,
                coordinateCommandIncluded = false,
                animatorCommandIncluded = false,
                navMeshCommandIncluded = false,
                transformCommandIncluded = false,
                privacyInferenceIncluded = false,
                showBubble = raw.showBubble
            };

            bool changed =
                safeLine != (raw.suggestedLine ?? string.Empty).Trim() ||
                safeMood != (raw.moodBias ?? string.Empty).ToLowerInvariant() ||
                safeAction != (raw.recommendedLocalAction ?? string.Empty).ToLowerInvariant() ||
                !Mathf.Approximately(safeRoam, raw.roamWeightBias) ||
                !Mathf.Approximately(safeQuiet, raw.quietIdleWeightBias) ||
                !Mathf.Approximately(safeSocial, raw.socialResponseWeightBias);
            reason = changed ? "passed_with_output_clamp" : "passed";
            return true;
        }

        private static string SanitizeLine(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim()
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ');
            while (trimmed.Contains("  "))
            {
                trimmed = trimmed.Replace("  ", " ");
            }

            return trimmed.Substring(0, Mathf.Min(MaxSuggestedLineChars, trimmed.Length));
        }

        private static bool ContainsBlockedOutputText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string lower = value.ToLowerInvariant();
            return lower.Contains("http://") ||
                   lower.Contains("https://") ||
                   lower.Contains("file://") ||
                   lower.Contains("screen capture") ||
                   lower.Contains("screenshot") ||
                   lower.Contains("raw input") ||
                   lower.Contains("raw text") ||
                   lower.Contains("cross-app") ||
                   lower.Contains("contact") ||
                   lower.Contains("message") ||
                   lower.Contains("coordinate") ||
                   lower.Contains("position") ||
                   lower.Contains("transform") ||
                   lower.Contains("physics") ||
                   lower.Contains("navmesh") ||
                   lower.Contains("animator") ||
                   lower.Contains("camera") ||
                   lower.Contains("microphone") ||
                   lower.Contains("clipboard") ||
                   lower.Contains("package") ||
                   lower.Contains("command");
        }

        private static string SanitizeMood(string mood)
        {
            switch ((mood ?? string.Empty).ToLowerInvariant())
            {
                case "quiet":
                case "calm":
                case "curious":
                case "affectionate":
                case "alert":
                    return mood.ToLowerInvariant();
                default:
                    return "calm";
            }
        }

        private static string SanitizeRecommendedAction(string action)
        {
            switch ((action ?? string.Empty).ToLowerInvariant())
            {
                case "":
                case "none":
                    return "none";
                case "quiet_idle":
                case "quiet_companion":
                    return "quiet_idle";
                case "soft_roam":
                case "gentle_return":
                    return "soft_roam";
                case "social_response":
                case "reward_after_focus":
                    return "social_response";
                default:
                    return "none";
            }
        }
    }
}
