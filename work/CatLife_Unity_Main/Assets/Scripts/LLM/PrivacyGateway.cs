using UnityEngine;

namespace CatLife.LLM
{
    [DisallowMultipleComponent]
    public sealed class PrivacyGateway : MonoBehaviour
    {
        [SerializeField] private int maxDurationSeconds = 24 * 60 * 60;
        [SerializeField] private int maxPromptSummaryChars = 180;
        [SerializeField] private int maxPromptEventChars = 32;
        [SerializeField] private int maxPromptTagChars = 24;
        [SerializeField] private int maxPromptTags = 8;

        public bool TryValidate(BehaviorFeatureSummary summary, out string reason)
        {
            if (summary.HasBlockedPrivacyFields())
            {
                reason = "blocked_privacy_fields";
                return false;
            }

            if (summary.durationSec <= 0 || summary.durationSec > maxDurationSeconds)
            {
                reason = "invalid_duration";
                return false;
            }

            if (summary.focusDurationSec < 0 || summary.focusDurationSec > summary.durationSec)
            {
                reason = "invalid_focus_duration";
                return false;
            }

            if (summary.interruptCount < 0 || summary.completedSessionsToday < 0 || summary.todayFocusMinutes < 0)
            {
                reason = "invalid_negative_counter";
                return false;
            }

            reason = "passed";
            return true;
        }

        public bool TryValidate(CatPromptContext context, out string reason)
        {
            if (context == null)
            {
                reason = "prompt_context_missing";
                return false;
            }

            if (!context.privacyModeEnabled)
            {
                reason = "privacy_mode_disabled";
                return false;
            }

            if (ContainsBlockedPromptText(context.safeLocalSummary) ||
                ContainsBlockedPromptText(context.sceneInteractionSummary) ||
                ContainsBlockedPromptText(context.sceneInteractionDisplayName) ||
                ContainsBlockedPromptText(context.sceneInteractionPointId))
            {
                reason = "blocked_prompt_text";
                return false;
            }

            if (ContainsBlockedPromptText(context.recentEvents) ||
                ContainsBlockedPromptText(context.sceneInteractionTags))
            {
                reason = "blocked_prompt_tokens";
                return false;
            }

            if (context.focusConfidence < 0f ||
                context.focusConfidence > 1f ||
                context.interactionReadiness < 0f ||
                context.interactionReadiness > 1f ||
                context.focusScore01 < 0f ||
                context.focusScore01 > 1f ||
                context.arousal01 < 0f ||
                context.arousal01 > 1f ||
                context.distraction01 < 0f ||
                context.distraction01 > 1f)
            {
                reason = "prompt_score_out_of_range";
                return false;
            }

            reason = "passed";
            return true;
        }

        public BehaviorFeatureSummary Sanitize(BehaviorFeatureSummary summary)
        {
            summary.schemaVersion = string.IsNullOrEmpty(summary.schemaVersion)
                ? "catlife.focus_summary.v1"
                : summary.schemaVersion;
            summary.locale = "zh-CN";
            summary.durationSec = Mathf.Clamp(summary.durationSec, 1, maxDurationSeconds);
            summary.focusDurationSec = Mathf.Clamp(summary.focusDurationSec, 0, summary.durationSec);
            summary.interruptCount = Mathf.Max(0, summary.interruptCount);
            summary.completedSessionsToday = Mathf.Max(0, summary.completedSessionsToday);
            summary.todayFocusMinutes = Mathf.Max(0, summary.todayFocusMinutes);
            summary.focusScoreAvg01 = Mathf.Clamp01(summary.focusScoreAvg01);
            summary.arousalScoreAvg01 = Mathf.Clamp01(summary.arousalScoreAvg01);
            summary.distractionScoreAvg01 = Mathf.Clamp01(summary.distractionScoreAvg01);
            summary.longestFocusSec = Mathf.Max(0, summary.longestFocusSec);
            summary.rawTextIncluded = false;
            summary.rawTouchPathIncluded = false;
            summary.screenContentIncluded = false;
            summary.crossAppContentIncluded = false;
            return summary;
        }

