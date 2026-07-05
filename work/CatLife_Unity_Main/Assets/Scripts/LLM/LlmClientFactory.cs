using System;
using UnityEngine;

namespace CatLife.LLM
{
    [DisallowMultipleComponent]
    public sealed class LlmClientFactory : MonoBehaviour, ICatLLMClient
    {
        [SerializeField] private LlmRuntimeMode runtimeMode = LlmRuntimeMode.Auto;
        [SerializeField] private MonoBehaviour fallbackClientComponent;
        [SerializeField] private BlueLmOnDeviceClient blueLmOnDeviceClient;
        [SerializeField] private bool localFallbackWhenNoClient = true;

        public bool Enabled
        {
            get { return true; }
        }

        public bool IsBusy
        {
            get
            {
                ICatLLMClient client = ResolveSelectedClient();
                return client != null && client.IsBusy;
            }
        }

        private void Awake()
        {
            ResolveClients();
        }

        public void RequestSuggestion(
            CatPromptContext context,
            CatPromptBuilder builder,
            Action<LLMBehaviorSuggestion> onSuccess,
            Action<string> onError)
        {
            ResolveClients();
            if (runtimeMode == LlmRuntimeMode.LocalTemplateOnly)
            {
                Debug.Log("[CatLife] llm_factory llm_source=local_template fallback=runtime_mode_local_template");
                CompleteLocalFallback(context, "runtime_mode_local_template", onSuccess);
                return;
            }

            ICatLLMClient selected = ResolveSelectedClient();
            if (selected != null && selected.Enabled)
            {
                Debug.Log("[CatLife] llm_factory route=" + selected.GetType().Name + " runtime_mode=" + runtimeMode);
                selected.RequestSuggestion(context, builder, onSuccess, onError);
                return;
            }

            ICatLLMClient fallback = ResolveFallbackClient();
            if (fallback != null && fallback.Enabled)
            {
                Debug.Log("[CatLife] llm_factory route=" + fallback.GetType().Name + " fallback=selected_unavailable");
                fallback.RequestSuggestion(context, builder, onSuccess, onError);
                return;
            }

            if (localFallbackWhenNoClient)
            {
                Debug.Log("[CatLife] llm_factory llm_source=local_template fallback=llm_factory_no_available_client");
                CompleteLocalFallback(context, "llm_factory_no_available_client", onSuccess);
                return;
            }

            if (onError != null)
            {
                onError("llm_factory_no_available_client");
            }
        }

        private ICatLLMClient ResolveSelectedClient()
        {
            switch (runtimeMode)
            {
                case LlmRuntimeMode.BlueLmOnDevice:
                    return blueLmOnDeviceClient;
                case LlmRuntimeMode.MockOrVivoCloud:
                    return ResolveFallbackClient();
                case LlmRuntimeMode.Auto:
                    if (blueLmOnDeviceClient != null && blueLmOnDeviceClient.Enabled)
                    {
                        return blueLmOnDeviceClient;
                    }

                    return ResolveFallbackClient();
                default:
                    return null;
            }
        }

        private ICatLLMClient ResolveFallbackClient()
        {
            return fallbackClientComponent as ICatLLMClient;
        }

        private void ResolveClients()
        {
            if (blueLmOnDeviceClient == null)
            {
                blueLmOnDeviceClient = GetComponent<BlueLmOnDeviceClient>();
            }

            if (fallbackClientComponent == null)
            {
                fallbackClientComponent = GetComponent<MockCatLLMClient>();
            }

            if (fallbackClientComponent == this)
            {
                fallbackClientComponent = null;
            }
        }

        private static void CompleteLocalFallback(
            CatPromptContext context,
            string reason,
            Action<LLMBehaviorSuggestion> onSuccess)
        {
            if (onSuccess != null)
            {
                onSuccess(BlueLmOnDeviceClient.BuildLocalFallbackSuggestion(context, reason));
            }
        }
    }
}
