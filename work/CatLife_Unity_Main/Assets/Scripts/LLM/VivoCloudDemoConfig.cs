using System;
using UnityEngine;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class VivoCloudDemoConfig
    {
        private const string ResourcePath = "CatLifePrivate/vivo_cloud_credentials";

        public string appId = "2026414599";
        public string appKey = "";
        public string apiEndpoint = "https://api-ai.vivo.com.cn/v1/chat/completions";
        public string model = "Doubao-Seed-2.0-mini";
        public bool enableDirectCloudApi = true;

        public bool HasUsableCloudCredentials
        {
            get
            {
                return enableDirectCloudApi &&
                    !string.IsNullOrEmpty(appId) &&
                    !string.IsNullOrEmpty(appKey) &&
                    !IsPlaceholderAppKey(appKey) &&
                    !string.IsNullOrEmpty(apiEndpoint) &&
                    !string.IsNullOrEmpty(model) &&
                    apiEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string RedactedAppId
        {
            get
            {
                if (string.IsNullOrEmpty(appId))
                {
                    return "missing_app_id";
                }

                string trimmed = appId.Trim();
                if (trimmed.Length <= 4)
                {
                    return "****";
                }

                return trimmed.Substring(0, 2) + "****" + trimmed.Substring(trimmed.Length - 2);
            }
        }

        public static bool IsPlaceholderAppKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            string trimmed = value.Trim();
            return trimmed == "DO_NOT_COMMIT_REAL_APP_KEY" ||
                   trimmed == "REPLACE_WITH_LOCAL_PRIVATE_KEY" ||
                   trimmed == "YOUR_APP_KEY" ||
                   trimmed.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   trimmed.IndexOf("example", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static VivoCloudDemoConfig Load()
        {
            VivoCloudDemoConfig config = new VivoCloudDemoConfig();
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrEmpty(asset.text))
            {
                return config;
            }

            try
            {
                JsonUtility.FromJsonOverwrite(asset.text, config);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CatLife] vivo cloud demo config parse failed: " + ex.GetType().Name);
            }

            config.appId = string.IsNullOrEmpty(config.appId) ? "2026414599" : config.appId.Trim();
            config.appKey = string.IsNullOrEmpty(config.appKey) ? "" : config.appKey.Trim();
            config.apiEndpoint = string.IsNullOrEmpty(config.apiEndpoint)
                ? "https://api-ai.vivo.com.cn/v1/chat/completions"
                : config.apiEndpoint.Trim();
            config.model = string.IsNullOrEmpty(config.model) ? "Doubao-Seed-2.0-mini" : config.model.Trim();
            return config;
        }
    }
}
