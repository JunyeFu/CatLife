using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CatLife.EditorTools;

public static class CatLifeProneSceneInstaller
{
    const string Art="Assets/MobileRuntime/Art/Cat/Source/";
    const string Scene="Assets/Scenes/CatLifeMobile.unity";
    public static void Install()
    {
        var backup="Reports/ProneImportReview/backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(Scene,backup+"/CatLifeMobile.unity");
        File.Copy("Assets/MobileRuntime/Art/Cat/CL_CAT_Mobile.controller",backup+"/CL_CAT_Mobile.controller");
        CatLifeProneImportReview.Configure(Art+"CL_CAT_Prone_v11.fbx");
        string legacy=Art+"CL_CAT_Legacy_v11.fbx";
        AssetDatabase.ImportAsset(legacy,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(legacy);
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        var clips=importer.defaultClipAnimations;
        foreach(var clip in clips) clip.loopTime=clip.takeName.Contains("_loop_");
        importer.clipAnimations=clips;
        importer.SaveAndReimport();
        EditorSceneManager.OpenScene(Scene);
        var cat=GameObject.Find("CatLifeMobileCat");
        var animator=cat.GetComponent<Animator>();
        if(animator==null) throw new InvalidOperationException("Scene cat must keep its root Animator references.");
        var oldSkin=cat.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
        var material=oldSkin.sharedMaterial;
        var newModel=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"CL_CAT_Prone_v11.fbx"));
        PrefabUtility.UnpackPrefabInstance(newModel,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        var oldChildren=cat.transform.Cast<Transform>().Where(t=>t.name=="CL_CAT_CORRECTED_Armature" || t.name=="CL_CAT_CORRECTED_Mesh").ToArray();
        if(oldChildren.Length==0) throw new InvalidOperationException("Known cat art hierarchy missing; scene not changed.");
        if(PrefabUtility.IsPartOfPrefabInstance(cat))
            PrefabUtility.UnpackPrefabInstance(cat,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
        foreach(var child in oldChildren) UnityEngine.Object.DestroyImmediate(child.gameObject);
        foreach(var child in newModel.transform.Cast<Transform>().ToArray()) child.SetParent(cat.transform,false);
        UnityEngine.Object.DestroyImmediate(newModel);
        var skin=cat.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
        skin.sharedMaterial=material;
        skin.updateWhenOffscreen=true;
        for(int i=0;i<skin.sharedMesh.blendShapeCount;i++) skin.SetBlendShapeWeight(i,0);
        animator.avatar=null;
        var controller=CatLifeRuntimeAnimatorSetup.EnsureController();
        CatLifeRuntimeAnimatorSetup.RebuildControllerStates(controller);
        animator.runtimeAnimatorController=controller;
        animator.applyRootMotion=false;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(cat);
        EditorSceneManager.MarkSceneDirty(cat.scene);
        EditorSceneManager.SaveScene(cat.scene);
        AssetDatabase.SaveAssets();
        File.WriteAllText("Reports/ProneImportReview/installed.txt","Scene cat art replaced; root components, material, collider and UI retained. Backup="+backup);
        Debug.Log("PRONE_SCENE_INSTALLED backup="+backup);
    }
}
