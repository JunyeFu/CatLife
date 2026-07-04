using System.Collections.Generic;
using CatLife.Cat;
using CatLife.LLM;
using CatLife.Recognition;
using CatLife.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace CatLife.EditorTools
{
    public static class CatLifePlayModeBehaviorSmokeValidator
    {
        private const string MenuPath = "CatLife/Runtime/Validate Play Mode Behavior Smoke";
        private const string CatName = "CatCompanionModel";
        private const string SystemsName = "CatBehaviorSystems";

        [MenuItem(MenuPath)]
        public static void ValidateFromMenu()
        {
            string report = ValidateCurrentPlayModeReport();
            if (report.StartsWith("PASS"))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }

        public static bool ValidateCurrentPlayMode(bool logSuccess)
        {
            string report = ValidateCurrentPlayModeReport();
            bool passed = report.StartsWith("PASS");
            if (passed && logSuccess)
            {
                Debug.Log(report);
            }
            else if (!passed)
            {
                Debug.LogError(report);
            }

            return passed;
        }

        public static string ValidateCurrentPlayModeReport()
        {
            List<string> issues = new List<string>();
            if (!Application.isPlaying)
            {
                issues.Add("Play Mode is required for behavior smoke validation.");
                return BuildReport(issues);
            }

            GameObject cat = GameObject.Find(CatName);
            GameObject systems = GameObject.Find(SystemsName);
            if (cat == null) issues.Add("Missing scene object: " + CatName);
            if (systems == null) issues.Add("Missing scene object: " + SystemsName);
            if (cat == null || systems == null)
            {
                return BuildReport(issues);
            }

            NavMeshAgent agent = cat.GetComponent<NavMeshAgent>();
            CatBehaviorDriver driver = cat.GetComponent<CatBehaviorDriver>();
            CatDestinationPlanner planner = cat.GetComponent<CatDestinationPlanner>();
            CatBehaviorMemory memory = cat.GetComponent<CatBehaviorMemory>();
            CatBehaviorTelemetry telemetry = cat.GetComponent<CatBehaviorTelemetry>();
            CatInteractionMapper interactionMapper = cat.GetComponent<CatInteractionMapper>();
            CatNavMeshSafetyGuard safetyGuard = cat.GetComponent<CatNavMeshSafetyGuard>();
            Animator animator = cat.GetComponent<Animator>();
            RealtimeFeatureEngine featureEngine = systems.GetComponent<RealtimeFeatureEngine>();
            MockRecognitionProvider recognitionProvider = systems.GetComponent<MockRecognitionProvider>();
            PrivacyGateway privacyGateway = systems.GetComponent<PrivacyGateway>();
            FocusFeedbackProvider feedbackProvider = systems.GetComponent<FocusFeedbackProvider>();
            CatBubblePresenter bubblePresenter = Object.FindAnyObjectByType<CatBubblePresenter>();
            CatCameraRangeIndicator cameraRangeIndicator = Object.FindAnyObjectByType<CatCameraRangeIndicator>();
            CatInterestPointRegistry interestRegistry = Object.FindAnyObjectByType<CatInterestPointRegistry>();

            if (agent == null) issues.Add("Cat missing NavMeshAgent.");
            if (driver == null) issues.Add("Cat missing CatBehaviorDriver.");
            if (planner == null) issues.Add("Cat missing CatDestinationPlanner.");
            if (memory == null) issues.Add("Cat missing CatBehaviorMemory.");
            if (telemetry == null) issues.Add("Cat missing CatBehaviorTelemetry.");
            if (interactionMapper == null) issues.Add("Cat missing CatInteractionMapper.");
            if (safetyGuard == null) issues.Add("Cat missing CatNavMeshSafetyGuard.");
            if (animator == null) issues.Add("Cat missing Animator.");
            if (featureEngine == null) issues.Add("Systems missing RealtimeFeatureEngine.");
            if (recognitionProvider == null) issues.Add("Systems missing MockRecognitionProvider.");
            if (privacyGateway == null) issues.Add("Systems missing PrivacyGateway.");
            if (feedbackProvider == null) issues.Add("Systems missing FocusFeedbackProvider.");
            if (bubblePresenter == null) issues.Add("Scene missing CatBubblePresenter.");
            if (cameraRangeIndicator == null) issues.Add("Scene missing CatCameraRangeIndicator.");
            if (interestRegistry == null) issues.Add("Scene missing CatInterestPointRegistry.");
            if (issues.Count > 0)
            {
                return BuildReport(issues);
            }

            ValidateNavMeshRuntime(agent, safetyGuard, interestRegistry, issues);
            ValidateCameraPreferredPlanning(planner, cat.transform, issues);
            ValidateFocusSpeedAndIndicator(driver, agent, cameraRangeIndicator, issues);
            ValidateRecognitionAndPrompt(driver, featureEngine, recognitionProvider, interestRegistry, issues);
            ValidateFocusFeedback(privacyGateway, feedbackProvider, issues);
            ValidateAnimatorRuntime(animator, agent, issues);
            ValidateTelemetryRuntime(telemetry, issues);

            return BuildReport(issues);
        }

        private static void ValidateNavMeshRuntime(
            NavMeshAgent agent,
            CatNavMeshSafetyGuard safetyGuard,
            CatInterestPointRegistry interestRegistry,
            List<string> issues)
        {
            if (!agent.enabled)
            {
                issues.Add("NavMeshAgent is disabled.");
                return;
            }

            if (!agent.isOnNavMesh)
            {
                issues.Add("Cat is not on NavMesh in Play Mode.");
            }

            if (agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                issues.Add("Cat has non-complete NavMesh path: " + agent.pathStatus);
            }

            if (safetyGuard.LastNavMeshDistance > 0.4f)
            {
                issues.Add("Cat is drifting too far from NavMesh: " + safetyGuard.LastNavMeshDistance.ToString("F3"));
            }

            if (interestRegistry.Count < 8)
            {
                issues.Add("CatInterestPointRegistry has too few points: " + interestRegistry.Count);
            }
        }

        private static void ValidateCameraPreferredPlanning(
            CatDestinationPlanner planner,
            Transform catTransform,
            List<string> issues)
        {
            if (planner == null || catTransform == null)
            {
                return;
            }

            if (Camera.main == null)
            {
                issues.Add("Camera-preferred planning requires Camera.main.");
                return;
            }

            bool nonFocusOk = planner.IsPointInPreferredCameraRange(catTransform.position);
            if (!nonFocusOk)
            {
                RecognitionSnapshot snapshot = RecognitionSnapshot.CreateDefault();
                CatBehaviorDecision decision = CatBehaviorDecision.Create(
                    CatBehaviorState.Roam,
                    0f,
                    0f,
                    0,
                    CatActionInterruptPolicy.DropIfBusy,
                    false,
                    "smoke_camera_return");

                for (int i = 0; i < 12; i++)
                {
                    Vector3 target;
                    if (planner.TryPlanNext(
                            snapshot,
                            decision,
                            CatNeedState.CreateDefault(),
                            null,
                            catTransform.position,
                            out target) &&
                        planner.IsPointInPreferredCameraRange(target))
                    {
                        nonFocusOk = true;
                        break;
                    }
                }
            }

            if (!nonFocusOk)
            {
                issues.Add("Non-focus destination planner did not find a camera-preferred return target.");
            }

            ValidateFocusedCameraPreference(planner, catTransform, issues);
        }

        private static void ValidateFocusedCameraPreference(
            CatDestinationPlanner planner,
            Transform catTransform,
            List<string> issues)
        {
            RecognitionSnapshot snapshot = RecognitionSnapshot.CreateDefault();
            snapshot.focusState = FocusState.Focused;
            CatBehaviorDecision decision = CatBehaviorDecision.Create(
                CatBehaviorState.FocusedRoam,
                0f,
                0f,
                0,
                CatActionInterruptPolicy.DropIfBusy,
                true,
                "smoke_focused_camera_return");

            Vector3 target;
            if (!planner.TryPlanNext(
                    snapshot,
                    decision,
                    CatNeedState.CreateDefault(),
                    null,
                    catTransform.position,
                    out target))
            {
                issues.Add("Focused destination planner could not produce a camera-range target.");
                return;
            }

            if (!planner.IsPointInPreferredCameraRange(target))
            {
                issues.Add("Focused destination planner target is not inside camera range.");
            }
        }

        private static void ValidateFocusSpeedAndIndicator(
            CatBehaviorDriver driver,
            NavMeshAgent agent,
            CatCameraRangeIndicator indicator,
            List<string> issues)
        {
            if (driver == null || agent == null)
            {
                return;
            }

            driver.SetContinuousWalking(true, 1f);
            driver.SetFocusMode(false);
            float freeSpeed = agent.speed;

            driver.SetContinuousWalking(true, 0.5f);
            driver.SetFocusMode(true);
            float focusSpeed = agent.speed;

            driver.SetContinuousWalking(true, 1f);
            driver.SetFocusMode(false);
            float restoredSpeed = agent.speed;

            if (freeSpeed <= 0.01f || Mathf.Abs(focusSpeed / freeSpeed - 0.5f) > 0.08f)
            {
                issues.Add("Focused cat speed should be approximately 50% of non-focus speed.");
            }

            if (freeSpeed > 0.01f && Mathf.Abs(restoredSpeed / freeSpeed - 1f) > 0.08f)
            {
                issues.Add("Cat speed did not restore after leaving focus mode.");
            }

            if (indicator == null || !indicator.HasCoreReferences)
            {
                issues.Add("CatCameraRangeIndicator is missing runtime references.");
            }
        }

        private static void ValidateRecognitionAndPrompt(
            CatBehaviorDriver driver,
            RealtimeFeatureEngine featureEngine,
            MockRecognitionProvider recognitionProvider,
            CatInterestPointRegistry interestRegistry,
            List<string> issues)
        {
            driver.NotifyUiAction(CatBehaviorState.HeadTiltListen, "smoke_start_focus");
            driver.NotifyFocusSessionStarted();
            driver.NotifyUiAction(CatBehaviorState.TailWagHappy, "smoke_cat_page");
            driver.NotifyUiAction(CatBehaviorState.HeadTiltListen, "smoke_record_page");
            driver.NotifyCatTapped();
            if (!TryNotifyGroundTap(driver, interestRegistry))
            {
                issues.Add("Ground tap did not route to a valid NavMesh destination.");
            }

            featureEngine.Tick(0.6f);
            recognitionProvider.Tick(0.6f);

            RealtimeFeatureSnapshot features = featureEngine.Latest;
            RecognitionSnapshot snapshot = recognitionProvider.Latest;
            if (!features.isFocusSessionActive)
            {
                issues.Add("Realtime features did not enter focus session state.");
            }

            if (features.tapRate1s < 1f)
            {
                issues.Add("Realtime features did not record cat tap.");
            }

            if (features.pageSwitches30s < 2)
            {
                issues.Add("Realtime features did not record UI page events.");
            }

            if (snapshot.focusState == FocusState.NonFocus)
            {
                issues.Add("Recognition snapshot did not react to focus session features.");
            }

            CatPromptContext context = CatPromptContext.Create(
                snapshot,
                CatBehaviorState.IdleBreath,
                0f,
                "calm",
                new[] { "smoke_record_page", "smoke_cat_page", "smoke_start_focus" },
                features,
                true);

            string prompt = new CatPromptBuilder().BuildCompositeDebugPrompt(context);
            if (!prompt.Contains("focusSessionActive") || !prompt.Contains("distraction01"))
            {
                issues.Add("Composite prompt is missing realtime feature fields.");
            }

            if (!prompt.Contains("behaviorPolicy") || !prompt.Contains("blockedOutputs"))
            {
                issues.Add("Composite prompt is missing behavior policy or blocked output contract.");
            }
        }

        private static bool TryNotifyGroundTap(CatBehaviorDriver driver, CatInterestPointRegistry interestRegistry)
        {
            if (driver == null || interestRegistry == null || interestRegistry.Points == null)
            {
                return false;
            }

            CatInterestPoint[] points = interestRegistry.Points;
            for (int i = 0; i < points.Length; i++)
            {
                CatInterestPoint point = points[i];
                if (point == null)
                {
                    continue;
                }

                if (driver.NotifyGroundTapped(point.transform.position))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateAnimatorRuntime(Animator animator, NavMeshAgent agent, List<string> issues)
        {
            if (animator.runtimeAnimatorController == null)
            {
                issues.Add("Animator has no runtime controller in Play Mode.");
                return;
            }

            if (!animator.HasState(0, Animator.StringToHash("Base Layer.CL_CAT_SRC_Walk_60fps")))
            {
                issues.Add("Animator cannot resolve walk state in Play Mode.");
            }

            if (!animator.HasState(0, Animator.StringToHash("Base Layer.CL_CAT_IdleBreath_v06_headsync_loop_108f")))
            {
                issues.Add("Animator cannot resolve idle state in Play Mode.");
            }

            if (agent.hasPath && agent.velocity.magnitude > 0.01f)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (!stateInfo.IsName("CL_CAT_SRC_Walk_60fps") && !stateInfo.IsName("Base Layer.CL_CAT_SRC_Walk_60fps"))
                {
                    issues.Add("Cat is moving but Animator is not in walk state: " + stateInfo.shortNameHash);
                }
            }
        }

        private static void ValidateFocusFeedback(
            PrivacyGateway privacyGateway,
            FocusFeedbackProvider feedbackProvider,
            List<string> issues)
        {
            if (privacyGateway == null || feedbackProvider == null)
            {
                return;
            }

            BehaviorFeatureSummary summary = BehaviorFeatureSummary.CreateLocalSession(
                "smoke-focus-feedback",
                1500,
                1380,
                1,
                1,
                25,
                1380,
                true);

            string reason;
            if (!privacyGateway.TryValidate(summary, out reason))
            {
                issues.Add("PrivacyGateway rejected safe smoke summary: " + reason);
            }

            FocusFeedback fallback = feedbackProvider.BuildImmediateFallback(summary, false);
            if (fallback == null || string.IsNullOrEmpty(fallback.text))
            {
                issues.Add("FocusFeedbackProvider failed to build local fallback feedback.");
            }

            BehaviorFeatureSummary blocked = summary;
            blocked.rawTextIncluded = true;
            if (privacyGateway.TryValidate(blocked, out reason))
            {
                issues.Add("PrivacyGateway accepted a blocked raw-text summary.");
            }

            CatPromptContext safePromptContext = CatPromptContext.Create(
                RecognitionSnapshot.CreateDefault(),
                CatBehaviorState.Roam,
                0.25f,
                "curious",
                new[] { "scene_interaction" },
                default(RealtimeFeatureSnapshot),
                false);
            safePromptContext = privacyGateway.Sanitize(safePromptContext);
            if (!privacyGateway.TryValidate(safePromptContext, out reason))
            {
                issues.Add("PrivacyGateway rejected safe cat prompt context: " + reason);
            }

            CatPromptContext blockedPromptContext = CatPromptContext.Create(
                RecognitionSnapshot.CreateDefault(),
                CatBehaviorState.Roam,
                0.25f,
                "curious",
                new[] { "scene_interaction" },
                default(RealtimeFeatureSnapshot),
                false);
            blockedPromptContext.safeLocalSummary = "screen capture request";
            if (privacyGateway.TryValidate(blockedPromptContext, out reason))
            {
                issues.Add("PrivacyGateway accepted a blocked cat prompt context.");
            }
        }

        private static void ValidateTelemetryRuntime(CatBehaviorTelemetry telemetry, List<string> issues)
        {
            if (telemetry == null)
            {
                return;
            }

            if (!telemetry.HasCoreReferences)
            {
                issues.Add("CatBehaviorTelemetry has missing core references.");
            }

            string report = telemetry.BuildDebugReport();
            if (string.IsNullOrEmpty(report))
            {
                issues.Add("CatBehaviorTelemetry returned an empty report.");
                return;
            }

            string[] requiredFields =
            {
                "cat_state=",
                "focus=",
                "nav_on_mesh=",
                "router=",
                "features_focus=",
                "llm_enabled=",
                "safety="
            };

            for (int i = 0; i < requiredFields.Length; i++)
            {
                if (!report.Contains(requiredFields[i]))
                {
                    issues.Add("CatBehaviorTelemetry report missing field: " + requiredFields[i]);
                }
            }
        }

        private static string BuildReport(List<string> issues)
        {
            if (issues.Count == 0)
            {
                return "PASS CatLife Play Mode behavior smoke: NavMesh runtime, safety guard, recognition features, prompt context, focus feedback, telemetry, and animator states are responsive.";
            }

            return "FAIL CatLife Play Mode behavior smoke:\n- " + string.Join("\n- ", issues.ToArray());
        }
    }
}
