using System;
using System.Collections;
using UnityEngine;

namespace CatLife.LLM
{
    public sealed class MockCatLLMClient : MonoBehaviour, ICatLLMClient
    {
        [SerializeField] private bool enableClient = true;
        [SerializeField] private float simulatedLatencySeconds = 0.15f;

        public bool Enabled { get { return enableClient; } }
        public bool IsBusy { get; private set; }

        public void RequestSuggestion(
            CatPromptContext context,
            CatPromptBuilder builder,
            Action<LLMBehaviorSuggestion> onSuccess,
            Action<string> onError)
        {
            if (!Enabled)
            {
                if (onError != null)
                {
                    onError("MockCatLLMClient disabled.");
                }

                return;
            }

            if (IsBusy)
            {
                if (onError != null)
                {
                    onError("MockCatLLMClient busy.");
                }

                return;
            }

            StartCoroutine(CoRespond(context, onSuccess));
        }

        private IEnumerator CoRespond(CatPromptContext context, Action<LLMBehaviorSuggestion> onSuccess)
        {
            IsBusy = true;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, simulatedLatencySeconds));

            LLMBehaviorSuggestion suggestion = new LLMBehaviorSuggestion();
            if (context != null && context.focusState == "Focused")
            {
                suggestion.suggestedLine = "I will stay quiet nearby.";
                suggestion.moodBias = "calm";
                suggestion.roamWeightBias = -0.1f;
                suggestion.quietIdleWeightBias = 0.2f;
                suggestion.socialResponseWeightBias = -0.12f;
                suggestion.showBubble = false;
            }
            else if (context != null && context.userIntent == "WantsInteraction")
            {
                suggestion.suggestedLine = "I am here with you.";
                suggestion.moodBias = "affectionate";
                suggestion.roamWeightBias = -0.05f;
                suggestion.quietIdleWeightBias = -0.1f;
                suggestion.socialResponseWeightBias = 0.22f;
                suggestion.showBubble = true;
            }
            else
            {
                suggestion.suggestedLine = "";
                suggestion.moodBias = "curious";
                suggestion.roamWeightBias = 0.08f;
                suggestion.quietIdleWeightBias = 0f;
                suggestion.socialResponseWeightBias = 0.04f;
                suggestion.showBubble = false;
            }

            IsBusy = false;
            if (onSuccess != null)
            {
                onSuccess(LLMBehaviorSuggestion.ClampToWhitelist(suggestion));
            }
        }
    }
}
