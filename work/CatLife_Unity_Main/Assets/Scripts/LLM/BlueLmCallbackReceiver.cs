using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatLife.LLM
{
    public sealed class BlueLmCallbackReceiver : MonoBehaviour
    {
        public const string DefaultGameObjectName = "CatLifeBlueLmCallbackReceiver";

        private static readonly Dictionary<string, Action<BlueLmAndroidEvent>> Pending =
            new Dictionary<string, Action<BlueLmAndroidEvent>>();

        public static BlueLmCallbackReceiver EnsureReceiver(string gameObjectName)
        {
            string safeName = string.IsNullOrEmpty(gameObjectName)
                ? DefaultGameObjectName
                : gameObjectName;

            GameObject existing = GameObject.Find(safeName);
            if (existing == null)
            {
                existing = new GameObject(safeName);
                DontDestroyOnLoad(existing);
            }

            BlueLmCallbackReceiver receiver = existing.GetComponent<BlueLmCallbackReceiver>();
            if (receiver == null)
            {
                receiver = existing.AddComponent<BlueLmCallbackReceiver>();
            }

            return receiver;
        }

        public static void RegisterPending(string requestId, Action<BlueLmAndroidEvent> callback)
        {
            if (string.IsNullOrEmpty(requestId) || callback == null)
            {
                return;
            }

            Pending[requestId] = callback;
        }

        public static void UnregisterPending(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            Pending.Remove(requestId);
        }

        public void OnBlueLmEvent(string json)
        {
            BlueLmAndroidEvent androidEvent;
            string reason;
            if (!BlueLmAndroidEvent.TryParse(json, out androidEvent, out reason))
            {
                Debug.LogWarning("[CatLife] BlueLM callback rejected: " + reason);
                return;
            }

            Action<BlueLmAndroidEvent> callback;
            if (!Pending.TryGetValue(androidEvent.requestId, out callback))
            {
                Debug.LogWarning("[CatLife] BlueLM callback without pending request: " + androidEvent.requestId);
                return;
            }

            Pending.Remove(androidEvent.requestId);
            callback(androidEvent);
        }
    }
}