        public CatPromptContext Sanitize(CatPromptContext context)
        {
            if (context == null)
            {
                return null;
            }

            context.catMoveSpeed01 = Mathf.Clamp01(context.catMoveSpeed01);
            context.focusConfidence = Mathf.Clamp01(context.focusConfidence);
            context.interactionReadiness = Mathf.Clamp01(context.interactionReadiness);
            context.secondsSinceLastInteraction = Mathf.Max(0f, context.secondsSinceLastInteraction);
            context.secondsSinceLastFocusStart = Mathf.Max(0f, context.secondsSinceLastFocusStart);
            context.tapRate1s = Mathf.Max(0f, context.tapRate1s);
            context.tapRate5s = Mathf.Max(0f, context.tapRate5s);
            context.pageSwitches30s = Mathf.Max(0, context.pageSwitches30s);
            context.focusScore01 = Mathf.Clamp01(context.focusScore01);
            context.arousal01 = Mathf.Clamp01(context.arousal01);
            context.distraction01 = Mathf.Clamp01(context.distraction01);
            context.safeLocalSummary = ClampPromptText(context.safeLocalSummary, maxPromptSummaryChars);
            context.recentEvents = SanitizeTokenArray(context.recentEvents, 4, maxPromptEventChars);
            context.sceneInteractionPointId = ClampPromptText(context.sceneInteractionPointId, maxPromptEventChars);
            context.sceneInteractionDisplayName = ClampPromptText(context.sceneInteractionDisplayName, maxPromptEventChars);
            context.sceneInteractionTags = SanitizeTokenArray(context.sceneInteractionTags, maxPromptTags, maxPromptTagChars);
            context.sceneInteractionMotionCue = ClampPromptText(context.sceneInteractionMotionCue, maxPromptTagChars);
            context.secondsSinceSceneInteraction = Mathf.Max(0f, context.secondsSinceSceneInteraction);
            context.sceneInteractionSummary = ClampPromptText(context.sceneInteractionSummary, maxPromptSummaryChars);
            context.privacyModeEnabled = true;
            return context;
        }

        public string BuildPromptAuditLine(CatPromptContext context, string reason)
        {
            string safeReason = string.IsNullOrEmpty(reason) ? "unknown" : reason;
            if (context == null)
            {
                return "prompt_privacy=" + safeReason + "; context=missing";
            }

            return "prompt_privacy=" + safeReason +
                "; scene=" + (context.hasSceneInteraction ? context.sceneInteractionPointId : "none") +
                "; tags=" + CountItems(context.sceneInteractionTags) +
                "; events=" + CountItems(context.recentEvents) +
                "; privacy=" + context.privacyModeEnabled;
        }

        private static bool ContainsBlockedPromptText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string lower = text.ToLowerInvariant();
            return lower.Contains("http://") ||
                lower.Contains("https://") ||
                lower.Contains("file://") ||
                lower.Contains("rawtext") ||
                lower.Contains("screen capture") ||
                lower.Contains("screenshot") ||
                lower.Contains("screencontent") ||
                lower.Contains("screen content") ||
                lower.Contains("raw input") ||
                lower.Contains("raw text") ||
                lower.Contains("cross-app") ||
                lower.Contains("package name") ||
                lower.Contains("packagename") ||
                lower.Contains("clipboard") ||
                lower.Contains(" x/y") ||
                lower.Contains("\"x\"") ||
                lower.Contains("\"y\"") ||
                lower.Contains("transform command") ||
                lower.Contains("navmesh command") ||
                lower.Contains("animator command");
        }

        private static bool ContainsBlockedPromptText(string[] values)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (ContainsBlockedPromptText(values[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ClampPromptText(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int safeMax = Mathf.Max(1, maxChars);
            return text.Length <= safeMax ? text : text.Substring(0, safeMax);
        }

        private static string[] SanitizeTokenArray(string[] values, int maxItems, int maxChars)
        {
            if (values == null || values.Length == 0)
            {
                return new string[0];
            }

            int count = Mathf.Min(Mathf.Max(0, maxItems), values.Length);
            string[] sanitized = new string[count];
            for (int i = 0; i < count; i++)
            {
                sanitized[i] = ClampPromptText(values[i], maxChars);
            }

            return sanitized;
        }

        private static int CountItems(string[] values)
        {
            return values != null ? values.Length : 0;
        }
    }
}
