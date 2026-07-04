using System;
using UnityEngine;

namespace CatLife.LLM
{
    [Serializable]
    public sealed class BlueLmAndroidEvent
    {
        public string schemaVersion;
        public string requestId;
        public bool ok;
        public bool success;
        public string status;
        public string content;
        public string message;
        public string responseJson;
        public string error;
        public LLMBehaviorSuggestion suggestion;

        public bool IsSuccess
        {
            get
            {
                return ok ||
                    success ||
                    string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string ErrorText
        {
            get
            {
                if (!string.IsNullOrEmpty(error))
                {
                    return error;
                }

                if (!string.IsNullOrEmpty(message) && !IsSuccess)
                {
                    return message;
                }

                return "bluelm_android_event_failed";
            }
        }

        public static bool TryParse(string json, out BlueLmAndroidEvent androidEvent, out string reason)
        {
            androidEvent = null;
            if (string.IsNullOrEmpty(json))
            {
                reason = "android_event_empty";
                return false;
            }

            try
            {
                androidEvent = JsonUtility.FromJson<BlueLmAndroidEvent>(json);
            }
            catch (Exception ex)
            {
                reason = "android_event_parse_" + ex.GetType().Name;
                return false;
            }

            if (androidEvent == null || string.IsNullOrEmpty(androidEvent.requestId))
            {
                reason = "android_event_missing_request_id";
                return false;
            }

            reason = "passed";
            return true;
        }

        public bool TryBuildSuggestion(out LLMBehaviorSuggestion safeSuggestion, out string reason)
        {
            safeSuggestion = null;
            if (!IsSuccess)
            {
                reason = ErrorText;
                return false;
            }

            LLMBehaviorSuggestion raw = suggestion;
            if (raw == null)
            {
                string payload = FirstPayload();
                string suggestionJson = ExtractJsonObject(payload);
                if (string.IsNullOrEmpty(suggestionJson))
                {
                    reason = "android_event_missing_suggestion_json";
                    return false;
                }

                try
                {
                    raw = JsonUtility.FromJson<LLMBehaviorSuggestion>(suggestionJson);
                }
                catch (Exception ex)
                {
                    reason = "android_suggestion_parse_" + ex.GetType().Name;
                    return false;
                }
            }

            return LLMBehaviorSuggestion.TryBuildSafe(raw, out safeSuggestion, out reason);
        }

        private string FirstPayload()
        {
            if (!string.IsNullOrEmpty(content))
            {
                return content;
            }

            if (!string.IsNullOrEmpty(responseJson))
            {
                return responseJson;
            }

            return message ?? string.Empty;
        }

        private static string ExtractJsonObject(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return string.Empty;
            }

            return text.Substring(start, end - start + 1);
        }
    }
}
