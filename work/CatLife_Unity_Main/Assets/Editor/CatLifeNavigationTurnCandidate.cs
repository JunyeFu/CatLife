using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CatLifeNavigationTurnCandidate
{
    const string Folder = "Assets/Editor/CatProneReview/NavigationTurnCandidate";
    [Serializable] sealed class Sample { public float yaw_degrees; public float[] root_translation; }
    [Serializable] sealed class Schedule { public Sample[] samples; }

    public static void Build()
    {
        CatLifeRewardTurnCandidate.BuildRootCenteredTurns();
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene("Assets/Editor/CatProneReview/FigmaUiCandidate/CatLifeFigmaUiReview.unity");
        var cat = GameObject.Find("CatLifeMobileCat");
        var animator = cat.GetComponent<Animator>();
        string controllerPath = Folder + "/CL_CAT_NavigationTurn.controller";
        if (!File.Exists(controllerPath))
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), controllerPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        var navigation = new SerializedObject(cat.GetComponent("CatNavigationAgent"));
        var planner = new SerializedObject(cat.GetComponent("CatDestinationPlanner"));
        planner.FindProperty("nonFocusSampleRadius").floatValue = 1.6f;
        planner.FindProperty("minMoveDistance").floatValue = .35f;
        planner.ApplyModifiedPropertiesWithoutUndo();
        foreach (string side in new[] { "Left", "Right" })
        {
            string stateName = "CL_CAT_TurnLocal" + side;
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName) ?? machine.AddState(stateName);
            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Editor/CatProneReview/CL_CAT_RootTurnLocal" + side + "_candidate.anim");
            // Navigation samples the clip and root at the same phase. The entry
            // crossfade holds frame zero rather than advancing then rewinding it.
            state.speed = 0; state.writeDefaultValues = false;
            var schedule = JsonUtility.FromJson<Schedule>(File.ReadAllText("../../Reports/wp03-root-turn-20261022/" + side.ToLowerInvariant() + "-local/root-schedule.json"));
            var x = new AnimationCurve(); var z = new AnimationCurve(); var yaw = new AnimationCurve();
            for (int i = 0; i < schedule.samples.Length; i++)
            {
                var sample = schedule.samples[i]; float t = i / 120f;
                x.AddKey(t, -sample.root_translation[0] * .045f);
                z.AddKey(t, -sample.root_translation[1] * .045f);
                yaw.AddKey(t, -sample.yaw_degrees);
            }
            var step = navigation.FindProperty(side.ToLowerInvariant() + "Turn");
            step.FindPropertyRelative("stateName").stringValue = stateName;
            step.FindPropertyRelative("seconds").floatValue = 1;
            step.FindPropertyRelative("x").animationCurveValue = x;
            step.FindPropertyRelative("z").animationCurveValue = z;
            step.FindPropertyRelative("yaw").animationCurveValue = yaw;
        }
        navigation.ApplyModifiedPropertiesWithoutUndo();
        animator.runtimeAnimatorController = controller;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, Folder + "/CatLifeNavigationTurnReview.unity");
        Debug.Log("NAVIGATION_TURN_CANDIDATE_READY " + Folder);
    }
}
