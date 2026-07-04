using System.Text;
using CatLife.LLM;
using CatLife.Recognition;
using CatLife.SceneInteraction;
using UnityEngine;
using UnityEngine.AI;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatBehaviorTelemetry : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private CatBehaviorDriver behaviorDriver;
        [SerializeField] private CatNavigationAgent navigationAgent;
        [SerializeField] private CatDestinationPlanner destinationPlanner;
        [SerializeField] private CatActionRouter actionRouter;
        [SerializeField] private CatBehaviorMemory behaviorMemory;
        [SerializeField] private CatNeedModel needModel;
        [SerializeField] private CatNavMeshSafetyGuard safetyGuard;
        [SerializeField] private Animator animator;
        [SerializeField] private MonoBehaviour recognitionProviderComponent;
        [SerializeField] private MonoBehaviour llmClientComponent;
        [SerializeField] private RealtimeFeatureEngine featureEngine;

        [Header("Debug")]
        [SerializeField] private bool showOnScreen;
        [SerializeField] private float refreshInterval = 0.5f;
        [SerializeField] private Rect screenRect = new Rect(18f, 170f, 430f, 300f);
        [SerializeField] private int fontSize = 14;

        private readonly StringBuilder builder = new StringBuilder(1400);
        private IRecognitionProvider recognitionProvider;
        private ICatLLMClient llmClient;
        private string cachedReport = "Cat telemetry warming up.";
        private float nextRefreshTime;

        public string LastReport
        {
            get { return cachedReport; }
        }

        public bool HasCoreReferences
        {
            get
            {
                return behaviorDriver != null &&
                    navigationAgent != null &&
                    destinationPlanner != null &&
                    actionRouter != null &&
                    behaviorMemory != null &&
                    animator != null;
            }
        }

        private void Reset()
        {
            ResolveLocalReferences();
        }

        private void Awake()
        {
            ResolveLocalReferences();
            RefreshReport();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
            RefreshReport();
        }

        public void Configure(
            CatBehaviorDriver driver,
            CatNavigationAgent navigation,
            CatDestinationPlanner planner,
            CatActionRouter router,
            CatBehaviorMemory memory,
            CatNeedModel needs,
            CatNavMeshSafetyGuard guard,
            Animator catAnimator,
            MonoBehaviour recognitionProviderBehaviour,
            MonoBehaviour llmClientBehaviour,
            RealtimeFeatureEngine realtimeFeatures)
        {
            behaviorDriver = driver;
            navigationAgent = navigation;
            destinationPlanner = planner;
            actionRouter = router;
            behaviorMemory = memory;
            needModel = needs;
            safetyGuard = guard;
            animator = catAnimator;
            recognitionProviderComponent = recognitionProviderBehaviour;
            llmClientComponent = llmClientBehaviour;
            featureEngine = realtimeFeatures;
            recognitionProvider = recognitionProviderComponent as IRecognitionProvider;
            llmClient = llmClientComponent as ICatLLMClient;
            RefreshReport();
        }

        public string BuildDebugReport()
        {
            ResolveInterfaceReferences();

            RecognitionSnapshot snapshot = behaviorDriver != null
                ? behaviorDriver.LatestRecognitionSnapshot
                : recognitionProvider != null
                    ? recognitionProvider.Latest
                    : RecognitionSnapshot.CreateDefault();
            RealtimeFeatureSnapshot features = featureEngine != null ? featureEngine.Latest : default(RealtimeFeatureSnapshot);
            LLMBehaviorSuggestion suggestion = behaviorDriver != null
                ? behaviorDriver.LatestLlmSuggestion
                : LLMBehaviorSuggestion.Default();

            builder.Length = 0;
            builder.Append("cat_state=").Append(behaviorDriver != null ? behaviorDriver.CurrentState.ToString() : "missing_driver");
            builder.Append("; memory_state=").Append(behaviorMemory != null ? behaviorMemory.LastState.ToString() : "missing_memory");
            builder.Append("; focus=").Append(snapshot.focusState);
            builder.Append("; intent=").Append(snapshot.userIntent);
            builder.Append("; risk=").Append(snapshot.interruptionRisk);
            builder.Append("; interactionReady=").Append(snapshot.interactionReadiness.ToString("0.00"));
            builder.AppendLine();

            builder.Append("nav_on_mesh=").Append(navigationAgent != null && navigationAgent.IsOnNavMesh);
            builder.Append("; moving=").Append(navigationAgent != null && navigationAgent.IsMoving);
            builder.Append("; speed01=").Append(navigationAgent != null ? navigationAgent.Speed01.ToString("0.00") : "0.00");
            builder.Append("; remaining=").Append(navigationAgent != null ? navigationAgent.RemainingDistance.ToString("0.00") : "0.00");
            builder.Append("; path=").Append(navigationAgent != null ? navigationAgent.PathStatusText : "missing_navigation");
            builder.Append("; interest=").Append(destinationPlanner != null ? destinationPlanner.LastPlannedInterestPointId : "");
            builder.AppendLine();

            builder.Append("router=").Append(actionRouter != null ? actionRouter.LastDecision : "missing_router");
            builder.Append("; accepted=").Append(actionRouter != null ? actionRouter.LastAcceptedAction.ToString() : "None");
            builder.Append("; source=").Append(actionRouter != null ? actionRouter.LastAcceptedSource.ToString() : "None");
            builder.Append("; queue=").Append(actionRouter != null ? actionRouter.QueuedActionCount.ToString() : "0");
            builder.Append("; pending=").Append(actionRouter != null ? actionRouter.PendingAction.ToString() : "None");
            builder.Append("; hold=").Append(behaviorDriver != null ? behaviorDriver.ActionHoldRemaining.ToString("0.00") : "0.00");
            builder.AppendLine();

            builder.Append("features_focus=").Append(features.focusScore01.ToString("0.00"));
            builder.Append("; arousal=").Append(features.arousal01.ToString("0.00"));
            builder.Append("; distraction=").Append(features.distraction01.ToString("0.00"));
            builder.Append("; tap1s=").Append(features.tapRate1s.ToString("0.0"));
            builder.Append("; pages30s=").Append(features.pageSwitches30s);
            builder.AppendLine();

            AppendSceneInteractionLine();

            builder.Append("needs=");
            if (needModel != null)
            {
                CatNeedState needs = needModel.Current;
                builder.Append("curiosity ").Append(needs.curiosity01.ToString("0.00"));
                builder.Append(", safety ").Append(needs.safety01.ToString("0.00"));
                builder.Append(", focusCompanion ").Append(needs.focusCompanionship01.ToString("0.00"));
            }
            else
            {
                builder.Append("missing_need_model");
            }

            builder.AppendLine();
            builder.Append("llm_enabled=").Append(llmClient != null && llmClient.Enabled);
            builder.Append("; llm_busy=").Append(llmClient != null && llmClient.IsBusy);
            BlueLmOnDeviceClient blueLmClient = llmClientComponent as BlueLmOnDeviceClient;
            if (blueLmClient != null)
            {
                builder.Append("; llm_source=").Append(blueLmClient.LastSource);
                builder.Append("; llm_error=").Append(blueLmClient.LastFailureReason);
            }
            builder.Append("; mood=").Append(suggestion != null ? suggestion.moodBias : "calm");
            builder.Append("; bubble=").Append(suggestion != null && suggestion.showBubble);
            builder.Append("; safety=").Append(behaviorDriver != null ? behaviorDriver.LastLlmSafetyReason : "missing_driver");
            builder.AppendLine();

            builder.Append("anim=").Append(GetAnimatorStateName());
            builder.Append("; safety=").Append(safetyGuard != null ? safetyGuard.BuildStatusLine() : "missing_safety_guard");
            return builder.ToString();
        }

        private void AppendSceneInteractionLine()
        {
            SceneInteractionPayload payload = behaviorDriver != null
                ? behaviorDriver.LatestSceneInteractionPayload
                : default(SceneInteractionPayload);
            SceneInteractionPoint point = behaviorDriver != null
                ? behaviorDriver.LatestSceneInteractionPoint
                : null;

            builder.Append("scene_interaction=");
            if (!payload.IsValid || point == null)
            {
                builder.Append("none");
                builder.AppendLine();
                return;
            }

            builder.Append(payload.pointId);
            builder.Append("; label=").Append(payload.displayName);
            builder.Append("; tags=").Append(JoinTags(payload.tags));
            builder.Append("; animTag=").Append(point.PreferredAnimationTag);
            builder.AppendLine();
        }

        private static string JoinTags(string[] tags)
        {
            if (tags == null || tags.Length == 0)
            {
                return "none";
            }

            return string.Join(",", tags);
        }

        private void RefreshReport()
        {
            cachedReport = BuildDebugReport();
        }

        private void ResolveLocalReferences()
        {
            if (behaviorDriver == null) behaviorDriver = GetComponent<CatBehaviorDriver>();
            if (navigationAgent == null) navigationAgent = GetComponent<CatNavigationAgent>();
            if (destinationPlanner == null) destinationPlanner = GetComponent<CatDestinationPlanner>();
            if (actionRouter == null) actionRouter = GetComponent<CatActionRouter>();
            if (behaviorMemory == null) behaviorMemory = GetComponent<CatBehaviorMemory>();
            if (needModel == null) needModel = GetComponent<CatNeedModel>();
            if (safetyGuard == null) safetyGuard = GetComponent<CatNavMeshSafetyGuard>();
            if (animator == null) animator = GetComponent<Animator>();
            ResolveInterfaceReferences();
        }

        private void ResolveInterfaceReferences()
        {
            if (recognitionProvider == null && recognitionProviderComponent != null)
            {
                recognitionProvider = recognitionProviderComponent as IRecognitionProvider;
            }

            if (llmClient == null && llmClientComponent != null)
            {
                llmClient = llmClientComponent as ICatLLMClient;
            }
        }

        private string GetAnimatorStateName()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return "missing_animator";
            }

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.shortNameHash.ToString();
        }

        private void OnGUI()
        {
            if (!showOnScreen)
            {
                return;
            }

            GUIStyle style = GUI.skin.box;
            int previousSize = style.fontSize;
            TextAnchor previousAlignment = style.alignment;
            style.fontSize = Mathf.Max(10, fontSize);
            style.alignment = TextAnchor.UpperLeft;
            GUI.Box(screenRect, cachedReport, style);
            style.fontSize = previousSize;
            style.alignment = previousAlignment;
        }
    }
}
