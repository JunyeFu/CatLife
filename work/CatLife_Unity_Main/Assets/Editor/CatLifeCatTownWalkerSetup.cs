using System.IO;
using CatLife.Cat;
using CatLife.EditorTools;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CatLifeCatTownWalkerSetup
{
    private const string WalkFbxPath = "Assets/Art/Cat/Animations/CL_CAT_SRC_Walk_60fps.fbx";
    private const string WalkClipPath = "Assets/Art/Cat/Animations/Clips/CL_CAT_SRC_Walk_60fps.anim";
    private const string IdleClipPath = "Assets/Art/Cat/Animations/Clips/CL_CAT_IdleBreath_v06_headsync_loop_108f.anim";
    private const string ControllerPath = "Assets/Art/Cat/Animator/CatLife_TownWalker.controller";
    private static readonly string[] SourceWalkRoots = { "Armature", "CL_CAT_Armature" };
    private const string RuntimeWalkRoot = "CL_CAT_CORRECTED_Armature";
    private const string IsWalkingParameter = "IsWalking";
    private const string WalkStateName = "CL_CAT_SRC_Walk_60fps";
    private const string IdleStateName = "CL_CAT_IdleBreath_v06_headsync_loop_108f";

    private static readonly Vector3 TownCatPosition = new Vector3(-0.3f, -0.02f, -6.78f);
    private static readonly Vector3 TownCatRotation = new Vector3(0f, -0.017f, 0f);
    private static readonly Vector2 TownCatPatrolSize = new Vector2(2.6f, 1.6f);
    private const float TownCatScale = 0.0275f;

    [MenuItem("CatLife/Configure Cat Town Walker")]
    public static void ConfigureMenu()
    {
        ConfigureSceneCat(GameObject.Find("CatCompanionModel"));
    }

    public static void ConfigureSceneCat(GameObject cat)
    {
        if (cat == null)
        {
            Debug.LogWarning("[CatLifeCatTownWalkerSetup] CatCompanionModel not found.");
            return;
        }

        EnsureFolders();
        ConfigureWalkImporter();
        AnimationClip walkClip = BuildRetargetedWalkClip();
        AnimatorController controller = BuildAnimatorController(walkClip);
        ConfigureCatObject(cat, controller);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Art/Cat", "Animator");
        EnsureFolder("Assets/Art/Cat/Animations", "Clips");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void ConfigureWalkImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(WalkFbxPath) as ModelImporter;
        if (importer == null)
        {
            AnimationClip generatedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
            if (generatedClip != null)
            {
                Debug.Log("[CatLifeCatTownWalkerSetup] Walk source FBX is archived; using generated runtime clip: " + WalkClipPath);
            }
            else
            {
                Debug.LogWarning("[CatLifeCatTownWalkerSetup] Missing walk FBX and generated clip: " + WalkFbxPath);
            }

            return;
        }

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }
        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            changed = true;
        }
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip BuildRetargetedWalkClip()
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
        AnimationClip source = FindWalkSourceClip();
        if (source == null)
        {
            if (existing != null)
            {
                Debug.Log("[CatLifeCatTownWalkerSetup] Walk source FBX is unavailable; using existing generated walk clip: " + WalkClipPath);
            }
            else
            {
                Debug.LogWarning("[CatLifeCatTownWalkerSetup] No AnimationClip found in " + WalkFbxPath + " and no generated walk clip exists.");
            }

            return existing;
        }

        if (existing != null)
        {
            AssetDatabase.DeleteAsset(WalkClipPath);
        }

        AnimationClip retargeted = new AnimationClip();
        retargeted.name = "CL_CAT_SRC_Walk_60fps";
        retargeted.frameRate = source.frameRate;
        retargeted.wrapMode = WrapMode.Loop;

        EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(source);
        for (int i = 0; i < curveBindings.Length; i++)
        {
            EditorCurveBinding binding = curveBindings[i];
            binding.path = RetargetPath(binding.path);
            if (ShouldSkipRetargetedTransformCurve(binding))
            {
                continue;
            }

            AnimationUtility.SetEditorCurve(retargeted, binding, AnimationUtility.GetEditorCurve(source, curveBindings[i]));
        }

        EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(source);
        for (int i = 0; i < objectBindings.Length; i++)
        {
            EditorCurveBinding binding = objectBindings[i];
            binding.path = RetargetPath(binding.path);
            AnimationUtility.SetObjectReferenceCurve(retargeted, binding, AnimationUtility.GetObjectReferenceCurve(source, objectBindings[i]));
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(retargeted);
        settings.loopTime = true;
        settings.loopBlend = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalOrientation = true;
        AnimationUtility.SetAnimationClipSettings(retargeted, settings);

        AssetDatabase.CreateAsset(retargeted, WalkClipPath);
        EditorUtility.SetDirty(retargeted);
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
    }

    private static bool ShouldSkipRetargetedTransformCurve(EditorCurveBinding binding)
    {
        if (binding.type != typeof(Transform))
        {
            return false;
        }

        return binding.propertyName.StartsWith("m_LocalPosition.") ||
            binding.propertyName.StartsWith("m_LocalScale.");
    }

    private static AnimationClip FindWalkSourceClip()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(WalkFbxPath);
        AnimationClip fallback = null;
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null)
            {
                continue;
            }

            if (clip.name == "Scene")
            {
                return clip;
            }

            if (!clip.name.StartsWith("__preview__") && (fallback == null || clip.length > fallback.length))
            {
                fallback = clip;
            }
        }

        return fallback;
    }

    private static string RetargetPath(string path)
    {
        for (int i = 0; i < SourceWalkRoots.Length; i++)
        {
            string sourceWalkRoot = SourceWalkRoots[i];
            if (path == sourceWalkRoot)
            {
                return RuntimeWalkRoot;
            }

            if (path.StartsWith(sourceWalkRoot + "/"))
            {
                return RuntimeWalkRoot + path.Substring(sourceWalkRoot.Length);
            }
        }

        return path;
    }

    private static AnimatorController BuildAnimatorController(AnimationClip walkClip)
    {
        AnimatorController controller = CatLifeRuntimeAnimatorSetup.EnsureController();
        CatLifeRuntimeAnimatorSetup.RebuildControllerStates(controller);
        return controller;
    }

    private static bool HasParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType parameterType)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName && parameters[i].type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = states.Length - 1; i >= 0; i--)
        {
            stateMachine.RemoveState(states[i].state);
        }

        AnimatorStateTransition[] anyStateTransitions = stateMachine.anyStateTransitions;
        for (int i = anyStateTransitions.Length - 1; i >= 0; i--)
        {
            stateMachine.RemoveAnyStateTransition(anyStateTransitions[i]);
        }

        AnimatorTransition[] entryTransitions = stateMachine.entryTransitions;
        for (int i = entryTransitions.Length - 1; i >= 0; i--)
        {
            stateMachine.RemoveEntryTransition(entryTransitions[i]);
        }
    }

    private static void ConfigureCatObject(GameObject cat, RuntimeAnimatorController controller)
    {
        cat.transform.position = TownCatPosition;
        cat.transform.rotation = Quaternion.Euler(TownCatRotation);
        cat.transform.localScale = Vector3.one * TownCatScale;

        Animator animator = cat.GetComponent<Animator>();
        if (animator == null)
        {
            animator = cat.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        ConfigureAnimatedRenderers(cat);

        CatTownWalker walker = cat.GetComponent<CatTownWalker>();
        if (walker == null)
        {
            walker = cat.AddComponent<CatTownWalker>();
        }

        SerializedObject serialized = new SerializedObject(walker);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("isWalkingParameter").stringValue = IsWalkingParameter;
        serialized.FindProperty("walkStateName").stringValue = WalkStateName;
        serialized.FindProperty("idleStateName").stringValue = IdleStateName;
        serialized.FindProperty("xRange").vector2Value = new Vector2(-1.8f, 1.2f);
        serialized.FindProperty("zRange").vector2Value = new Vector2(-7.8f, -5.8f);
        serialized.FindProperty("localPatrolSize").vector2Value = TownCatPatrolSize;
        serialized.FindProperty("groundY").floatValue = TownCatPosition.y;
        serialized.FindProperty("walkSpeed").floatValue = 1.15f;
        serialized.FindProperty("turnSpeed").floatValue = 5.5f;
        serialized.FindProperty("waitSecondsRange").vector2Value = new Vector2(0.05f, 0.18f);
        serialized.FindProperty("initialIdleSeconds").floatValue = 0f;
        serialized.FindProperty("targetMinDistance").floatValue = 0.45f;
        serialized.FindProperty("targetTolerance").floatValue = 0.08f;
        serialized.FindProperty("maxMovementDeltaTime").floatValue = 0.05f;
        serialized.FindProperty("walkTransitionSeconds").floatValue = 0.12f;
        serialized.FindProperty("idleTransitionSeconds").floatValue = 0.16f;
        serialized.FindProperty("startWalkingOnEnable").boolValue = true;
        serialized.FindProperty("useCurrentPositionAsPatrolCenter").boolValue = true;
        serialized.FindProperty("keepSkinnedMeshVisibleWhileAnimated").boolValue = true;
        serialized.FindProperty("skinnedMeshLocalBounds").boundsValue = new Bounds(new Vector3(0f, 0.18f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(cat);
        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(walker);
    }

    private static void ConfigureAnimatedRenderers(GameObject cat)
    {
        SkinnedMeshRenderer[] skinnedRenderers = cat.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
            skinnedRenderer.updateWhenOffscreen = true;
            skinnedRenderer.localBounds = new Bounds(new Vector3(0f, 0.18f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
            EditorUtility.SetDirty(skinnedRenderer);
        }
    }
}
