using System;
using System.IO;
using System.Linq;
using CatLife.Cat;
using CatLife.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CatLifeV12SceneInstaller
{
    const string Art = "Assets/MobileRuntime/Art/Cat/";
    const string Candidate = "Assets/Editor/CatProneReview/V12FullWeightsController/";
    const string Scene = "Assets/Scenes/CatLifeMobile.unity";

    public static void Install()
    {
        string backup = "../../Reports/cat-animation-v12/scene-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(Scene, backup + "/CatLifeMobile.unity");
        File.Copy(Art + "CL_CAT_Mobile.controller", backup + "/CL_CAT_Mobile.controller");
        File.Copy("ProjectSettings/QualitySettings.asset", backup + "/QualitySettings.asset");
        Directory.CreateDirectory(CatLifeRuntimeAnimatorSetup.ClipFolder);
        AssetDatabase.Refresh();
        foreach (string path in Directory.GetFiles(Candidate, "*.anim"))
        {
            string destination = CatLifeRuntimeAnimatorSetup.ClipFolder + Path.GetFileName(path);
            if (!AssetDatabase.CopyAsset(path, destination)) throw new Exception("Could not copy candidate clip: " + destination);
        }
        EditorSceneManager.OpenScene(Scene);
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Candidate + "PF_CL_CAT_V12Candidate.prefab"));
        var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
        using (var counts = skin.sharedMesh.GetBonesPerVertex())
            if (counts.ToArray().Max() != 9) throw new Exception("Expected accepted full-weight mesh.");
        var mesh = Object.Instantiate(skin.sharedMesh);
        mesh.name = "MSH_CL_CAT_Runtime_v12";
        AssetDatabase.CreateAsset(mesh, Art + mesh.name + ".asset");
        skin.sharedMesh = mesh;
        skin.quality = SkinQuality.Auto;
        for (int i = 0; i < mesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, 0);
        var controller = CatLifeRuntimeAnimatorSetup.EnsureController();
        CatLifeRuntimeAnimatorSetup.RebuildControllerStates(controller);
        model.GetComponent<Animator>().runtimeAnimatorController = controller;

        var cat = GameObject.Find("CatLifeMobileCat");
        var animator = cat.GetComponent<Animator>();
        var oldSkin = cat.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
        skin.sharedMaterial = oldSkin.sharedMaterial;
        var oldChildren = cat.transform.Cast<Transform>().Where(t =>
            t.name == "CL_CAT_CORRECTED_Armature" || t.name == "CL_CAT_CORRECTED_Mesh").ToArray();
        if (oldChildren.Length != 2) throw new Exception("Expected the two known cat art roots.");
        // Only known art roots are replaced. Root Animator, physics, navigation and business components keep their IDs.
        var position = cat.transform.position;
        var scale = cat.transform.localScale;
        var rotation = cat.transform.rotation;
        model.name = "PF_CL_CAT_Runtime_v12";
        PrefabUtility.SaveAsPrefabAsset(model, Art + model.name + ".prefab");
        if (PrefabUtility.IsPartOfPrefabInstance(cat))
            PrefabUtility.UnpackPrefabInstance(cat, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        foreach (var child in oldChildren) Object.DestroyImmediate(child.gameObject);
        foreach (Transform child in model.transform.Cast<Transform>().ToArray()) child.SetParent(cat.transform, false);
        Object.DestroyImmediate(model);
        foreach (var driver in cat.GetComponents<CatCrouchShapeDriver>()) Object.DestroyImmediate(driver);
        animator.avatar = null;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (cat.transform.position != position || cat.transform.localScale != scale || cat.transform.rotation != rotation)
            throw new Exception("Physical root changed during art replacement.");
        int previousQuality = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.skinWeights = SkinWeights.Unlimited;
        }
        QualitySettings.SetQualityLevel(previousQuality, false);
        EditorSceneManager.MarkSceneDirty(cat.scene);
        EditorSceneManager.SaveScene(cat.scene);
        AssetDatabase.SaveAssets();
        string[] dependencies = AssetDatabase.GetDependencies(Scene, true);
        if (dependencies.Any(p => p.StartsWith("Assets/Editor/") || p == Art + "Source/CL_CAT_Prone_v11.fbx" || p == Art + "Source/CL_CAT_Legacy_v11.fbx"))
            throw new Exception("Study or old cat FBX remains in scene dependencies.");
        File.WriteAllText("../../Reports/cat-animation-v12/formal-installed.txt",
            $"Installed v12: one mesh, 20 clips, 22 states, 39 shapes, full skin weights.\nRoot retained: {position}, {rotation}, {scale}.\nBackup={backup}\nFormal PlayMode and Android not yet verified.\n");
        Debug.Log("V12_FORMAL_INSTALLED backup=" + backup);
    }
}
