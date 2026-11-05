using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

// Isolated integration only. No production scene or runtime asset is replaced.
public static class CatLifeV12ControllerCandidate
{
    const string Review = "Assets/Editor/CatProneReview/";
    static bool FullWeights => Environment.GetCommandLineArgs().Contains("-fullWeights");
    static string Folder => Review + (FullWeights ? "V12FullWeightsController/" : "V12Controller/");
    const string Stand = "CL_CAT_StandIdle_v01_loop_96f";
    static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
    {
        { "CL_CAT_LieFocusRest_v01_loop_96f", "CL_CAT_ProneBreath_v11_loop_240f" },
        { "CL_CAT_LieFocusAttention_v01_48f", "CL_CAT_ProneAttention_v11_48f" }
    };

    public static void BuildFullWeights()
    {
        var previous = QualitySettings.skinWeights;
        try { QualitySettings.skinWeights = SkinWeights.Unlimited; Build(); }
        finally { QualitySettings.skinWeights = previous; }
    }

    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Review +
            (FullWeights ? "PF_CL_CAT_CommonFullWeights_v12.prefab" : "PF_CL_CAT_CommonSitProne_v12.prefab")));
        var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
        string skinPath = AnimationUtility.CalculateTransformPath(skin.transform, model.transform);
        var rows = File.ReadAllLines("../../08-handoff-docs/planning/CatLife_v12动画接入清单.csv")
            .Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Split(',')).ToArray();
        var clips = new Dictionary<string, AnimationClip>();
        var sourceClips = new Dictionary<string, AnimationClip>();
        var startTimes = new Dictionary<string, float>();
        foreach (var row in rows.Where(r => !Aliases.ContainsKey(r[0])))
        {
            string state = row[0];
            var source = AssetDatabase.LoadAllAssetsAtPath(Review + row[1]).OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__"));
            int first = int.Parse(row[2]), last = int.Parse(row[3]);
            if (last / 60f > source.length + .0001f) throw new Exception("Clip range exceeds source: " + state);
            var clip = new AnimationClip { name = state, frameRate = 60 };
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.path != "" && model.transform.Find(binding.path) == null)
                    throw new Exception("Unresolved candidate binding: " + binding.path);
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                var keys = Enumerable.Range(first, last - first + 1)
                    .Select(f => new Keyframe((f - first) / 60f, curve.Evaluate(f / 60f))).ToArray();
                var sliced = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(sliced, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(sliced, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, binding, sliced);
            }
            // Every state owns every shape, so previous pose corrections cannot linger.
            // This is clip data, not a second per-frame writer.
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
            {
                var binding = EditorCurveBinding.FloatCurve(skinPath, typeof(SkinnedMeshRenderer), "blendShape." + skin.sharedMesh.GetBlendShapeName(i));
                if (AnimationUtility.GetEditorCurve(clip, binding) == null)
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, (last - first) / 60f, 0));
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0;
            settings.stopTime = (last - first) / 60f;
            settings.loopTime = bool.Parse(row[4]);
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string path = Folder + state + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) AssetDatabase.CreateAsset(clip, path);
            else { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); clip = existing; }
            clips.Add(state, clip);
            sourceClips.Add(state, source);
            startTimes.Add(state, first / 60f);
        }
        foreach (var alias in Aliases)
        {
            clips.Add(alias.Key, clips[alias.Value]);
            sourceClips.Add(alias.Key, sourceClips[alias.Value]);
            startTimes.Add(alias.Key, startTimes[alias.Value]);
        }
        string controllerPath = Folder + "CL_CAT_V12Candidate.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        // A previous interrupted build left this candidate asset with no layers.
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        foreach (var row in rows)
        {
            var state = machine.AddState(row[0]);
            state.motion = clips[row[0]];
            state.writeDefaultValues = false;
            if (row[0] == Stand) machine.defaultState = state;
        }
        AssetDatabase.SaveAssets();

        var mesh = new Mesh();
        Vector3[] Sample(AnimationClip clip, float time)
        {
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, 0);
            clip.SampleAnimation(model, time);
            skin.BakeMesh(mesh, false);
            return mesh.vertices;
        }
        var errors = new Dictionary<string, float>();
        foreach (var row in rows)
        {
            float max = 0;
            foreach (float fraction in new[] { 0f, .5f, 1f })
            {
                float time = clips[row[0]].length * fraction;
                var a = Sample(sourceClips[row[0]], startTimes[row[0]] + time);
                var b = Sample(clips[row[0]], time);
                max = Mathf.Max(max, a.Select((p, i) => Vector3.Distance(p, b[i])).Max());
            }
            errors.Add(row[0], max);
            if (max > .0001f) throw new Exception("Sliced mesh differs from imported source: " + row[0] + " " + max);
        }
        Sample(clips[Stand], 0);
        // The CPU review prefab deliberately contains no Animator.
        var animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0);
        var report = new List<string> { "state,seconds,sliceMeshError,animatorMeshError,returnStandShapeError" };
        var reference = Object.Instantiate(model);
        reference.GetComponent<Animator>().runtimeAnimatorController = null;
        var referenceSkin = reference.GetComponentInChildren<SkinnedMeshRenderer>();
        var referenceMesh = new Mesh();
        foreach (var row in rows)
        {
            string state = row[0];
            animator.Play(state, 0, .5f);
            animator.Update(0);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName(state)) throw new Exception("Animator did not enter " + state);
            clips[state].SampleAnimation(reference, clips[state].length * .5f);
            skin.BakeMesh(mesh, false);
            referenceSkin.BakeMesh(referenceMesh, false);
            var expected = referenceMesh.vertices;
            float difference = mesh.vertices.Select((p, i) => Vector3.Distance(p, expected[i])).Max();
            if (difference > .0001f) throw new Exception("Animator differs from sample: " + state + " " + difference);
            animator.CrossFadeInFixedTime(Stand, .15f, 0, 0);
            for (int frame = 0; frame < 15; frame++) animator.Update(1f / 60);
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName(Stand) || animator.IsInTransition(0))
                throw new Exception("Stand return incomplete: " + state);
            float shapeError = 0;
            float standTime = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1) * clips[Stand].length;
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
            {
                var binding = EditorCurveBinding.FloatCurve(skinPath, typeof(SkinnedMeshRenderer), "blendShape." + skin.sharedMesh.GetBlendShapeName(i));
                float value = AnimationUtility.GetEditorCurve(clips[Stand], binding).Evaluate(standTime);
                shapeError = Mathf.Max(shapeError, Mathf.Abs(skin.GetBlendShapeWeight(i) - value));
            }
            if (shapeError > .001f) throw new Exception("Residual correction after " + state + ": " + shapeError);
            report.Add(FormattableString.Invariant($"{state},{clips[state].length:R},{errors[state]:R},{difference:R},{shapeError:R}"));
        }
        animator.Play(Stand, 0, 0); animator.Update(0);
        PrefabUtility.SaveAsPrefabAsset(model, Folder + "PF_CL_CAT_V12Candidate.prefab");
        AssetDatabase.SaveAssets();
        string reportPath = "../../Reports/cat-animation-v12/controller-candidate" + (FullWeights ? "-fullweights" : "");
        File.WriteAllLines(reportPath + ".csv", report);
        File.WriteAllText(reportPath + ".txt",
            $"{rows.Length} states; {clips.Values.Distinct().Count()} extracted clips; {skin.sharedMesh.blendShapeCount} shapes owned by each clip.\n" +
            $"Full stored influences={FullWeights}; global evaluation={QualitySettings.skinWeights}.\n" +
            "Three-point source comparison, editor Animator midpoint and return-to-Stand shape checks only.\n" +
            "Not PlayMode, anatomical transition, terrain, navigation or Android acceptance. No production assets replaced.\n");
        Object.DestroyImmediate(referenceMesh); Object.DestroyImmediate(mesh);
        Object.DestroyImmediate(reference); Object.DestroyImmediate(model);
        Debug.Log("V12_CONTROLLER_CANDIDATE_FINISHED: " + rows.Length + " states");
    }
}
