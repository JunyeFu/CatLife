using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CatLifeRewardTurnCandidate
{
    const string Folder = "Assets/Editor/CatProneReview";
    public static void InstallPaw()
    {
        const string stateName = "CL_CAT_CrouchPawWave_v01_loop_96f";
        string target = CatLife.EditorTools.CatLifeRuntimeAnimatorSetup.ClipFolder + stateName + ".anim";
        string backup = "../../Reports/wp01-paw-install-20261013";
        Directory.CreateDirectory(backup);
        if (!File.Exists(backup + "/" + stateName + ".anim"))
            File.Copy(target, backup + "/" + stateName + ".anim");
        var candidate = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/CL_CAT_PawVisible_candidate.anim");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
        EditorUtility.CopySerialized(candidate, clip);
        clip.name = stateName;
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        Debug.Log("PAW_VISIBLE_INSTALLED " + target);
    }
    public static void InstallSequence()
    {
        const string stateName = "CL_CAT_RewardSideTailReturn_v01_384f";
        const string controllerPath = "Assets/MobileRuntime/Art/Cat/CL_CAT_Mobile.controller";
        string backup = "../../Reports/wp01-sequence-install-20261013";
        Directory.CreateDirectory(backup);
        string backupFile = backup + "/CL_CAT_Mobile.controller";
        if (!File.Exists(backupFile)) File.Copy(controllerPath, backupFile);
        string target = CatLife.EditorTools.CatLifeRuntimeAnimatorSetup.ClipFolder + stateName + ".anim";
        var candidate = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/CL_CAT_RewardSequence_candidate.anim");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
        if (clip == null)
        {
            clip = UnityEngine.Object.Instantiate(candidate);
            clip.name = stateName;
            AssetDatabase.CreateAsset(clip, target);
        }
        else { EditorUtility.CopySerialized(candidate, clip); clip.name = stateName; }
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName) ?? machine.AddState(stateName);
        state.motion = clip; state.speed = 1; state.writeDefaultValues = false;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("REWARD_SEQUENCE_INSTALLED " + target);
    }
    public static void Build()
    {
        BuildClip("../../Reports/wp01-reward-turn-20261013-right/CL_CAT_RewardSideStep_v01.fbx", "CL_CAT_RewardSideStep", 2);
    }
    public static void BuildSequence()
    {
        BuildClip("../../Reports/wp01-reward-sequence-20261013-outward/CL_CAT_RewardSequence_v01.fbx", "CL_CAT_RewardSequence", 6.4f);
    }
    public static void BuildPaw()
    {
        BuildClip("../../Reports/wp01-paw-visible-20261013/CL_CAT_PawVisible.fbx", "CL_CAT_PawVisible", 2.4f);
    }
    public static void BuildLocalTurns()
    {
        BuildClip("../../Reports/wp03-turn-left-local-20261013/CL_CAT_TurnLocalLeft.fbx", "CL_CAT_TurnLocalLeft", 2);
        BuildClip("../../Reports/wp03-turn-right-local-20261013/CL_CAT_TurnLocalRight.fbx", "CL_CAT_TurnLocalRight", 2);
    }
    public static void BuildRootCenteredTurns()
    {
        BuildClip("../../Reports/wp03-root-turn-20261022/left-local/CL_CAT_RootTurnLocalLeft.fbx", "CL_CAT_RootTurnLocalLeft", 2);
        BuildClip("../../Reports/wp03-root-turn-20261022/right-local/CL_CAT_RootTurnLocalRight.fbx", "CL_CAT_RootTurnLocalRight", 2);
    }
    public static void BuildTurnReferences()
    {
        BuildClip("../../Reports/wp03-turn-left-study-20261013/CL_CAT_TurnLeftStudy.fbx", "CL_CAT_TurnFullLeft", 2);
        BuildClip("../../Reports/wp01-reward-turn-20261013-right/CL_CAT_RewardSideStep_v01.fbx", "CL_CAT_TurnFullRight", 2);
    }
    static void BuildClip(string input, string name, float duration)
    {
        string source = Folder + "/" + name + "_v01.fbx";
        File.Copy(input, source, true);
        AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(source);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var take = AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
        var cat = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MobileRuntime/Art/Cat/PF_CL_CAT_Runtime_v12.prefab"));
        try
        {
            var clip = new AnimationClip { name = name, frameRate = 60 };
            foreach (var binding in AnimationUtility.GetCurveBindings(take))
            {
                if (binding.path != "" && cat.transform.Find(binding.path) == null)
                    throw new Exception("Reward turn binding missing: " + binding.path);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(take, binding));
            }
            var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
            string path = AnimationUtility.CalculateTransformPath(skin.transform, cat.transform);
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + skin.sharedMesh.GetBlendShapeName(i)), AnimationCurve.Constant(0, duration, 0));
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = duration; settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string target = Folder + "/" + name + "_candidate.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
            if (existing == null) AssetDatabase.CreateAsset(clip, target);
            else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); }
            AssetDatabase.SaveAssets();
            Debug.Log("REWARD_TURN_CANDIDATE_READY " + target);
        }
        finally { UnityEngine.Object.DestroyImmediate(cat); }
    }
}
