using System;
using UnityEngine;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class FocusFeedbackLlmOutput
    {
        public const string ExpectedSchemaVersion = "catlife.focus_feedback.v1";
        private const int MaxBubbleChars = 48;
        private const int MaxRecordSummaryChars = 90;

        public string schema_version = ExpectedSchemaVersion;
        public string bubble_text = "";
        public string record_summary = "";
        public string tone = "warm";
        public string reaction_hint = "idle_breath";
        [Range(0f, 1f)] public float confidence = 1f;
        public FocusFeedbackSafety safety = new FocusFeedbackSafety();

        public static bool TryBuildFeedback(
            FocusFeedbackLlmOutput raw,
            string source,
            out FocusFeedback feedback,
            out string reason)
        {
            feedback = FocusFeedback.Create("", source, true, "llm_output_missing");
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

            if (raw.confidence < 0.5f)
            {
                reason = "low_confidence";
                return false;
            }

            string bubbleText = SanitizeText(raw.bubble_text, MaxBubbleChars);
            string recordSummary = SanitizeText(raw.record_summary, MaxRecordSummaryChars);
            if (ContainsBlockedText(bubbleText) || ContainsBlockedText(recordSummary))
            {
                reason = "blocked_feedback_text";
                return false;
            }

            string safeTone = SanitizeTone(raw.tone);
            string safeReaction = SanitizeReaction(raw.reaction_hint);
            feedback = FocusFeedback.Create(
                bubbleText,
                recordSummary,
                string.IsNullOrEmpty(source) ? "llm_structured" : source,
                safeTone,
                safeReaction,
                Mathf.Clamp01(raw.confidence),
                false,
                "structured_output_passed");
            reason = safeTone == raw.tone && safeReaction == raw.reaction_hint
                ? "structured_output_passed"
                : "structured_output_clamped";
            feedback.safetyReason = reason;
            return true;
        }

        private static string SanitizeText(string value, int maxChars)
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

            return trimmed.Substring(0, Mathf.Min(Mathf.Max(0, maxChars), trimmed.Length));
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
                   lower.Contains("medical") ||
                   lower.Contains("diagnosis") ||
                   lower.Contains("coordinate") ||
                   lower.Contains("transform") ||
                   lower.Contains("physics") ||
                   lower.Contains("navmesh") ||
                   lower.Contains("animator") ||
                   lower.Contains("command");
        }
    }

    [Serializable]
    public sealed class FocusFeedbackSafety
    {
        public bool contains_blame;
        public bool contains_medical_claim;
        public bool contains_sensitive_inference;
    }
}
