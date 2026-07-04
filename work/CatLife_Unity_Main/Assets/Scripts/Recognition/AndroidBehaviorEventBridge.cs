using UnityEngine;

namespace CatLife.Recognition
{
    [DisallowMultipleComponent]
    public sealed class AndroidBehaviorEventBridge : MonoBehaviour
    {
        public const string DefaultGameObjectName = "CatLifeAndroidBehaviorEventBridge";

        [SerializeField] private RealtimeFeatureEngine featureEngine;
        [SerializeField] private bool createOnLoad = true;
        [SerializeField] private bool logEvents;

        public static AndroidBehaviorEventBridge Instance { get; private set; }

        public string LastAcceptedEventType { get; private set; } = "none";
        public string LastAcceptedRouteId { get; private set; } = "none";
        public string LastRejectedReason { get; private set; } = "none";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureReceiverAfterSceneLoad()
        {
            EnsureReceiver();
        }

        public static AndroidBehaviorEventBridge EnsureReceiver()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameObject existing = GameObject.Find(DefaultGameObjectName);
            if (existing == null)
            {
                existing = new GameObject(DefaultGameObjectName);
            }

            AndroidBehaviorEventBridge bridge = existing.GetComponent<AndroidBehaviorEventBridge>();
            if (bridge == null)
            {
                bridge = existing.AddComponent<AndroidBehaviorEventBridge>();
            }

            return bridge;
        }

        public static void RecordUnityEvent(string eventType, string routeId, bool writeToFeatureEngine = false)
        {
            BehaviorEvent behaviorEvent = new BehaviorEvent
            {
                eventType = eventType,
                routeId = routeId,
                source = "unity"
            };

            EnsureReceiver().AcceptBehaviorEvent(behaviorEvent, writeToFeatureEngine);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (createOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            ResolveFeatureEngine();
        }

        private void OnEnable()
        {
            ResolveFeatureEngine();
        }

        public void OnBehaviorEvent(string json)
        {
            BehaviorEvent safeEvent;
            string reason;
            if (!BehaviorEventSanitizer.TryParseAndSanitize(json, out safeEvent, out reason))
            {
                LastRejectedReason = reason;
                if (logEvents)
                {
                    Debug.LogWarning("[CatLife] Behavior event rejected: " + reason);
                }
                return;
            }

            AcceptBehaviorEvent(safeEvent, true);
        }

        public bool AcceptBehaviorEvent(BehaviorEvent rawEvent)
        {
            return AcceptBehaviorEvent(rawEvent, true);
        }

        public bool AcceptBehaviorEvent(BehaviorEvent rawEvent, bool writeToFeatureEngine)
        {
            BehaviorEvent safeEvent;
            string reason;
            if (!BehaviorEventSanitizer.TrySanitize(rawEvent, out safeEvent, out reason))
            {
                LastRejectedReason = reason;
                return false;
            }

            ResolveFeatureEngine();
            if (writeToFeatureEngine && featureEngine != null)
            {
                featureEngine.RecordBehaviorEvent(safeEvent);
            }

            LastAcceptedEventType = safeEvent.eventType;
            LastAcceptedRouteId = safeEvent.routeId;
            LastRejectedReason = "none";
            if (logEvents)
            {
                Debug.Log("[CatLife] Behavior event accepted: " + safeEvent.eventType + " route=" + safeEvent.routeId);
            }

            return true;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            AcceptBehaviorEvent(new BehaviorEvent
            {
                eventType = pauseStatus ? "AppPause" : "AppResume",
                routeId = "application",
                source = "lifecycle"
            }, true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            AcceptBehaviorEvent(new BehaviorEvent
            {
                eventType = hasFocus ? "AppResume" : "AppPause",
                routeId = "application",
                source = "lifecycle"
            }, true);
        }

        private void ResolveFeatureEngine()
        {
            if (featureEngine != null)
            {
                return;
            }

            featureEngine = FindAnyObjectByType<RealtimeFeatureEngine>();
        }
    }
}
