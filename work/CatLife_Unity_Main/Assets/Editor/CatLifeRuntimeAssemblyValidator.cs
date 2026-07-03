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
        private const string ControllerPath = "Assets/Art/Cat/Animator/CatLife_TownWalker.controller";
        private const string ProjectStructurePath = "Assets/PROJECT_STRUCTURE.md";

        private static readonly string[] RequiredWalkAreas =
        {
            "CatWalkableArea_MainPlaza",
            "CatWalkableArea_LeftGardenPath",
            "CatWalkableArea_RightGardenPath",
            "CatWalkableArea_FrontStoneRing"
        };

        private static readonly string[] RequiredRootDirectories =
        {
            "Art",
            "Editor",
            "Materials",
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

            if (issues.Count == 0)
            {
                return "PASS CatLife runtime assembly validation: scene wiring, NavMesh runtime, cat behavior driver, recognition/LLM systems, UI binding, and 11 animator states are present.";
            }

            return "FAIL CatLife runtime assembly validation:\n- " + string.Join("\n- ", issues.ToArray());
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

            if (behaviorDriver != null)
            {
                RequireSerializedObject(behaviorDriver, "recognitionProviderComponent", issues);
                RequireSerializedObject(behaviorDriver, "llmClientComponent", issues);
                RequireSerializedObject(behaviorDriver, "navigationAgent", issues);
                RequireSerializedObject(behaviorDriver, "animationController", issues);
                RequireSerializedObject(behaviorDriver, "destinationPlanner", issues);
                RequireSerializedObject(behaviorDriver, "actionRouter", issues);
                RequireSerializedObject(behaviorDriver, "featureEngine", issues);
            }

            if (navigationAgent != null)
            {
                RequireSerializedObject(navigationAgent, "agent", issues);
            }

            if (safetyGuard != null)
            {
                RequireSerializedObject(safetyGuard, "agent", issues);
            }

            if (animationController != null)
            {
                RequireSerializedObject(animationController, "animator", issues);
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

            if (recognitionProvider != null)
            {
                RequireSerializedObject(recognitionProvider, "featureEngine", issues);
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
