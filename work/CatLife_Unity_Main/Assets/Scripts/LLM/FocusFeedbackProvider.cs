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
            float waitSeconds = Mathf.Min(Mathf.Max(0f, simulatedLatencySeconds), Mathf.Max(0.05f, timeoutSeconds));
            yield return new WaitForSecondsRealtime(waitSeconds);

            string text;
            if (summary.interruptCount >= 3)
            {
                text = "这轮有几次短暂停顿，但你没有放弃。猫咪建议下一轮先选更轻的目标。";
            }
            else if (summary.focusScoreAvg01 >= 0.75f)
            {
                text = "刚才这段很稳，猫咪已经安静陪你走完了一轮专注。";
            }
            else
            {
                text = "这段节奏已经开始稳定了，猫咪会继续陪你慢慢进入状态。";
            }

            Complete(FocusFeedback.Create(ClampText(text, 60), "mock_llm", false, "privacy_passed"), onComplete);
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

        private static string ClampText(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            return text.Length <= maxChars ? text : text.Substring(0, maxChars);
        }
    }
}
