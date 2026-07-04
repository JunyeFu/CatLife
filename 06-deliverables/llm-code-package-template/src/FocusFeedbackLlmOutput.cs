using System;

namespace CatLife.Llm
{
    public sealed class FocusFeedbackLlmOutput
    {
        public const string ExpectedSchemaVersion = "catlife.focus_feedback.v1";

        public string schema_version { get; set; } = ExpectedSchemaVersion;
        public string bubble_text { get; set; } = string.Empty;
        public string record_summary { get; set; } = string.Empty;
        public string tone { get; set; } = "warm";
        public string reaction_hint { get; set; } = "idle_breath";
        public double confidence { get; set; } = 1;
        public FocusFeedbackSafety safety { get; set; } = new FocusFeedbackSafety();

        public static bool TryBuildFeedback(
            FocusFeedbackLlmOutput raw,
            out FocusFeedback feedback,
            out string reason)
        {
            feedback = FocusFeedback.Local("这段记录已保存，猫咪会继续安静陪你。");
            if (raw == null)
            {
                reason = "llm_output_missing";
                return false;
            }

            if (raw.schema_version != ExpectedSchemaVersion)
            {
                reason = "schema_version_mismatch";
                return false;
            }

            if (raw.safety == null ||
                raw.safety.contains_blame ||
                raw.safety.contains_medical_claim ||
                raw.safety.contains_sensitive_inference)
            {
                reason = "llm_output_safety_flagged";
                return false;
            }

            if (raw.confidence < 0.5)
            {
                reason = "low_confidence";
                return false;
            }

            string text = Clamp(Clean(raw.bubble_text), 48);
            string summary = Clamp(Clean(raw.record_summary), 90);
            if (ContainsBlockedText(text) || ContainsBlockedText(summary))
            {
                reason = "blocked_feedback_text";
                return false;
            }

            string safeTone = SanitizeTone(raw.tone);
            string safeReaction = SanitizeReaction(raw.reaction_hint);
            reason = safeTone == raw.tone && safeReaction == raw.reaction_hint
                ? "structured_output_passed"
                : "structured_output_clamped";
            feedback = FocusFeedback.Llm(text, summary, safeTone, safeReaction, raw.confidence, reason);
            return true;
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        }

        private static string Clamp(string value, int maxChars)
        {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars);
        }

        private static string SanitizeTone(string value)
        {
            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "warm":
                case "quiet":
                case "encouraging":
                    return value.ToLowerInvariant();
                default:
                    return "warm";
            }
        }

        private static string SanitizeReaction(string value)
        {
            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "idle_breath":
                case "head_tilt_listen":
                case "tail_wag_happy":
                case "paw_wave":
                case "stretch_yawn":
                    return value.ToLowerInvariant();
                default:
                    return "idle_breath";
            }
        }

        private static bool ContainsBlockedText(string value)
        {
            string lower = (value ?? string.Empty).ToLowerInvariant();
            return lower.Contains("coordinate") ||
                   lower.Contains("transform") ||
                   lower.Contains("navmesh") ||
                   lower.Contains("animator") ||
                   lower.Contains("screenshot") ||
                   lower.Contains("raw input") ||
                   lower.Contains("cross-app") ||
                   lower.Contains("command");
        }
    }

    public sealed class FocusFeedbackSafety
    {
        public bool contains_blame { get; set; }
        public bool contains_medical_claim { get; set; }
        public bool contains_sensitive_inference { get; set; }
    }
}
