using System;

namespace CatLife.Llm
{
    public sealed class FocusFeedback
    {
        public string Text { get; set; } = string.Empty;
        public string RecordSummary { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Tone { get; set; } = "warm";
        public string ReactionHint { get; set; } = "idle_breath";
        public double Confidence { get; set; } = 1;
        public string SafetyReason { get; set; } = "local";
        public bool IsDegraded { get; set; }

        public static FocusFeedback Local(string text)
        {
            return new FocusFeedback
            {
                Text = text,
                Source = "local_template",
                SafetyReason = "local_fallback",
                IsDegraded = true
            };
        }

        public static FocusFeedback Llm(
            string text,
            string recordSummary,
            string tone,
            string reactionHint,
            double confidence,
            string reason)
        {
            return new FocusFeedback
            {
                Text = text,
                RecordSummary = recordSummary,
                Source = "llm_structured",
                Tone = string.IsNullOrWhiteSpace(tone) ? "warm" : tone,
                ReactionHint = string.IsNullOrWhiteSpace(reactionHint) ? "idle_breath" : reactionHint,
                Confidence = Math.Max(0, Math.Min(1, confidence)),
                SafetyReason = string.IsNullOrWhiteSpace(reason) ? "structured_output_passed" : reason,
                IsDegraded = false
            };
        }
    }
}
