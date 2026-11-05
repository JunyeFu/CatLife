using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatLife.Editor
{
    public static class CatLifeSitSniffCandidate
    {
        public const string Folder = "Assets/Editor/CatProneReview/RoamSitChainCandidate";
        public const string ScenePath = Folder + "/CatLifeSitSniffReview.unity";
        public const string StateName = "CL_CAT_SitSniffStand_v01_280f";

        public static void Build()
        {
            EditorSceneManager.OpenScene(CatLifeTownAtlasReview.ScenePath);
            var cat = GameObject.Find("CatLifeMobileCat");
            var animator = cat.GetComponent<Animator>();
            AnimationClip Source(string name) => animator.runtimeAnimatorController.animationClips.Single(clip => clip.name == name);
            var stand = Source("CL_CAT_StandIdle_v01_loop_96f");
            var down = Source("CL_CAT_StandToSit_v01_72f");
            var sniff = Source("CL_CAT_SitCuriousSniff_v01_loop_112f");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var sequence = new AnimationClip { name = StateName, frameRate = 60 };
            var bindings = AnimationUtility.GetCurveBindings(stand).Union(AnimationUtility.GetCurveBindings(down))
                .Union(AnimationUtility.GetCurveBindings(sniff));
            float seamError = 0;
            foreach (var binding in bindings)
            {
                var a = AnimationUtility.GetEditorCurve(stand, binding);
                var b = AnimationUtility.GetEditorCurve(down, binding);
                var c = AnimationUtility.GetEditorCurve(sniff, binding);
                if (a == null || b == null || c == null)
                    throw new InvalidOperationException("Sequence source binding mismatch: " + binding.path + "/" + binding.propertyName);
                // Quaternion q/-q is the same orientation. Continuity is repaired
                // after resampling, not mistaken for an anatomical seam.
                if (!binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                {
                    seamError = Mathf.Max(seamError, Mathf.Abs(a.Evaluate(0) - b.Evaluate(0)));
                    seamError = Mathf.Max(seamError, Mathf.Abs(b.Evaluate(down.length) - c.Evaluate(0)));
                    seamError = Mathf.Max(seamError, Mathf.Abs(c.Evaluate(sniff.length) - b.Evaluate(down.length)));
                }
                var keys = new Keyframe[281];
                for (int frame = 0; frame <= 280; frame++)
                {
                    float value;
                    if (frame <= 12) value = b.Evaluate(0);
                    else if (frame <= 84) value = b.Evaluate((frame - 12) / 60f);
                    else if (frame <= 196) value = c.Evaluate((frame - 84) / 60f);
                    else if (frame <= 268) value = b.Evaluate((268 - frame) / 60f);
                    else value = b.Evaluate(0);
                    keys[frame] = new Keyframe(frame / 60f, value);
                }
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(sequence, binding, curve);
            }
            if (seamError > .001f) throw new InvalidOperationException("Non-rotation source seam differs: " + seamError);
            sequence.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(sequence);
            settings.loopTime = false; settings.startTime = 0; settings.stopTime = 280 / 60f;
            AnimationUtility.SetAnimationClipSettings(sequence, settings);
            string clipPath = Folder + "/" + StateName + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing == null) AssetDatabase.CreateAsset(sequence, clipPath);
            else { EditorUtility.CopySerialized(sequence, existing); UnityEngine.Object.DestroyImmediate(sequence); sequence = existing; }

            string controllerPath = Folder + "/CL_CAT_SitSniffReview.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), controllerPath))
                    throw new InvalidOperationException("Could not isolate the current runtime controller.");
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            }
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(item => item.state).SingleOrDefault(item => item.name == StateName) ?? machine.AddState(StateName);
            state.motion = sequence; state.speed = 1; state.writeDefaultValues = false;
            EditorUtility.SetDirty(controller);
            animator.runtimeAnimatorController = controller;
            var control = new SerializedObject(cat.GetComponent("CatAnimationController"));
            control.FindProperty("curiousSniffStateName").stringValue = StateName;
            control.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp03-sit-chain-candidate-20261017"));
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "source-map.txt"),
                "Isolated scene=" + ScenePath + "\n" +
                "Stand source=" + AssetDatabase.GetAssetPath(stand) + "\n" +
                "Sit source=" + AssetDatabase.GetAssetPath(down) + "\n" +
                "Sniff source=" + AssetDatabase.GetAssetPath(sniff) + "\n" +
                "Frames 0-12 steady; 12-84 authored sit; 84-196 sniff; 196-268 reverse of authored sit; 268-280 steady.\n" +
                "The rise is a reversed existing keyframed sit, not a newly anatomically approved asset.\n" +
                "Non-rotation seam max=" + seamError.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "\n" +
                "Formal scene, accepted reward tail, mesh and camera unchanged; isolated preparation pending visual approval.\n");
            Debug.Log("CATLIFE_SIT_SNIFF_CANDIDATE " + ScenePath + " length=" + sequence.length);
        }
    }
}
