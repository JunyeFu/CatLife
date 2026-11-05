using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CatLifeProneImportReview
{
    const string PathName = "Assets/Editor/CatProneReview/CL_CAT_ProneReview_v11.fbx";
    public static void Run()
    {
        Configure(PathName);
    }
    public static void Configure(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.importBlendShapes = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importBlendShapeNormals = ModelImporterNormals.None;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        string take = importer.defaultClipAnimations[0].takeName;
        importer.clipAnimations = new[]
        {
            Clip(take, "CL_CAT_ProneReview_v11", 0, 360, false),
            Clip(take, "CL_CAT_LieToStand_v11_120f", 30, 150, false),
            Clip(take, "CL_CAT_StandToLie_v11_150f", 180, 330, false),
            Clip(take, "CL_CAT_ProneHold_v11_loop_30f", 0, 30, true),
            Clip(take, "CL_CAT_StandHold_v11_loop_30f", 150, 180, true),
            Clip(take, "CL_CAT_ProneBreath_v11_loop_240f", 360, 600, true),
            Clip(take, "CL_CAT_ProneAttention_v11_48f", 600, 648, false)
        };
        importer.SaveAndReimport();
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var lines = new System.Collections.Generic.List<string>();
        lines.Add($"Importer global={importer.globalScale} fileScale={importer.fileScale} units={importer.useFileScale}");
        lines.Add($"Mesh bounds={renderer.sharedMesh.bounds} renderer scale={renderer.transform.lossyScale}");
        lines.AddRange(model.GetComponentsInChildren<Transform>().Take(8).Select(t => $"Transform {t.name} scale={t.localScale} position={t.localPosition}"));
        lines.Add("Imported mesh blendshapes: " + renderer.sharedMesh.blendShapeCount);
        for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
            lines.Add(renderer.sharedMesh.GetBlendShapeName(i));
        bool synchronized = false;
        foreach (var clip in clips)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            int shapes = bindings.Count(b => b.propertyName.StartsWith("blendShape."));
            int bones = bindings.Count(b => b.type == typeof(Transform));
            lines.Add($"{clip.name}: seconds={clip.length} fps={clip.frameRate} bones={bones} shapes={shapes}");
            lines.AddRange(bindings.Where(b => b.propertyName.StartsWith("blendShape."))
                .Select(b => b.path + ":" + b.propertyName));
            synchronized |= bones > 0 && shapes >= 5;
        }
        Directory.CreateDirectory("Reports/ProneImportReview");
        File.WriteAllLines("Reports/ProneImportReview/import.txt", lines);
        if (!synchronized) throw new Exception("Prone review missing synchronized bone/shape animation. See import.txt");
        Debug.Log("PRONE_IMPORT_REVIEW_PASS");
    }

    static ModelImporterClipAnimation Clip(string take, string name, float first, float last, bool loop)
    {
        return new ModelImporterClipAnimation {takeName=take, name=name,
            firstFrame=first, lastFrame=last, loopTime=loop};
    }

    public static void Render()
    {
        Run();
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        var cat = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PathName));
        var clip = AssetDatabase.LoadAllAssetsAtPath(PathName).OfType<AnimationClip>()
            .First(c => c.name == "CL_CAT_ProneReview_v11");
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        skin.updateWhenOffscreen = true;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(.65f, .65f, .65f));
        material.SetFloat("_Smoothness", 0f);
        material.SetFloat("_SpecularHighlights", 0f);
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        skin.sharedMaterial = material;
        var light = new GameObject("ReviewLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        RenderSettings.ambientLight = Color.gray;
        var camera = new GameObject("ReviewCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .19f, .2f);
        camera.orthographic = true;
        clip.SampleAnimation(cat, 2.5f);
        var bounds = skin.bounds;
        float size = bounds.size.magnitude;
        camera.orthographicSize = size * .55f;
        camera.farClipPlane = size * 20;
        camera.nearClipPlane = size * .001f;
        camera.transform.position = bounds.center + new Vector3(1,.35f,1).normalized * size * 3;
        camera.transform.LookAt(bounds.center);
        var rt = new RenderTexture(1000, 800, 24);
        camera.targetTexture = rt;
        var bakedObject = new GameObject("SampledMesh");
        // BakeMesh output already includes the imported armature scale.
        // Keep review geometry outside that hierarchy to avoid applying it twice.
        var filter = bakedObject.AddComponent<MeshFilter>();
        var sampledRenderer = bakedObject.AddComponent<MeshRenderer>();
        sampledRenderer.sharedMaterial = material;
        skin.enabled = false;
        Directory.CreateDirectory("Reports/ProneImportReview");
        foreach (int frame in new[] {1, 91, 151})
        {
            clip.SampleAnimation(cat, (frame-1)/60f);
            var baked = new Mesh();
            skin.BakeMesh(baked, false);
            Debug.Log($"PRONE_LOCAL frame={frame} mesh={baked.bounds} skinScale={skin.transform.lossyScale}");
            filter.sharedMesh = baked;
            var actual = sampledRenderer.bounds;
            float radius = actual.size.magnitude;
            camera.orthographicSize = radius * .55f;
            camera.farClipPlane = radius * 20;
            camera.nearClipPlane = radius * .001f;
            camera.transform.position = actual.center + new Vector3(1,.35f,1).normalized * radius * 3;
            camera.transform.LookAt(actual.center);
            Debug.Log($"PRONE_SAMPLE frame={frame} bounds={actual} vertices={baked.vertexCount}");
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(1000, 800, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0,0,1000,800),0,0);
            texture.Apply();
            File.WriteAllBytes($"Reports/ProneImportReview/unity_F{frame:000}.png",texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(baked);
        }
        RenderTexture.active = null;
        camera.targetTexture = null;
        rt.Release();
        Debug.Log("PRONE_UNITY_RENDER_DONE");
    }
}
