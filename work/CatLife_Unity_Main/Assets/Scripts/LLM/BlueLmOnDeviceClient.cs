using System;
using System.Collections;
using UnityEngine;

namespace CatLife.LLM
{
    [DisallowMultipleComponent]
    public sealed class BlueLmOnDeviceClient : MonoBehaviour, ICatLLMClient
    {
        [SerializeField] private bool enableClient = true;
        [SerializeField] private float timeoutSeconds = 2.5f;
#pragma warning disable 0414
        [SerializeField] private string androidBridgeClass = "com.catlife.bluelm.BlueLmUnityBridge";
        [SerializeField] private string androidGenerateMethod = "generate";
        [SerializeField] private string androidInitMethod = "init";
        [SerializeField] private string modelPath = "/sdcard/1225/1.7.0.4_1225_mtk9500";
#pragma warning restore 0414
        [SerializeField] private string callbackGameObjectName = BlueLmCallbackReceiver.DefaultGameObjectName;
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private PrivacyGateway privacyGateway;
        [SerializeField] private bool logLifecycle = true;

        public bool Enabled
        {
            get { return enableClient && IsAndroidRuntime(); }
        }

        public bool IsBusy { get; private set; }
        public string LastSource { get; private set; } = "local_template";
        public string LastFailureReason { get; private set; } = "not_requested";

        private void Awake()
        {
            ResolvePrivacyGateway();
            if (initializeOnAwake)
            {
                TryInitializeAndroidBridge();
            }
        }

        public void RequestSuggestion(
            CatPromptContext context,
            CatPromptBuilder builder,
            Action<LLMBehaviorSuggestion> onSuccess,
            Action<string> onError)
        {
            if (IsBusy)
            {
                CompleteFallback(context, "bluelm_client_busy", onSuccess);
                return;
            }

            ResolvePrivacyGateway();
            string privacyReason = string.Empty;
            CatPromptContext safeContext = privacyGateway != null ? privacyGateway.Sanitize(context) : context;
            if (privacyGateway == null || !privacyGateway.TryValidate(safeContext, out privacyReason))
            {
                CompleteFallback(safeContext, string.IsNullOrEmpty(privacyReason) ? "privacy_gateway_missing" : privacyReason, onSuccess);
                return;
            }

            if (!Enabled)
            {
                CompleteFallback(safeContext, IsAndroidRuntime() ? "bluelm_disabled" : "bluelm_not_android_runtime", onSuccess);
                return;
            }

            StartCoroutine(CoRequest(safeContext, builder ?? new CatPromptBuilder(), onSuccess, onError));
        }

        public static LLMBehaviorSuggestion BuildLocalFallbackSuggestion(CatPromptContext context, string reason)
        {
            LLMBehaviorSuggestion suggestion = new LLMBehaviorSuggestion();
            bool focused = context != null && (context.focusSessionActive || context.focusState == "Focused");
            if (focused)
            {
                suggestion.suggestedLine = string.Empty;
                suggestion.moodBias = "calm";
                suggestion.roamWeightBias = -0.18f;
                suggestion.quietIdleWeightBias = 0.22f;
                suggestion.socialResponseWeightBias = -0.16f;
                suggestion.showBubble = false;
            }
            else if (context != null && context.userIntent == "WantsInteraction")
            {
                suggestion.suggestedLine = "I am here with you.";
                suggestion.moodBias = "affectionate";
                suggestion.roamWeightBias = -0.04f;
                suggestion.quietIdleWeightBias = -0.08f;
                suggestion.socialResponseWeightBias = 0.18f;
                suggestion.showBubble = true;
            }
            else
            {
                suggestion.suggestedLine = string.Empty;
                suggestion.moodBias = "curious";
                suggestion.roamWeightBias = 0.06f;
                suggestion.quietIdleWeightBias = 0f;
                suggestion.socialResponseWeightBias = 0.03f;
                suggestion.showBubble = false;
            }

            return LLMBehaviorSuggestion.ClampToWhitelist(suggestion);
        }

