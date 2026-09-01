using System;
using UnityEngine;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class GenericCloudConfig
    {
        private const string ResourcePath = "CatLifePrivate/generic_cloud_credentials";

        public string provider = "mimo";
        public string apiKey = "";
        public string apiEndpoint = "https://api.xiaomimimo.com/v1/chat/completions";
        public string model = "mimo-v2.5";
        public bool enableDirectCloudApi = true;

        public bool HasUsableCloudCredentials
        {
            get
            {
                return enableDirectCloudApi &&
                    !string.IsNullOrEmpty(apiKey) &&
                    !IsPlaceholderApiKey(apiKey) &&
                    !string.IsNullOrEmpty(apiEndpoint) &&
                    !string.IsNullOrEmpty(model) &&
                    apiEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsPlaceholderApiKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return true;
            string trimmed = value.Trim();
            return trimmed == "DO_NOT_COMMIT_REAL_API_KEY" ||
                   trimmed == "REPLACE_WITH_LOCAL_PRIVATE_KEY" ||
                   trimmed == "YOUR_API_KEY";
        }

        public static GenericCloudConfig Load()
        {
            GenericCloudConfig config = new GenericCloudConfig();
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrEmpty(asset.text)) return config;

            try
            {
                JsonUtility.FromJsonOverwrite(asset.text, config);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CatLife] generic cloud config parse failed: " + ex.GetType().Name);
            }

            config.provider = string.IsNullOrEmpty(config.provider) ? "mimo" : config.provider.Trim().ToLowerInvariant();
            config.apiKey = string.IsNullOrEmpty(config.apiKey) ? "" : config.apiKey.Trim();
            config.apiEndpoint = string.IsNullOrEmpty(config.apiEndpoint)
                ? "https://api.xiaomimimo.com/v1/chat/completions"
                : config.apiEndpoint.Trim();
            config.model = string.IsNullOrEmpty(config.model) ? "mimo-v2.5" : config.model.Trim();
            return config;
        }
    }
}
