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
                    !string.IsNullOrEmpty(apiEndpoint) &&
                    !string.IsNullOrEmpty(model);
            }
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
