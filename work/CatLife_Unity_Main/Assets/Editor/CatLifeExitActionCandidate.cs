using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CatLifeExitActionCandidate
{
    const string Folder = "Assets/Editor/CatProneReview/ExitCandidate";
    static readonly string[] Names = { "CL_CAT_ProneExitReach_v01_24f", "CL_CAT_ProneExitHold_v01_loop_48f", "CL_CAT_ProneExitRelease_v01_24f" };
    public static void SaveUiContactCandidate(Vector3 contact, Vector3[] nearPoints, Vector3[] farPoints, string nearPath, string farPath)
    {
        string materialPath = Folder + "/MAT_CL_UI_PawContactCandidate.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/CL_UI_PawContactCandidate.shader"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var ui = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MobileRuntime/UI/PF_CL_UI_Mobile.prefab"));
        try
        {
            ui.name = "PF_CL_UI_ExitCandidate";
            var transforms = ui.GetComponentsInChildren<Transform>(true);
            var track = transforms.Single(t => t.name == "SwipeTrack");
            track.gameObject.AddComponent<CatLifePawContactView>().Configure(contact, nearPoints, farPoints, nearPath, farPath, material);
            var label = transforms.Single(t => t.name == "SwipeLabel").GetComponent<Text>();
            label.text = "继续右滑可结束";
            label.rectTransform.anchorMin = new Vector2(.53f, 0);
            label.rectTransform.anchorMax = new Vector2(.97f, 1);
            var card = (RectTransform)transforms.Single(t => t.name == "ExitConfirmCard");
            card.anchorMin = new Vector2(.08f, .06f); card.anchorMax = new Vector2(.92f, .28f);
            AddControls(ui);
            PrefabUtility.SaveAsPrefabAsset(ui, Folder + "/PF_CL_UI_ExitCandidate.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log($"EXIT_UI_PREFAB_READY paw points={nearPoints.Length}+{farPoints.Length}; original UI unchanged");
        }
        finally { UnityEngine.Object.DestroyImmediate(ui); }
    }
    public static void InstallControls()
    {
        string path = Folder + "/PF_CL_UI_ExitCandidate.prefab";
        var ui = PrefabUtility.LoadPrefabContents(path);
        try { AddControls(ui); PrefabUtility.SaveAsPrefabAsset(ui, path); AssetDatabase.SaveAssets(); }
        finally { PrefabUtility.UnloadPrefabContents(ui); }
        Debug.Log("EXIT_CONTROLS_READY candidate settings and one-shot prompt serialized");
    }
    public static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity", OpenSceneMode.Single);
        var app = UnityEngine.Object.FindFirstObjectByType<CatLifeMobileApp>();
        var appData = new SerializedObject(app);
        var oldUi = (GameObject)appData.FindProperty("viewRoot").objectReferenceValue;
        var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PF_CL_UI_ExitCandidate.prefab"), scene);
        ui.name = "CatLifeMobileView";
        appData.FindProperty("viewRoot").objectReferenceValue = ui;
        appData.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.Object.DestroyImmediate(oldUi);
        var cat = GameObject.Find("CatLifeMobileCat");
        var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/RuntimeIntegration/PF_CL_CAT_ExitIntegration_candidate.prefab");
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var source = candidate.GetComponentInChildren<SkinnedMeshRenderer>();
        var bones = cat.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
        skin.bones = source.bones.Select(b => bones[b.name]).ToArray(); skin.sharedMesh = source.sharedMesh;
        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, 0);
        var animator = cat.GetComponent<Animator>();
        animator.runtimeAnimatorController = candidate.GetComponent<Animator>().runtimeAnimatorController;
        PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, Folder + "/CatLifeExitCandidate.unity")) throw new Exception("Candidate scene was not saved.");
        Debug.Log("EXIT_SCENE_READY cold-start scene saved; original scene not overwritten");
    }
    private static void AddControls(GameObject ui)
    {
        var transforms = ui.GetComponentsInChildren<Transform>(true);
        Transform Named(string name) => transforms.Single(t => t != null && t.name == name);
        var preference = (RectTransform)Named("SettingsPreferenceCard");
        preference.anchorMin = new Vector2(.04f, .50f); preference.anchorMax = new Vector2(.96f, .86f);
        var privacy = (RectTransform)Named("SettingsPrivacyCard");
        privacy.anchorMin = new Vector2(.04f, .23f); privacy.anchorMax = new Vector2(.96f, .48f);
        var local = (RectTransform)Named("SettingsLocalDataCard");
        local.anchorMin = new Vector2(.04f, .02f); local.anchorMax = new Vector2(.96f, .21f);
        void Row(string name, float min, float max)
        {
            var rect = (RectTransform)Named(name); rect.anchorMin = new Vector2(.04f, min); rect.anchorMax = new Vector2(.96f, max);
        }
        Row("SettingsDuration", .62f, .81f); Row("SettingsReminder", .42f, .61f); Row("SettingsAutoFocus", .22f, .41f);
        var oldRow = transforms.SingleOrDefault(t => t != null && t.name == "SettingsExitIntervention");
        if (oldRow != null) UnityEngine.Object.DestroyImmediate(oldRow.gameObject);
        var row = UnityEngine.Object.Instantiate(Named("SettingsAutoFocus").gameObject, preference, false);
        row.name = "SettingsExitIntervention";
        var rowRect = (RectTransform)row.transform;
        rowRect.anchorMin = new Vector2(.04f, .02f); rowRect.anchorMax = new Vector2(.96f, .21f);
        var title = row.GetComponentInChildren<Text>();
        title.name = "SettingsExitInterventionText"; title.text = "猫咪退出挽留"; title.alignment = TextAnchor.MiddleLeft;
        title.rectTransform.anchorMin = new Vector2(.04f, .40f); title.rectTransform.anchorMax = new Vector2(.68f, .98f);
        title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
        var value = UnityEngine.Object.Instantiate(title, row.transform, false);
        value.name = "SettingsExitInterventionValue"; value.text = "开启"; value.fontStyle = FontStyle.Bold; value.alignment = TextAnchor.MiddleRight;
        value.rectTransform.anchorMin = new Vector2(.70f, .40f); value.rectTransform.anchorMax = new Vector2(.96f, .98f);
        var copy = UnityEngine.Object.Instantiate(title, row.transform, false);
        copy.name = "SettingsExitInterventionCopy"; copy.text = "每次专注最多一次，继续右滑仍可结束"; copy.fontSize = 18;
        copy.color = new Color(.45f, .42f, .36f);
        copy.rectTransform.anchorMin = new Vector2(.04f, .04f); copy.rectTransform.anchorMax = new Vector2(.96f, .43f);
        var oldPrompt = transforms.SingleOrDefault(t => t != null && t.name == "ExitInterventionPrompt");
        if (oldPrompt != null) UnityEngine.Object.DestroyImmediate(oldPrompt.gameObject);
        var prompt = UnityEngine.Object.Instantiate(Named("FocusCompanionChip").gameObject, Named("FocusPanel"), false);
        prompt.name = "ExitInterventionPrompt";
        UnityEngine.Object.DestroyImmediate(prompt.transform.Find("FocusCompanionIcon").gameObject);
        var promptText = prompt.GetComponentInChildren<Text>();
        promptText.name = "ExitInterventionPromptText"; promptText.text = "再陪你专注一会儿？"; promptText.fontSize = 28;
        promptText.rectTransform.anchorMin = new Vector2(.04f, 0); promptText.rectTransform.anchorMax = new Vector2(.96f, 1);
        promptText.rectTransform.offsetMin = promptText.rectTransform.offsetMax = Vector2.zero;
        var promptRect = (RectTransform)prompt.transform;
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(.5f, .5f); promptRect.pivot = new Vector2(.5f, 0);
        promptRect.sizeDelta = new Vector2(560, 100);
        foreach (var graphic in prompt.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        prompt.SetActive(false);
        var hint = Named("SwipeLabel").GetComponent<Text>();
        hint.rectTransform.anchorMin = new Vector2(.1f, -.74f); hint.rectTransform.anchorMax = new Vector2(.9f, -.14f);
        hint.rectTransform.offsetMin = hint.rectTransform.offsetMax = Vector2.zero;
        hint.alignment = TextAnchor.MiddleCenter;
        var outline = hint.GetComponent<Outline>();
        if (outline == null) outline = hint.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.1f, .22f, .16f, .9f); outline.effectDistance = new Vector2(1.5f, -1.5f);
        Named("SwipeTrack").GetComponent<CatLifePawContactView>().ConfigurePrompt(promptRect, hint);
        var sheet = Named("SettingsSheet");
        var viewport = sheet.Find("ExitSettingsViewport") as RectTransform;
        if (viewport == null)
        {
            var go = new GameObject("ExitSettingsViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            go.transform.SetParent(sheet, false); viewport = (RectTransform)go.transform;
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = new Vector2(1, .86f);
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.clear;
        }
        var content = viewport.Find("ExitSettingsContent") as RectTransform;
        if (content == null)
        {
            content = (RectTransform)new GameObject("ExitSettingsContent", typeof(RectTransform)).transform;
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(0, 1800);
        }
        foreach (var panel in new[] { preference, privacy, local })
        {
            panel.SetParent(content, false);
            panel.anchorMin = new Vector2(panel.anchorMin.x, panel.anchorMin.y / .86f);
            panel.anchorMax = new Vector2(panel.anchorMax.x, panel.anchorMax.y / .86f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
        }
        var scroll = sheet.GetComponent<ScrollRect>();
        if (scroll == null) scroll = sheet.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.inertia = false;
        var sizing = ui.GetComponent<CatLifeMobileTouchLayout>();
        if (sizing == null) sizing = ui.AddComponent<CatLifeMobileTouchLayout>();
        var rowRects = new[] { "SettingsDuration", "SettingsReminder", "SettingsAutoFocus", "SettingsAi", "SettingsBehavior", "SettingsRecords", "SettingsClear" }
            .Select(name => (RectTransform)Named(name)).Append(rowRect).ToArray();
        sizing.Configure(scroll, rowRects, Named("SwipeTrack").GetComponent<CatLifeSwipeToEnd>(), (RectTransform)Named("SwipeTrack"),
            (RectTransform)Named("ExitConfirmCard"), new[] { (RectTransform)Named("InterruptConfirm"), (RectTransform)Named("InterruptCancel") },
            (RectTransform)Named("ExitConfirmText"));
    }
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        string controllerPath = Folder + "/ExitCandidate.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        for (int index = 0; index < Names.Length; index++)
        {
            string name = Names[index];
            string path = Folder + "/" + name + ".fbx";
            File.Copy("../../Reports/wp04-exit-actions-20261013/" + name + ".fbx", path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapeNormals = ModelImporterNormals.None;
            importer.importBlendShapes = true;
            importer.isReadable = true;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 255;
            importer.minBoneWeight = 0;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var take = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            string skinPath = AnimationUtility.CalculateTransformPath(skin.transform, model.transform);
            var clip = new AnimationClip { name = name, frameRate = 60 };
            foreach (var binding in AnimationUtility.GetCurveBindings(take))
            {
                if (binding.path.Length > 0 && model.transform.Find(binding.path) == null)
                    throw new Exception("Exit candidate binding missing: " + binding.path);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(take, binding));
            }
            float duration = index == 1 ? .8f : .4f;
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
            {
                var binding = EditorCurveBinding.FloatCurve(skinPath, typeof(SkinnedMeshRenderer), "blendShape." + skin.sharedMesh.GetBlendShapeName(i));
                if (AnimationUtility.GetEditorCurve(clip, binding) == null)
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, duration, 0));
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.startTime = 0; settings.stopTime = duration; settings.loopTime = index == 1;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string clipPath = Folder + "/" + name + ".anim";
            var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (old == null) AssetDatabase.CreateAsset(clip, clipPath);
            else { EditorUtility.CopySerialized(clip, old); UnityEngine.Object.DestroyImmediate(clip); clip = old; }
            var state = machine.AddState(name);
            state.motion = clip; state.writeDefaultValues = false;
            if (index == 0) machine.defaultState = state;
        }
        var cat = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + Names[0] + ".fbx"));
        try
        {
            cat.name = "PF_CL_CAT_ExitCandidate";
            cat.transform.localScale = Vector3.one * .045f;
            var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
            skin.quality = SkinQuality.Auto;
            skin.updateWhenOffscreen = true;
            var formal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MobileRuntime/Art/Cat/PF_CL_CAT_Runtime_v12.prefab");
            skin.sharedMaterial = formal.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
            var animator = cat.GetComponent<Animator>();
            if (animator == null) animator = cat.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PrefabUtility.SaveAsPrefabAsset(cat, Folder + "/PF_CL_CAT_ExitCandidate.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(cat); }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("EXIT_CANDIDATE_READY three clips, isolated prefab/controller");
    }

    public static void BuildRuntimeIntegration()
    {
        string output = Folder + "/RuntimeIntegration";
        Directory.CreateDirectory(output);
        AssetDatabase.Refresh();
        var formal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MobileRuntime/Art/Cat/PF_CL_CAT_Runtime_v12.prefab");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + Names[0] + ".fbx");
        var importedSkin = source.GetComponentInChildren<SkinnedMeshRenderer>();
        var cat = UnityEngine.Object.Instantiate(formal);
        try
        {
            cat.name = "PF_CL_CAT_ExitIntegration_candidate";
            var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
            int oldVertices = skin.sharedMesh.vertexCount;
            var bones = cat.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            skin.bones = importedSkin.bones.Select(b => bones[b.name]).ToArray();
            string meshPath = output + "/MSH_CL_CAT_ExitIntegration_candidate.asset";
            var mesh = UnityEngine.Object.Instantiate(importedSkin.sharedMesh);
            mesh.name = "MSH_CL_CAT_ExitIntegration_candidate";
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (savedMesh == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, savedMesh); UnityEngine.Object.DestroyImmediate(mesh); mesh = savedMesh; }
            skin.sharedMesh = mesh;
            skin.quality = SkinQuality.Auto;
            for (int i = 0; i < mesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, 0);
            string controllerPath = output + "/ExitIntegration_candidate.controller";
            if (!File.Exists(controllerPath))
                AssetDatabase.CopyAsset("Assets/MobileRuntime/Art/Cat/CL_CAT_Mobile.controller", controllerPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (string name in Names)
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
                state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + name + ".anim");
            }
            string skinPath = AnimationUtility.CalculateTransformPath(skin.transform, cat.transform);
            foreach (var entry in machine.states)
            {
                var state = entry.state;
                var clip = UnityEngine.Object.Instantiate((AnimationClip)state.motion);
                clip.name = state.motion.name;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.path.Length > 0 && cat.transform.Find(binding.path) == null)
                        throw new Exception("Integration binding missing: " + binding.path);
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    var binding = EditorCurveBinding.FloatCurve(skinPath, typeof(SkinnedMeshRenderer), "blendShape." + mesh.GetBlendShapeName(i));
                    if (AnimationUtility.GetEditorCurve(clip, binding) == null)
                        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, clip.length, 0));
                }
                string clipPath = output + "/" + clip.name + ".anim";
                var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (saved == null) { AssetDatabase.CreateAsset(clip, clipPath); saved = clip; }
                else { EditorUtility.CopySerialized(clip, saved); UnityEngine.Object.DestroyImmediate(clip); }
                state.motion = saved;
                state.writeDefaultValues = false;
            }
            cat.GetComponent<Animator>().runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(cat, output + "/PF_CL_CAT_ExitIntegration_candidate.prefab");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"EXIT_INTEGRATION_READY vertices={oldVertices}/{mesh.vertexCount} shapes={mesh.blendShapeCount} states={machine.states.Length}");
        }
        finally { UnityEngine.Object.DestroyImmediate(cat); }
    }
}
