using System.Collections.Generic;
using System.IO;
using CatLife.Cat;
using CatLife.LLM;
using CatLife.Recognition;
using CatLife.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace CatLife.EditorTools
{
    public static class CatLifeRuntimeAssemblyValidator
    {
        private const string MenuPath = "CatLife/Runtime/Validate Runtime Assembly";
        private const string CatName = "CatCompanionModel";
        private const string RuntimeName = "Runtime";
        private const string NavigationName = "Navigation";
        private const string SystemsName = "CatBehaviorSystems";
        private const string AnchorRootName = "CatDestinationAnchors";
        private const string ForbiddenRootName = "CatForbiddenZones";
        private const string InterestRootName = "CatInterestPoints";
        private const string ControllerPath = "Assets/Art/Cat/Animator/CatLife_TownWalker.controller";
        private const string ProjectStructurePath = "Assets/PROJECT_STRUCTURE.md";
        private const string BehaviorEventSchemaPath = "Assets/Configs/behavior_event_schema.json";
        private const string LlmFeedbackSchemaPath = "Assets/Configs/llm_feedback_schema.json";

        private static readonly string[] RequiredWalkAreas =
        {
            "CatWalkableArea_MainPlaza",
            "CatWalkableArea_LeftGardenPath",
            "CatWalkableArea_RightGardenPath",
            "CatWalkableArea_FrontStoneRing",
            "CatWalkableArea_CenterSecondRing_North",
            "CatWalkableArea_CenterSecondRing_South",
            "CatWalkableArea_CenterSecondRing_West",
            "CatWalkableArea_CenterSecondRing_East"
        };

        private static readonly string[] RequiredRootDirectories =
        {
            "Art",
            "Configs",
            "Editor",
            "Materials",
            "Plugins",
            "Prefabs",
            "Scenes",
            "Scripts",
            "Settings",
            "UI"
        };

        private static readonly string[] RequiredScriptDirectories =
        {
            "Camera",
            "Cat",
            "Core",
            "LLM",
            "Recognition",
            "UI"
        };

        private static readonly string[] RequiredArtDirectories =
        {
            "Cat",
            "Town"
        };

        private static readonly string[] ForbiddenRootExtensions =
        {
            ".blend",
            ".fbx",
            ".glb",
            ".gltf",
            ".mp4",
            ".mov",
            ".zip",
            ".rar",
            ".7z"
        };

        private static readonly string[] RequiredAnimatorParameters =
        {
            "MoveSpeed01",
            "IsMoving",
            "InFocusMode",
            "Arousal01",
            "IsWalking"
        };

        private static readonly string[] RequiredStateNames =
        {
            "CL_CAT_IdleBreath_v06_headsync_loop_108f",
            "CL_CAT_SRC_Walk_60fps",
            "CL_CAT_AlertLook_v01_loop_120f",
            "CL_CAT_CuriousSniff_v02_loop_112f",
            "CL_CAT_EarTwitchAlert_v02_loop_120f",
            "CL_CAT_HeadShakeNo_v01_loop_108f",
            "CL_CAT_HeadTiltListen_v01_loop_96f",
            "CL_CAT_LookBack_v02_loop_112f",
            "CL_CAT_PawWave_v01_loop_96f",
            "CL_CAT_StretchYawn_v03_slow_loop_264f",
            "CL_CAT_TailWagHappy_v01_loop_96f"
        };

        private static readonly string[] RequiredClipPaths =
        {
            "Assets/Art/Cat/Animations/Clips/CL_CAT_IdleBreath_v06_headsync_loop_108f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_SRC_Walk_60fps.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_AlertLook_v01_loop_120f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_CuriousSniff_v02_loop_112f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_EarTwitchAlert_v02_loop_120f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_HeadShakeNo_v01_loop_108f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_HeadTiltListen_v01_loop_96f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_LookBack_v02_loop_112f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_PawWave_v01_loop_96f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_StretchYawn_v03_slow_loop_264f.anim",
            "Assets/Art/Cat/Animations/Clips/CL_CAT_TailWagHappy_v01_loop_96f.anim"
        };

        [MenuItem(MenuPath)]
        public static void ValidateFromMenu()
        {
            string report = ValidateCurrentSceneReport();
            if (report.StartsWith("PASS"))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }

        public static bool ValidateCurrentScene(bool logSuccess)
        {
            string report = ValidateCurrentSceneReport();
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

        public static string ValidateCurrentSceneReport()
        {
            List<string> issues = new List<string>();
            ValidateProjectStructure(issues);
            ValidateSceneObjects(issues);
            ValidateAnimatorAssets(issues);
            ValidateLlmRuntimeAdapters(issues);
            ValidateAndroidBlueLmPlugin(issues);

            if (issues.Count == 0)
            {
                return "PASS CatLife runtime assembly validation: scene wiring, NavMesh runtime, cat behavior driver, recognition/LLM systems, BlueLM Unity adapter, Android BlueLM bridge skeleton, config schemas, UI binding, and 11 animator states are present.";
            }

            return "FAIL CatLife runtime assembly validation:\n- " + string.Join("\n- ", issues.ToArray());
        }

        private static void ValidateLlmRuntimeAdapters(List<string> issues)
        {
            CatPromptContext context = CatPromptContext.Create(
                RecognitionSnapshot.CreateDefault(),
                CatBehaviorState.Roam,
                0.25f,
                "curious",
                new[] { "stage1_bluelm" },
                default(RealtimeFeatureSnapshot),
                false);

            BlueLmUnityRequest request = BlueLmUnityRequest.Create(
                string.Empty,
                context,
                new CatPromptBuilder(),
                1.25f);
            if (string.IsNullOrEmpty(request.requestId) || request.requestId.Length != 32)
            {
                issues.Add("BlueLmUnityRequest must generate a 32-char Guid N requestId.");
            }

            if (request.timeoutMs < 1000 || string.IsNullOrEmpty(request.userContextJson))
            {
                issues.Add("BlueLmUnityRequest did not preserve timeout or user context JSON.");
            }

            BlueLmAndroidEvent successEvent;
            string reason;
            string successJson = "{\"requestId\":\"stage1_success\",\"ok\":true,\"suggestion\":{\"suggestedLine\":\"Stay close.\",\"moodBias\":\"curious\",\"roamWeightBias\":0.1,\"quietIdleWeightBias\":0.0,\"socialResponseWeightBias\":0.0,\"showBubble\":true}}";
            if (!BlueLmAndroidEvent.TryParse(successJson, out successEvent, out reason))
            {
                issues.Add("BlueLmAndroidEvent rejected valid success JSON: " + reason);
            }
            else
            {
                LLMBehaviorSuggestion suggestion;
                if (!successEvent.TryBuildSuggestion(out suggestion, out reason))
                {
                    issues.Add("BlueLmAndroidEvent could not build success suggestion: " + reason);
                }
                else if (suggestion.moodBias != "curious" || !suggestion.showBubble)
                {
                    issues.Add("BlueLmAndroidEvent success suggestion fields were not preserved.");
                }
            }

            BlueLmAndroidEvent failureEvent;
            string failureJson = "{\"requestId\":\"stage1_failure\",\"ok\":false,\"error\":\"model_not_ready\"}";
            if (!BlueLmAndroidEvent.TryParse(failureJson, out failureEvent, out reason))
            {
                issues.Add("BlueLmAndroidEvent rejected valid failure JSON: " + reason);
            }
            else if (failureEvent.IsSuccess || failureEvent.ErrorText != "model_not_ready")
            {
                issues.Add("BlueLmAndroidEvent failure status was not preserved.");
            }
        }

        private static void ValidateAndroidBlueLmPlugin(List<string> issues)
        {
            ValidateProjectFileContains(
                "Assets/Plugins/Android/AndroidManifest.xml",
                issues,
                "MANAGE_EXTERNAL_STORAGE",
                "/sdcard/1225/1.7.0.4_1225_mtk9500");

            ValidateProjectFileContains(
                "Assets/Plugins/Android/libs/README.md",
                issues,
                "llm-sdk-release.aar",
                "SDK_NOT_LINKED");

            ValidateProjectFileContains(
                "Assets/Plugins/Android/src/main/java/com/catlife/bluelm/BlueLmUnityBridge.java",
                issues,
                "public static void init",
                "public static void generate",
                "openManageAllFilesAccessSettings");

            ValidateProjectFileContains(
                "Assets/Plugins/Android/src/main/java/com/catlife/bluelm/BlueLmEngine.java",
                issues,
                "DEFAULT_MODEL_PATH",
                "CODE_SDK_NOT_LINKED",
                "generateJsonAsync");

            ValidateProjectFileContains(
                "Assets/Plugins/Android/src/main/java/com/catlife/bluelm/BlueLmUnityCallback.java",
                issues,
                "UnitySendMessage",
                "BlueLM init ok=",
                "OnBlueLmEvent");

            ValidateProjectFileContains(
                "Assets/Plugins/Android/src/main/java/com/catlife/bluelm/BlueLmPermissionHelper.java",
                issues,
                "ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION",
                "isExternalStorageManager");

            string projectSettingsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../ProjectSettings/ProjectSettings.asset"));
            if (!File.Exists(projectSettingsPath))
            {
                issues.Add("Missing ProjectSettings.asset for Android settings validation.");
                return;
            }

            string settingsText = File.ReadAllText(projectSettingsPath);
            if (!settingsText.Contains("AndroidMinSdkVersion: 28"))
            {
                issues.Add("AndroidMinSdkVersion must be 28 for BlueLM stage 2.");
            }

            if (!settingsText.Contains("AndroidTargetArchitectures: 2"))
            {
                issues.Add("AndroidTargetArchitectures must stay ARM64-only for BlueLM stage 2.");
            }
        }

        private static void ValidateProjectStructure(List<string> issues)
        {
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(ProjectStructurePath) == null)
            {
                issues.Add("Missing Unity project structure guide: " + ProjectStructurePath);
            }

            for (int i = 0; i < RequiredRootDirectories.Length; i++)
            {
                string path = "Assets/" + RequiredRootDirectories[i];
                if (!AssetDatabase.IsValidFolder(path))
                {
                    issues.Add("Missing required Assets root directory: " + path);
                }
            }

            for (int i = 0; i < RequiredScriptDirectories.Length; i++)
            {
                string path = "Assets/Scripts/" + RequiredScriptDirectories[i];
                if (!AssetDatabase.IsValidFolder(path))
                {
                    issues.Add("Missing required runtime script domain directory: " + path);
                }
            }

            for (int i = 0; i < RequiredArtDirectories.Length; i++)
            {
                string path = "Assets/Art/" + RequiredArtDirectories[i];
                if (!AssetDatabase.IsValidFolder(path))
                {
                    issues.Add("Missing required art domain directory: " + path);
                }
            }

            ValidateConfigSchemas(issues);

            string fullAssetsPath = Path.GetFullPath(Application.dataPath);
            string[] rootFiles = Directory.GetFiles(fullAssetsPath);
            for (int i = 0; i < rootFiles.Length; i++)
            {
                string file = rootFiles[i];
                if (file.EndsWith(".meta"))
                {
                    continue;
                }

                string extension = Path.GetExtension(file).ToLowerInvariant();
                for (int j = 0; j < ForbiddenRootExtensions.Length; j++)
                {
                    if (extension == ForbiddenRootExtensions[j])
                    {
                        issues.Add("Large/source binary must not live directly under Assets root: " + Path.GetFileName(file));
                        break;
                    }
                }
            }
        }

        private static void ValidateConfigSchemas(List<string> issues)
        {
            ValidateTextAssetContains(
                BehaviorEventSchemaPath,
                issues,
                "privacy_level",
                "forbidden_collection",
                "event_schema",
                "cloud_upload_policy",
                "raw_input_text",
                "screen_screenshot_content",
                "user_consented_cloud_ai");

            ValidateTextAssetContains(
                LlmFeedbackSchemaPath,
                issues,
                "catlife.focus_feedback.v1",
                "bubble_text",
                "record_summary",
                "reaction_hint",
                "confidence",
                "contains_blame",
                "contains_medical_claim",
                "contains_sensitive_inference");
        }

        private static void ValidateTextAssetContains(
            string path,
            List<string> issues,
            params string[] requiredFragments)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null)
            {
                issues.Add("Missing required config schema: " + path);
                return;
            }

            string text = asset.text ?? string.Empty;
            for (int i = 0; i < requiredFragments.Length; i++)
            {
                if (!text.Contains(requiredFragments[i]))
                {
                    issues.Add("Config schema " + path + " is missing required fragment: " + requiredFragments[i]);
                }
            }
        }

        private static void ValidateProjectFileContains(
            string assetRelativePath,
            List<string> issues,
            params string[] requiredFragments)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, assetRelativePath.Substring("Assets/".Length)));
            if (!File.Exists(fullPath))
            {
                issues.Add("Missing required project file: " + assetRelativePath);
                return;
            }

            string text = File.ReadAllText(fullPath);
            for (int i = 0; i < requiredFragments.Length; i++)
            {
                if (!text.Contains(requiredFragments[i]))
                {
                    issues.Add("Project file " + assetRelativePath + " is missing required fragment: " + requiredFragments[i]);
                }
            }
        }

        private static void ValidateSceneObjects(List<string> issues)
        {
            GameObject cat = GameObject.Find(CatName);
            if (cat == null)
            {
                issues.Add("Missing scene object: " + CatName);
                return;
            }

            Transform runtime = FindTransform(RuntimeName, issues);
            Transform navigation = runtime != null ? runtime.Find(NavigationName) : null;
            Transform systems = runtime != null ? runtime.Find(SystemsName) : null;
            if (navigation == null) issues.Add("Missing Runtime/Navigation root.");
            if (systems == null) issues.Add("Missing Runtime/CatBehaviorSystems root.");

            ValidateCatComponents(cat, issues);
            ValidateNavigation(navigation, issues);
            ValidateSystems(systems, issues);
            ValidateUiBinding(issues);
        }

        private static void ValidateCatComponents(GameObject cat, List<string> issues)
        {
            Animator animator = RequireComponent<Animator>(cat, issues);
            NavMeshAgent navMeshAgent = RequireComponent<NavMeshAgent>(cat, issues);
            CatNavigationAgent navigationAgent = RequireComponent<CatNavigationAgent>(cat, issues);
            CatNavMeshSafetyGuard safetyGuard = RequireComponent<CatNavMeshSafetyGuard>(cat, issues);
            CatDestinationPlanner destinationPlanner = RequireComponent<CatDestinationPlanner>(cat, issues);
            CatAnimationController animationController = RequireComponent<CatAnimationController>(cat, issues);
            CatActionRouter actionRouter = RequireComponent<CatActionRouter>(cat, issues);
            RequireComponent<CatNeedModel>(cat, issues);
            RequireComponent<CatBehaviorMemory>(cat, issues);
            RequireComponent<CatBehaviorBrainScorer>(cat, issues);
            CatBehaviorTelemetry behaviorTelemetry = RequireComponent<CatBehaviorTelemetry>(cat, issues);
            CatInteractionMapper interactionMapper = RequireComponent<CatInteractionMapper>(cat, issues);
            CatBehaviorDriver behaviorDriver = RequireComponent<CatBehaviorDriver>(cat, issues);

            if (animator != null && animator.runtimeAnimatorController == null)
            {
                issues.Add("Cat Animator has no runtimeAnimatorController.");
            }

            if (navMeshAgent != null)
            {
                if (navMeshAgent.radius <= 0f) issues.Add("Cat NavMeshAgent radius is invalid.");
                if (navMeshAgent.height <= 0f) issues.Add("Cat NavMeshAgent height is invalid.");
            }

            if (navigationAgent != null)
            {
                RequireSerializedObject(navigationAgent, "agent", issues);
                RequireSerializedMinFloat(navigationAgent, "freeRoamSpeed", 1f, issues);
                RequireSerializedMinFloat(navigationAgent, "focusedRoamSpeed", 1f, issues);
            }

            BoxCollider interactionCollider = cat.GetComponent<BoxCollider>();
            if (interactionCollider == null)
            {
                issues.Add("Cat needs a BoxCollider for tap/long-press raycast interaction.");
            }
            else if (!interactionCollider.isTrigger)
            {
                issues.Add("Cat interaction BoxCollider should be trigger-only.");
            }

            if (behaviorDriver != null)
            {
                RequireSerializedObject(behaviorDriver, "recognitionProviderComponent", issues);
                RequireSerializedObject(behaviorDriver, "llmClientComponent", issues);
                RequireSerializedObject(behaviorDriver, "navigationAgent", issues);
                RequireSerializedObject(behaviorDriver, "animationController", issues);
                RequireSerializedObject(behaviorDriver, "destinationPlanner", issues);
                RequireSerializedObject(behaviorDriver, "actionRouter", issues);
                RequireSerializedObject(behaviorDriver, "featureEngine", issues);
                RequireSerializedObject(behaviorDriver, "privacyGateway", issues);
                RequireSerializedObject(behaviorDriver, "needModel", issues);
                RequireSerializedObject(behaviorDriver, "behaviorMemory", issues);
                RequireSerializedObject(behaviorDriver, "behaviorScorer", issues);
                RequireSerializedObject(behaviorDriver, "focusLookAtTarget", issues);
                RequireSerializedBool(behaviorDriver, "faceCameraWhenFocusedIdle", true, issues);
            }

            if (safetyGuard != null)
            {
                RequireSerializedObject(safetyGuard, "agent", issues);
                RequireSerializedObject(safetyGuard, "navigationAgent", issues);
            }

            if (animationController != null)
            {
                RequireSerializedObject(animationController, "animator", issues);
            }

            if (behaviorTelemetry != null)
            {
                RequireSerializedObject(behaviorTelemetry, "behaviorDriver", issues);
                RequireSerializedObject(behaviorTelemetry, "navigationAgent", issues);
                RequireSerializedObject(behaviorTelemetry, "destinationPlanner", issues);
                RequireSerializedObject(behaviorTelemetry, "actionRouter", issues);
                RequireSerializedObject(behaviorTelemetry, "behaviorMemory", issues);
                RequireSerializedObject(behaviorTelemetry, "needModel", issues);
                RequireSerializedObject(behaviorTelemetry, "safetyGuard", issues);
                RequireSerializedObject(behaviorTelemetry, "animator", issues);
                RequireSerializedObject(behaviorTelemetry, "recognitionProviderComponent", issues);
                RequireSerializedObject(behaviorTelemetry, "llmClientComponent", issues);
                RequireSerializedObject(behaviorTelemetry, "featureEngine", issues);
            }

            if (interactionMapper != null)
            {
                RequireSerializedObject(interactionMapper, "behaviorDriver", issues);
                RequireSerializedObject(interactionMapper, "inputCamera", issues);
                RequireSerializedObject(interactionMapper, "catRoot", issues);
            }

            CatTownWalker legacyWalker = cat.GetComponent<CatTownWalker>();
            if (legacyWalker != null && legacyWalker.enabled)
            {
                issues.Add("Legacy CatTownWalker is still enabled; CatBehaviorDriver should be primary.");
            }

            if (destinationPlanner == null || actionRouter == null)
            {
                return;
            }

            if (destinationPlanner != null)
            {
                RequireSerializedObject(destinationPlanner, "planningCamera", issues);
                RequireSerializedObject(destinationPlanner, "interestPointRegistry", issues);
                RequireSerializedObject(destinationPlanner, "needModel", issues);
                RequireSerializedObject(destinationPlanner, "behaviorMemory", issues);
                RequireSerializedArray(destinationPlanner, "forbiddenZones", 1, issues);
                RequireSerializedBool(destinationPlanner, "preferCameraRangeWhenNonFocused", true, issues);
                RequireSerializedBool(destinationPlanner, "preferCameraRangeWhenFocused", true, issues);
                RequireSerializedMinFloat(destinationPlanner, "cameraReturnBiasWeight", 1f, issues);
                RequireSerializedMinFloat(destinationPlanner, "nonFocusNearCameraBiasWeight", 1f, issues);
                RequireSerializedMinFloat(destinationPlanner, "focusFarCameraBiasWeight", 1f, issues);
            }
        }

        private static void ValidateNavigation(Transform navigation, List<string> issues)
        {
            if (navigation == null)
            {
                return;
            }

            NavMeshSurface surface = navigation.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                issues.Add("Runtime/Navigation missing NavMeshSurface.");
            }
            else
            {
                if (surface.collectObjects != CollectObjects.Children)
                {
                    issues.Add("NavMeshSurface should collect children only.");
                }

                if (surface.useGeometry != NavMeshCollectGeometry.PhysicsColliders)
                {
                    issues.Add("NavMeshSurface should build from physics colliders.");
                }

                if (surface.navMeshData == null)
                {
                    issues.Add("NavMeshSurface has no baked navMeshData.");
                }
            }

            for (int i = 0; i < RequiredWalkAreas.Length; i++)
            {
                Transform area = navigation.Find(RequiredWalkAreas[i]);
                if (area == null)
                {
                    issues.Add("Missing walk area: " + RequiredWalkAreas[i]);
                    continue;
                }

                BoxCollider collider = area.GetComponent<BoxCollider>();
                if (collider == null || collider.isTrigger)
                {
                    issues.Add("Walk area needs non-trigger BoxCollider: " + RequiredWalkAreas[i]);
                }
            }

            Transform anchorRoot = navigation.Find(AnchorRootName);
            if (anchorRoot == null)
            {
                issues.Add("Missing CatDestinationAnchors root.");
            }
            else if (anchorRoot.childCount < 6)
            {
                issues.Add("Expected at least 6 cat destination anchors, found " + anchorRoot.childCount + ".");
            }

            ValidateInterestPoints(navigation, issues);
            ValidateForbiddenZones(navigation, issues);
        }

        private static void ValidateInterestPoints(Transform navigation, List<string> issues)
        {
            CatInterestPointRegistry registry = navigation.GetComponent<CatInterestPointRegistry>();
            if (registry == null)
            {
                issues.Add("Runtime/Navigation missing CatInterestPointRegistry.");
            }
            else if (registry.Count < 8)
            {
                issues.Add("Expected at least 8 cat interest points in registry, found " + registry.Count + ".");
            }

            Transform interestRoot = navigation.Find(InterestRootName);
            if (interestRoot == null)
            {
                issues.Add("Missing CatInterestPoints root.");
                return;
            }

            CatInterestPoint[] points = interestRoot.GetComponentsInChildren<CatInterestPoint>(true);
            if (points.Length < 8)
            {
                issues.Add("Expected at least 8 CatInterestPoint components, found " + points.Length + ".");
            }
        }

        private static void ValidateForbiddenZones(Transform navigation, List<string> issues)
        {
            Transform forbiddenRoot = navigation.Find(ForbiddenRootName);
            if (forbiddenRoot == null)
            {
                issues.Add("Missing CatForbiddenZones root.");
                return;
            }

            CatForbiddenZone[] zones = forbiddenRoot.GetComponentsInChildren<CatForbiddenZone>(true);
            if (zones.Length == 0)
            {
                issues.Add("CatForbiddenZones root has no CatForbiddenZone children.");
                return;
            }

            bool hasRendererBoundsZone = false;
            bool hasManualOverrideZone = false;
            bool hasCenterPlatformZone = false;
            for (int i = 0; i < zones.Length; i++)
            {
                CatForbiddenZone zone = zones[i];
                BoxCollider collider = zone.GetComponent<BoxCollider>();
                if (collider == null || collider.isTrigger)
                {
                    issues.Add("Forbidden zone needs non-trigger BoxCollider: " + zone.name);
                }

                NavMeshModifier modifier = zone.GetComponent<NavMeshModifier>();
                if (modifier == null || !modifier.overrideArea || modifier.ignoreFromBuild)
                {
                    issues.Add("Forbidden zone needs active NavMeshModifier area override: " + zone.name);
                }
                else if (modifier.area != NavMesh.GetAreaFromName("Not Walkable") && NavMesh.GetAreaFromName("Not Walkable") >= 0)
                {
                    issues.Add("Forbidden zone should use Not Walkable area: " + zone.name);
                }

                if (zone.ProjectionScale < 1.049f)
                {
                    issues.Add("Forbidden zone projection scale should be at least 1.05: " + zone.name);
                }

                hasRendererBoundsZone |= zone.SourceKind == CatForbiddenZone.ZoneSourceKind.RendererBounds;
                hasManualOverrideZone |= zone.SourceKind == CatForbiddenZone.ZoneSourceKind.ManualOverride;
                hasCenterPlatformZone |= zone.name.Contains("CenterPawPlatform");
            }

            if (!hasRendererBoundsZone)
            {
                issues.Add("Expected at least one renderer-bounds forbidden zone fallback.");
            }

            if (!hasManualOverrideZone)
            {
                issues.Add("Expected at least one manual-override forbidden zone for complex scenery.");
            }

            if (!hasCenterPlatformZone)
            {
                issues.Add("Missing center paw platform manual forbidden zone.");
            }
        }

        private static void ValidateSystems(Transform systems, List<string> issues)
        {
            if (systems == null)
            {
                return;
            }

            MockRecognitionProvider recognitionProvider = RequireComponent<MockRecognitionProvider>(systems.gameObject, issues);
            RealtimeFeatureEngine featureEngine = RequireComponent<RealtimeFeatureEngine>(systems.gameObject, issues);
            RequireComponent<MockCatLLMClient>(systems.gameObject, issues);
            RequireComponent<PrivacyGateway>(systems.gameObject, issues);
            FocusFeedbackProvider feedbackProvider = RequireComponent<FocusFeedbackProvider>(systems.gameObject, issues);

            if (recognitionProvider != null)
            {
                RequireSerializedObject(recognitionProvider, "featureEngine", issues);
            }

            if (feedbackProvider != null)
            {
                RequireSerializedObject(feedbackProvider, "privacyGateway", issues);
            }

            if (featureEngine == null)
            {
                issues.Add("RealtimeFeatureEngine is required for local recognition features.");
            }
        }

        private static void ValidateUiBinding(List<string> issues)
        {
            CatLifeHomeUiController uiController = Object.FindAnyObjectByType<CatLifeHomeUiController>();
            if (uiController == null)
            {
                issues.Add("Missing CatLifeHomeUiController.");
                return;
            }

            RequireSerializedObject(uiController, "catBehaviorDriver", issues);
            RequireSerializedObject(uiController, "focusFeedbackProvider", issues);
            RequireSerializedObject(uiController, "catBubblePresenter", issues);

            CatCameraRangeIndicator indicator = uiController.GetComponent<CatCameraRangeIndicator>();
            if (indicator == null)
            {
                issues.Add("CatLifeHomeUiController missing CatCameraRangeIndicator.");
            }
            else if (!indicator.HasCoreReferences)
            {
                issues.Add("CatCameraRangeIndicator has missing core references.");
            }
        }

        private static void ValidateAnimatorAssets(List<string> issues)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                issues.Add("Missing AnimatorController: " + ControllerPath);
                return;
            }

            for (int i = 0; i < RequiredAnimatorParameters.Length; i++)
            {
                if (!HasParameter(controller, RequiredAnimatorParameters[i]))
                {
                    issues.Add("AnimatorController missing parameter: " + RequiredAnimatorParameters[i]);
                }
            }

            AnimatorStateMachine stateMachine = controller.layers.Length > 0 ? controller.layers[0].stateMachine : null;
            if (stateMachine == null)
            {
                issues.Add("AnimatorController has no Base Layer state machine.");
                return;
            }

            for (int i = 0; i < RequiredStateNames.Length; i++)
            {
                if (!HasState(stateMachine, RequiredStateNames[i]))
                {
                    issues.Add("AnimatorController missing state: " + RequiredStateNames[i]);
                }
            }

            for (int i = 0; i < RequiredClipPaths.Length; i++)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RequiredClipPaths[i]);
                if (clip == null)
                {
                    issues.Add("Missing animation clip: " + RequiredClipPaths[i]);
                    continue;
                }

                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (!settings.loopTime)
                {
                    issues.Add("Animation clip is not looped: " + RequiredClipPaths[i]);
                }
            }
        }

        private static Transform FindTransform(string name, List<string> issues)
        {
            GameObject found = GameObject.Find(name);
            if (found == null)
            {
                issues.Add("Missing scene object: " + name);
                return null;
            }

            return found.transform;
        }

        private static T RequireComponent<T>(GameObject target, List<string> issues) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                issues.Add(target.name + " missing component: " + typeof(T).Name);
            }

            return component;
        }

        private static void RequireSerializedObject(Object target, string propertyName, List<string> issues)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                issues.Add(target.name + " missing serialized property: " + propertyName);
                return;
            }

            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
            {
                issues.Add(target.name + " has unassigned reference: " + propertyName);
            }
        }

        private static void RequireSerializedArray(Object target, string propertyName, int minSize, List<string> issues)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                issues.Add(target.name + " missing serialized property: " + propertyName);
                return;
            }

            if (!property.isArray || property.arraySize < minSize)
            {
                issues.Add(target.name + " has too few entries in " + propertyName + ".");
            }
        }

        private static void RequireSerializedBool(Object target, string propertyName, bool expectedValue, List<string> issues)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                issues.Add(target.name + " missing serialized property: " + propertyName);
                return;
            }

            if (property.propertyType != SerializedPropertyType.Boolean || property.boolValue != expectedValue)
            {
                issues.Add(target.name + " has invalid bool value for " + propertyName + ".");
            }
        }

        private static void RequireSerializedMinFloat(Object target, string propertyName, float minValue, List<string> issues)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                issues.Add(target.name + " missing serialized property: " + propertyName);
                return;
            }

            if (property.propertyType != SerializedPropertyType.Float || property.floatValue < minValue)
            {
                issues.Add(target.name + " has invalid float value for " + propertyName + ".");
            }
        }

        private static bool HasParameter(AnimatorController controller, string parameterName)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasState(AnimatorStateMachine stateMachine, string stateName)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == stateName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
