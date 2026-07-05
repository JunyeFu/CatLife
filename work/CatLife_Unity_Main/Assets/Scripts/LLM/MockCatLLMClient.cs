using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CatLife.LLM
{
    public sealed class MockCatLLMClient : MonoBehaviour, ICatLLMClient
    {
        [SerializeField] private bool enableClient = true;
        [SerializeField] private float simulatedLatencySeconds = 0.15f;
        [SerializeField] private bool preferVivoCloudWhenConfigured = true;
        [SerializeField] private int cloudTimeoutSeconds = 8;

        public bool Enabled { get { return enableClient; } }
        public bool IsBusy { get; private set; }
        public string LastSource { get; private set; } = "not_requested";
        public string LastFailureReason { get; private set; } = "";
        public string LastCloudRequestId { get; private set; } = "";
        public long LastCloudStatusCode { get; private set; }
        public string LastCloudAppIdRedacted { get; private set; } = "missing_app_id";
        public bool LastCloudConfigUsable { get; private set; }

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

            VivoCloudDemoConfig config = VivoCloudDemoConfig.Load();
            LastCloudAppIdRedacted = config.RedactedAppId;
            LastCloudConfigUsable = config.HasUsableCloudCredentials;
            LastCloudStatusCode = 0;
            LastCloudRequestId = "";
            LastFailureReason = "";
            LastSource = LastCloudConfigUsable ? "vivo_cloud_pending" : "local_template";
            Debug.Log("[CatLife] llm_request llm_source=" + LastSource +
                " app_id=" + LastCloudAppIdRedacted +
                " cloud_config_usable=" + LastCloudConfigUsable);
            if (preferVivoCloudWhenConfigured && config.HasUsableCloudCredentials)
            {
                bool cloudCompleted = false;
                LLMBehaviorSuggestion cloudSuggestion = null;
                string cloudError = "";
                yield return StartCoroutine(CoRequestVivoCloud(
                    config,
                    context,
                    new CatPromptBuilder(),
                    suggestion =>
                    {
                        cloudSuggestion = suggestion;
                        cloudCompleted = true;
                    },
                    error =>
                    {
                        cloudError = error;
                        cloudCompleted = true;
                    }));

                if (cloudCompleted && cloudSuggestion != null)
                {
                    IsBusy = false;
                    LastSource = "vivo_cloud";
                    LastFailureReason = "";
                    Debug.Log("[CatLife] llm_result llm_source=vivo_cloud status_code=" + LastCloudStatusCode +
                        " request_id=" + LastCloudRequestId);
                    if (onSuccess != null)
                    {
                        onSuccess(LLMBehaviorSuggestion.ClampToWhitelist(cloudSuggestion));
                    }

                    yield break;
                }

                if (!string.IsNullOrEmpty(cloudError))
                {
                    LastSource = "local_template";
                    LastFailureReason = cloudError;
                    Debug.LogWarning("[CatLife] llm_result llm_source=local_template fallback=vivo_cloud_error llm_error=" + cloudError);
                }
            }
            else if (preferVivoCloudWhenConfigured)
            {
                LastFailureReason = config.enableDirectCloudApi ? "vivo_cloud_credentials_missing_or_placeholder" : "vivo_cloud_disabled";
                Debug.Log("[CatLife] llm_result llm_source=local_template fallback=" + LastFailureReason);
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, simulatedLatencySeconds));

            LLMBehaviorSuggestion suggestion = new LLMBehaviorSuggestion();
            if (context != null && (context.focusSessionActive || context.focusState == "Focused"))
            {
                suggestion.suggestedLine = "I will stay quiet nearby.";
                suggestion.moodBias = "calm";
                suggestion.roamWeightBias = context.distraction01 > 0.5f ? -0.26f : -0.12f;
                suggestion.quietIdleWeightBias = context.focusScore01 > 0.65f ? 0.28f : 0.16f;
                suggestion.socialResponseWeightBias = context.arousal01 > 0.45f ? -0.24f : -0.12f;
                suggestion.showBubble = false;
            }
            else if (context != null && context.distraction01 >= 0.55f)
            {
                suggestion.suggestedLine = "Let's take it slowly.";
                suggestion.moodBias = "calm";
                suggestion.roamWeightBias = -0.18f;
                suggestion.quietIdleWeightBias = 0.18f;
                suggestion.socialResponseWeightBias = -0.08f;
                suggestion.showBubble = false;
            }
            else if (context != null &&
                context.hasSceneInteraction &&
                context.secondsSinceSceneInteraction <= 12f)
            {
                ApplySceneInteractionBias(context, suggestion);
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
            if (LastSource != "vivo_cloud")
            {
                LastSource = "local_template";
            }
            Debug.Log("[CatLife] llm_result llm_source=" + LastSource +
                " fallback_reason=" + (string.IsNullOrEmpty(LastFailureReason) ? "none" : LastFailureReason) +
                " focus=" + (context != null && (context.focusSessionActive || context.focusState == "Focused")));

            if (onSuccess != null)
            {
                onSuccess(LLMBehaviorSuggestion.ClampToWhitelist(suggestion));
            }
        }

        private IEnumerator CoRequestVivoCloud(
            VivoCloudDemoConfig config,
            CatPromptContext context,
            CatPromptBuilder builder,
            Action<LLMBehaviorSuggestion> onSuccess,
            Action<string> onError)
        {
            string requestId = Guid.NewGuid().ToString("N");
            LastCloudRequestId = requestId;
            LastCloudStatusCode = 0;
            LastCloudAppIdRedacted = config.RedactedAppId;
            VivoChatRequest body = VivoChatRequest.Create(config.model, builder, context);
            string bodyJson = JsonUtility.ToJson(body);

            using (UnityWebRequest req = new UnityWebRequest(config.apiEndpoint, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] payload = Encoding.UTF8.GetBytes(bodyJson);
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = Mathf.Max(1, cloudTimeoutSeconds);
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + config.appKey);
                req.SetRequestHeader("requestId", requestId);

                yield return req.SendWebRequest();
                LastCloudStatusCode = req.responseCode;

                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (onError != null)
                    {
                        onError("vivo_cloud_network_" + req.responseCode + "_" + req.error);
                    }

                    yield break;
                }

                VivoChatResponse response = null;
                try
                {
                    response = JsonUtility.FromJson<VivoChatResponse>(req.downloadHandler.text);
                }
                catch (Exception ex)
                {
                    if (onError != null)
                    {
                        onError("vivo_cloud_response_parse_" + ex.GetType().Name);
                    }

                    yield break;
                }

                string content = response != null ? response.FirstContent() : "";
                string suggestionJson = ExtractJsonObject(content);
                if (string.IsNullOrEmpty(suggestionJson))
                {
                    if (onError != null)
                    {
                        onError("vivo_cloud_missing_suggestion_json");
                    }

                    yield break;
                }

                LLMBehaviorSuggestion suggestion = null;
                try
                {
                    suggestion = JsonUtility.FromJson<LLMBehaviorSuggestion>(suggestionJson);
                }
                catch (Exception ex)
                {
                    if (onError != null)
                    {
                        onError("vivo_cloud_suggestion_parse_" + ex.GetType().Name);
                    }

                    yield break;
                }

                LLMBehaviorSuggestion safeSuggestion;
                string reason;
                if (!LLMBehaviorSuggestion.TryBuildSafe(suggestion, out safeSuggestion, out reason))
                {
                    if (onError != null)
                    {
                        onError("vivo_cloud_unsafe_output_" + reason);
                    }

                    yield break;
                }

                if (onSuccess != null)
                {
                    onSuccess(safeSuggestion);
                }
            }
        }

        private static string ExtractJsonObject(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return "";
            }

            return text.Substring(start, end - start + 1);
        }

        private static void ApplySceneInteractionBias(
            CatPromptContext context,
            LLMBehaviorSuggestion suggestion)
        {
            suggestion.showBubble = false;
            suggestion.socialResponseWeightBias = 0.04f;

            if (HasSceneTag(context, "quiet") || HasSceneTag(context, "home"))
            {
                suggestion.suggestedLine = "";
                suggestion.moodBias = "calm";
                suggestion.roamWeightBias = -0.08f;
                suggestion.quietIdleWeightBias = 0.18f;
                return;
            }

            if (HasSceneTag(context, "garden") || HasSceneTag(context, "sniff"))
            {
                suggestion.suggestedLine = "";
                suggestion.moodBias = "curious";
                suggestion.roamWeightBias = 0.14f;
                suggestion.quietIdleWeightBias = -0.06f;
                return;
            }

            if (HasSceneTag(context, "plaza") || HasSceneTag(context, "walk"))
            {
                suggestion.suggestedLine = "";
                suggestion.moodBias = "curious";
                suggestion.roamWeightBias = 0.1f;
                suggestion.quietIdleWeightBias = -0.04f;
                return;
            }

            suggestion.suggestedLine = "";
            suggestion.moodBias = "calm";
            suggestion.roamWeightBias = 0.02f;
            suggestion.quietIdleWeightBias = 0.02f;
        }

        private static bool HasSceneTag(CatPromptContext context, string tag)
        {
            if (context == null || context.sceneInteractionTags == null || string.IsNullOrEmpty(tag))
            {
                return false;
            }

            for (int i = 0; i < context.sceneInteractionTags.Length; i++)
            {
                if (string.Equals(context.sceneInteractionTags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        [Serializable]
        private sealed class VivoChatRequest
        {
            public string model;
            public VivoChatMessage[] messages;
            public bool stream;
            public float temperature;
            public float top_p;
            public int max_tokens;

            public static VivoChatRequest Create(string model, CatPromptBuilder builder, CatPromptContext context)
            {
                return new VivoChatRequest
                {
                    model = model,
                    stream = false,
                    temperature = 0f,
                    top_p = 1f,
                    max_tokens = 256,
                    messages = new[]
                    {
                        new VivoChatMessage
                        {
                            role = "system",
                            content = builder.BuildSystemPrompt() + "\n" + builder.BuildDeveloperPrompt()
                        },
                        new VivoChatMessage
                        {
                            role = "user",
                            content = builder.BuildUserContextPrompt(context) + "\nReturn only JSON matching:\n" + builder.BuildOutputJsonSchemaPrompt()
                        }
                    }
                };
            }
        }

        [Serializable]
        private sealed class VivoChatMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class VivoChatResponse
        {
            public VivoChatChoice[] choices;

            public string FirstContent()
            {
                if (choices == null || choices.Length == 0 || choices[0] == null || choices[0].message == null)
                {
                    return "";
                }

                return choices[0].message.content ?? "";
            }
        }

        [Serializable]
        private sealed class VivoChatChoice
        {
            public VivoChatMessage message;
        }
    }
}
