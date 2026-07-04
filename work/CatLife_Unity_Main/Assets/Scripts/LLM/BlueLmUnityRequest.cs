using System;
using UnityEngine;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class BlueLmUnityRequest
    {
        public string schemaVersion = "catlife.bluelm.request.v1";
        public string requestId;
        public long createdAtUtcTicks;
        public int timeoutMs;
        public string systemPrompt;
        public string developerPrompt;
        public string userContextJson;
        public string outputSchemaJson;

        public static BlueLmUnityRequest Create(
            string requestId,
            CatPromptContext context,
            CatPromptBuilder builder,
            float timeoutSeconds)
        {
            CatPromptBuilder safeBuilder = builder ?? new CatPromptBuilder();
            return new BlueLmUnityRequest
            {
                requestId = string.IsNullOrEmpty(requestId) ? Guid.NewGuid().ToString("N") : requestId,
                createdAtUtcTicks = DateTime.UtcNow.Ticks,
                timeoutMs = Mathf.RoundToInt(Mathf.Max(0.1f, timeoutSeconds) * 1000f),
                systemPrompt = safeBuilder.BuildSystemPrompt(),
                developerPrompt = safeBuilder.BuildDeveloperPrompt(),
                userContextJson = safeBuilder.BuildUserContextPrompt(context),
                outputSchemaJson = safeBuilder.BuildOutputJsonSchemaPrompt()
            };
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }
}
