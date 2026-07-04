using System;
using System.Collections;
using UnityEngine;

namespace CatLife.LLM
{
    [DisallowMultipleComponent]
    public sealed class FocusFeedbackProvider : MonoBehaviour
    {
        [SerializeField] private PrivacyGateway privacyGateway;
        [SerializeField] private bool enableMockModel = true;
        [SerializeField] private float simulatedLatencySeconds = 0.25f;
        [SerializeField] private float timeoutSeconds = 1.5f;

        public string LastSource { get; private set; } = "local_template";
        public string LastSafetyReason { get; private set; } = "not_requested";

        private void Awake()
        {
            if (privacyGateway == null)
            {
                privacyGateway = GetComponent<PrivacyGateway>();
            }
        }

        public void Configure(PrivacyGateway gateway)
        {
            privacyGateway = gateway;
        }

        public void RequestFeedback(
            BehaviorFeatureSummary summary,
            bool allowSmartExplanation,
            Action<FocusFeedback> onComplete)
        {
            PrivacyGateway gateway = privacyGateway;
            if (gateway == null)
            {
                gateway = GetComponent<PrivacyGateway>();
            }

            string reason = "";
            if (gateway == null || !gateway.TryValidate(summary, out reason))
            {
                Complete(LocalTemplateFallback.Generate(summary, string.IsNullOrEmpty(reason) ? "privacy_gateway_missing" : reason), onComplete);
                return;
            }

            BehaviorFeatureSummary safeSummary = gateway.Sanitize(summary);
            if (!allowSmartExplanation || !enableMockModel)
            {
                Complete(LocalTemplateFallback.Generate(safeSummary, allowSmartExplanation ? "mock_model_disabled" : "smart_explanation_off"), onComplete);
                return;
            }

            StartCoroutine(CoMockModelFeedback(safeSummary, onComplete));
        }

        public FocusFeedback BuildImmediateFallback(BehaviorFeatureSummary summary, bool allowSmartExplanation)
        {
            string reason = "";
            if (privacyGateway == null || !privacyGateway.TryValidate(summary, out reason))
            {
                return LocalTemplateFallback.Generate(summary, string.IsNullOrEmpty(reason) ? "privacy_gateway_missing" : reason);
            }

            return LocalTemplateFallback.Generate(privacyGateway.Sanitize(summary), allowSmartExplanation ? "immediate_preview" : "smart_explanation_off");
        }

        private IEnumerator CoMockModelFeedback(BehaviorFeatureSummary summary, Action<FocusFeedback> onComplete)
        {
            float safeTimeoutSeconds = Mathf.Max(0.05f, timeoutSeconds);
            if (simulatedLatencySeconds > safeTimeoutSeconds)
            {
                yield return new WaitForSecondsRealtime(safeTimeoutSeconds);
                Complete(LocalTemplateFallback.Generate(summary, "llm_timeout"), onComplete);
                yield break;
            }

            float waitSeconds = Mathf.Max(0f, simulatedLatencySeconds);
            yield return new WaitForSecondsRealtime(waitSeconds);

            FocusFeedbackLlmOutput output = new FocusFeedbackLlmOutput();
            output.schema_version = FocusFeedbackLlmOutput.ExpectedSchemaVersion;
            output.confidence = Mathf.Clamp01(0.68f + summary.focusScoreAvg01 * 0.24f);
            output.safety = new FocusFeedbackSafety();

            if (summary.interruptCount >= 3)
            {
                output.bubble_text = "停顿多一点也没关系，回来就好。";
                output.record_summary = "本轮有几次短暂停顿，但你没有放弃，下一轮可以先选更轻的目标。";
                output.tone = "quiet";
                output.reaction_hint = "head_tilt_listen";
            }
            else if (summary.focusScoreAvg01 >= 0.75f)
            {
                output.bubble_text = "刚才这段很稳，我会继续安静陪你。";
                output.record_summary = "本轮节奏稳定，猫咪已降低动作频率，陪你完成这段专注。";
                output.tone = "warm";
                output.reaction_hint = "tail_wag_happy";
            }
            else
            {
                output.bubble_text = "节奏已经慢下来了，我们继续来。";
                output.record_summary = "这段专注开始稳定，后续适合保持轻互动和低打扰陪伴。";
                output.tone = "encouraging";
                output.reaction_hint = "idle_breath";
            }

            FocusFeedback feedback;
            string reason;
            if (!FocusFeedbackLlmOutput.TryBuildFeedback(output, "mock_llm_structured", out feedback, out reason))
            {
                Complete(LocalTemplateFallback.Generate(summary, reason), onComplete);
                yield break;
            }

            Complete(feedback, onComplete);
        }

        private void Complete(FocusFeedback feedback, Action<FocusFeedback> onComplete)
        {
            LastSource = feedback != null ? feedback.source : "local_template";
            LastSafetyReason = feedback != null ? feedback.safetyReason : "empty_feedback";
            if (onComplete != null)
            {
                onComplete(feedback ?? FocusFeedback.Create("", "local_template", true, "empty_feedback"));
            }
        }
    }
}
