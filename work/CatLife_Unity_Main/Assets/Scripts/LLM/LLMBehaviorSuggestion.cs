using System;
using System.Text;
using System.Text.RegularExpressions;
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
        public string gentleReminder = "";
        public string returnSuggestion = "";
        public string catReactionReason = "";
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
            if (ContainsBlockedOutputText(raw.suggestedLine) || ContainsBlockedOutputText(raw.gentleReminder) ||
                ContainsBlockedOutputText(raw.returnSuggestion) || ContainsBlockedOutputText(raw.catReactionReason))
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
                gentleReminder = SanitizeLine(raw.gentleReminder, 24),
                returnSuggestion = SanitizeLine(raw.returnSuggestion, 24),
                catReactionReason = SanitizeLine(raw.catReactionReason, 24),
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

        private static string SanitizeLine(string value, int maxChars = MaxSuggestedLineChars)
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

            int length = Mathf.Min(maxChars, trimmed.Length);
            if (length > 0 && length < trimmed.Length && char.IsHighSurrogate(trimmed[length - 1]) && char.IsLowSurrogate(trimmed[length])) length--;
            return trimmed.Substring(0, length);
        }

        private static bool ContainsBlockedOutputText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string lower = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            string compact = Regex.Replace(lower, @"[\s\p{Cf}\p{P}]", "");
            bool medical = Regex.IsMatch(compact, @"(你|判断|确定|诊断|患有|得了).{0,12}(抑郁症|焦虑症|精神病|心理疾病)|诊断结果|(停|停止|停用|不要|别再|减少|增加|加大|服用|吃).{0,5}(药|服药|吃药|用药|药物|药量|抗抑郁药)");
            bool coercion = Regex.IsMatch(compact, @"(没用|废物|无能|懒鬼)|不.{0,10}(学|专注|继续).{0,10}(离开你|不要你|不理你|讨厌你)|再.{0,8}(结束|退出|放弃).{0,10}(离开你|不要你|不理你)");
            return medical || coercion || lower.Contains("http://") ||
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