        private IEnumerator CoRequest(
            CatPromptContext context,
            CatPromptBuilder builder,
            Action<LLMBehaviorSuggestion> onSuccess,
            Action<string> onError)
        {
            IsBusy = true;
            string requestId = Guid.NewGuid().ToString("N");
            BlueLmUnityRequest request = BlueLmUnityRequest.Create(requestId, context, builder, timeoutSeconds);
            BlueLmCallbackReceiver.EnsureReceiver(callbackGameObjectName);

            bool completed = false;
            LLMBehaviorSuggestion result = null;
            string failureReason = string.Empty;
            string resultSource = "local_template";

            BlueLmCallbackReceiver.RegisterPending(
                requestId,
                androidEvent =>
                {
                    LLMBehaviorSuggestion safeSuggestion;
                    string reason;
                    if (androidEvent.TryBuildSuggestion(out safeSuggestion, out reason))
                    {
                        result = safeSuggestion;
                        resultSource = string.IsNullOrEmpty(androidEvent.source) ? "bluelm_on_device" : androidEvent.source;
                        failureReason = "passed";
                    }
                    else
                    {
                        failureReason = reason;
                        resultSource = string.IsNullOrEmpty(androidEvent.source) ? "local_template" : androidEvent.source;
                    }

                    completed = true;
                });

            string sendError;
            if (!TrySendToAndroid(request.ToJson(), out sendError))
            {
                BlueLmCallbackReceiver.UnregisterPending(requestId);
                CompleteFallback(context, sendError, onSuccess);
                NotifyError(onError, sendError);
                IsBusy = false;
                yield break;
            }

            if (logLifecycle)
            {
                Debug.Log("[CatLife] BlueLM request sent: " + requestId);
            }

            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, timeoutSeconds);
            while (!completed && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!completed)
            {
                failureReason = "bluelm_timeout";
                BlueLmCallbackReceiver.UnregisterPending(requestId);
            }

            IsBusy = false;
            if (result != null)
            {
                LastSource = resultSource;
                LastFailureReason = string.IsNullOrEmpty(failureReason) ? "passed" : failureReason;
                if (onSuccess != null)
                {
                    onSuccess(result);
                }

                yield break;
            }

            CompleteFallback(context, failureReason, onSuccess);
            NotifyError(onError, failureReason);
        }

        private bool TrySendToAndroid(string requestJson, out string reason)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass bridge = new AndroidJavaClass(androidBridgeClass))
                {
                    bridge.CallStatic(androidGenerateMethod, requestJson, callbackGameObjectName);
                }

                reason = "sent";
                return true;
            }
            catch (Exception ex)
            {
                reason = "bluelm_android_bridge_" + ex.GetType().Name;
                return false;
            }
#else
            reason = "bluelm_not_android_runtime";
            return false;
#endif
        }

        private bool TryInitializeAndroidBridge()
        {
            if (!Enabled)
            {
                return false;
            }

            BlueLmCallbackReceiver.EnsureReceiver(callbackGameObjectName);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass bridge = new AndroidJavaClass(androidBridgeClass))
                {
                    bridge.CallStatic(androidInitMethod, callbackGameObjectName, modelPath);
                }

                if (logLifecycle)
                {
                    Debug.Log("[CatLife] BlueLM init requested: " + modelPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CatLife] BlueLM init bridge failed: " + ex.GetType().Name);
                return false;
            }
#else
            return false;
#endif
        }

        private void CompleteFallback(
            CatPromptContext context,
            string reason,
            Action<LLMBehaviorSuggestion> onSuccess)
        {
            if (logLifecycle)
            {
                Debug.Log("[CatLife] BlueLM fallback: " + (string.IsNullOrEmpty(reason) ? "unknown" : reason));
            }

            LastSource = "local_template";
            LastFailureReason = string.IsNullOrEmpty(reason) ? "unknown" : reason;
            if (onSuccess != null)
            {
                onSuccess(BuildLocalFallbackSuggestion(context, reason));
            }
        }

        private static void NotifyError(Action<string> onError, string reason)
        {
            if (onError != null)
            {
                onError(string.IsNullOrEmpty(reason) ? "bluelm_unknown_error" : reason);
            }
        }

        private void ResolvePrivacyGateway()
        {
            if (privacyGateway != null)
            {
                return;
            }

            privacyGateway = GetComponent<PrivacyGateway>();
            if (privacyGateway == null)
            {
                privacyGateway = FindAnyObjectByType<PrivacyGateway>();
            }
        }

        private static bool IsAndroidRuntime()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }
}
