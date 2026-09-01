using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CatLifeMobileSizeIsolation
{
    public static void BuildEmptyAndroidBaselineBatch()
    {
        string scenePath = "Assets/Scenes/CatLifeEmptySizeProbe.unity";
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject camera = new GameObject("Main Camera", typeof(Camera));
        camera.tag = "MainCamera";
        EditorSceneManager.SaveScene(scene, scenePath);

        string output = "build/CatLife_EmptySizeProbe.apk";
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("CATLIFE_EMPTY_SIZE_PROBE result=" + report.summary.result + " size=" + report.summary.totalSize + " output=" + output);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
