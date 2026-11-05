using System;
using System.IO;
using CatLife.Cat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class CatLifeV12WalkContactInstaller
{
    const string Candidate = "Assets/Editor/CatProneReview/CL_CAT_WalkContact_candidate_v12.anim";
    const string Runtime = "Assets/MobileRuntime/Art/Cat/AnimationsV12/CL_CAT_Walk_v01_loop_60f.anim";
    const string Scene = "Assets/Scenes/CatLifeMobile.unity";

    public static void Install()
    {
        var candidate = AssetDatabase.LoadAssetAtPath<AnimationClip>(Candidate);
        var runtime = AssetDatabase.LoadAssetAtPath<AnimationClip>(Runtime);
        if (candidate == null || runtime == null) throw new Exception("Walk clip was not prepared.");
        string backup = "../../Reports/cat-animation-v12/walk-unity-candidate/before-install/";
        Directory.CreateDirectory(backup);
        File.Copy(Runtime, backup + "CL_CAT_Walk_v01_loop_60f.anim", true);
        File.Copy(Scene, backup + "CatLifeMobile.unity", true);
        EditorUtility.CopySerialized(candidate, runtime);
        runtime.name = "CL_CAT_Walk_v01_loop_60f";
        EditorUtility.SetDirty(runtime);

        var scene = EditorSceneManager.OpenScene(Scene);
        var cat = GameObject.Find("CatLifeMobileCat");
        var nav = cat.GetComponent<CatNavigationAgent>();
        var serialized = new SerializedObject(nav);
        serialized.FindProperty("freeRoamSpeed").floatValue = 1.15f;
        serialized.FindProperty("focusedRoamSpeed").floatValue = 1.15f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        cat.GetComponent<NavMeshAgent>().speed = 1.15f;
        EditorUtility.SetDirty(nav);
        EditorUtility.SetDirty(cat.GetComponent<NavMeshAgent>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("V12_WALK_CONTACT_INSTALLED clip=" + Runtime + " baseSpeed=1.15m/s; playback follows actual movement");
    }
}
