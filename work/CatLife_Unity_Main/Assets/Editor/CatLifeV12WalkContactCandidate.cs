using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CatLifeV12WalkContactCandidate
{
    const string Source = "Assets/Editor/CatProneReview/CL_CAT_WalkContact_runtime_v12.fbx";
    const string Candidate = "Assets/Editor/CatProneReview/CL_CAT_WalkContact_candidate_v12.anim";
    const string Prefab = "Assets/MobileRuntime/Art/Cat/PF_CL_CAT_Runtime_v12.prefab";
    const string Report = "../../Reports/cat-animation-v12/walk-unity-candidate/";

    public static void BuildAndInspect()
    {
        Directory.CreateDirectory(Report);
        var importer = (ModelImporter)AssetImporter.GetAtPath(Source);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importBlendShapeNormals = ModelImporterNormals.None;
        importer.skinWeights = ModelImporterSkinWeights.Custom;
        importer.maxBonesPerVertex = 32;
        importer.minBoneWeight = 0;
        importer.SaveAndReimport();
        var source = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>()
            .Single(c => !c.name.StartsWith("__preview__"));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var cat = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
        Object.DestroyImmediate(cat.GetComponent<Animator>());
        cat.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        cat.transform.localScale = Vector3.one * .045f;
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = skin.sharedMesh;
        var clip = new AnimationClip { name = "CL_CAT_WalkContact_candidate_v12", frameRate = 60 };
        var bindings = AnimationUtility.GetCurveBindings(source);
        foreach (var binding in bindings)
        {
            if (binding.path != "" && cat.transform.Find(binding.path) == null)
                throw new Exception("Walk binding absent from formal cat: " + binding.path);
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            var keys = Enumerable.Range(0, 61).Select(f => new Keyframe(f / 60f, curve.Evaluate(f / 60f))).ToArray();
            var copied = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(copied, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(copied, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, binding, copied);
        }
        string skinPath = AnimationUtility.CalculateTransformPath(skin.transform, cat.transform);
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            var binding = EditorCurveBinding.FloatCurve(skinPath, typeof(SkinnedMeshRenderer), "blendShape." + mesh.GetBlendShapeName(i));
            if (AnimationUtility.GetEditorCurve(clip, binding) == null)
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, 1, 0));
        }
        clip.EnsureQuaternionContinuity();
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0;
        settings.stopTime = 1;
        settings.loopTime = true;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Candidate);
        if (existing == null) AssetDatabase.CreateAsset(clip, Candidate);
        else { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); clip = existing; }
        AssetDatabase.SaveAssets();

        string[] feet = { "frontleg2", "R_frontleg2", "backleg2", "R_backleg2" };
        int[] footBones = feet.Select(name => Array.FindIndex(skin.bones, b => b.name == name)).ToArray();
        var sets = feet.Select(_ => new List<int>()).ToArray();
        using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
        {
            int offset = 0;
            for (int v = 0; v < counts.Length; v++)
            {
                for (int f = 0; f < feet.Length; f++)
                {
                    float sum = 0;
                    for (int j = 0; j < counts[v]; j++)
                        if (weights[offset + j].boneIndex == footBones[f]) sum += weights[offset + j].weight;
                    if (sum >= .99f) sets[f].Add(v);
                }
                offset += counts[v];
            }
        }
        var baked = new Mesh();
        var rows = new StringBuilder("frame,foot,sole_y,center_x,center_z,delta_z,full_vertex_min_y\n");
        var centers = new Vector3[61, 4];
        float minMesh = float.PositiveInfinity, maxSliceError = 0;
        var prior = new Vector3[4];
        var originalQuality = QualitySettings.skinWeights;
        QualitySettings.skinWeights = SkinWeights.Unlimited;
        try
        {
            for (int frame = 0; frame <= 60; frame++)
            {
                float time = frame / 60f;
                source.SampleAnimation(cat, time);
                skin.BakeMesh(baked, false);
                var sourceVertices = baked.vertices;
                clip.SampleAnimation(cat, time);
                skin.BakeMesh(baked, false);
                var vertices = baked.vertices;
                for (int v = 0; v < vertices.Length; v++)
                    maxSliceError = Mathf.Max(maxSliceError, Vector3.Distance(vertices[v], sourceVertices[v]));
                float fullMin = vertices.Min(v => v.y);
                minMesh = Mathf.Min(minMesh, fullMin);
                for (int f = 0; f < feet.Length; f++)
                {
                    Vector3 center = Vector3.zero;
                    float sole = float.PositiveInfinity;
                    foreach (int v in sets[f])
                    {
                        Vector3 p = skin.transform.position + skin.transform.rotation * vertices[v];
                        center += p;
                        sole = Mathf.Min(sole, p.y);
                    }
                    center /= sets[f].Count;
                    centers[frame, f] = center;
                    rows.AppendLine(FormattableString.Invariant($"{frame},{feet[f]},{sole:R},{center.x:R},{center.z:R},{(frame == 0 ? 0 : (center.z - prior[f].z) * 60):R},{fullMin:R}"));
                    prior[f] = center;
                }
            }
            float[] touchdown = { .25f, .75f, 0, .5f };
            float[] duty = { .65f, .65f, .60f, .60f };
            float[] total = new float[2], worst = new float[2];
            int samples = 0;
            for (int frame = 1; frame <= 60; frame++)
            for (int f = 0; f < 4; f++)
            {
                float phase = Mathf.Repeat((frame - .5f) / 60f - touchdown[f], 1);
                if (phase >= duty[f]) continue;
                samples++;
                for (int sign = 0; sign < 2; sign++)
                {
                    float bodySpeed = sign == 0 ? .405f : -.405f;
                    float slip = Mathf.Abs((centers[frame, f].z - centers[frame - 1, f].z) * 60 + bodySpeed);
                    total[sign] += slip;
                    worst[sign] = Mathf.Max(worst[sign], slip);
                }
            }
            File.WriteAllText(Report + "stride.csv", rows.ToString());
            File.WriteAllLines(Report + "result.txt", new[]
            {
                FormattableString.Invariant($"formalMeshVertices={mesh.vertexCount}; shapes={mesh.blendShapeCount}; sourceBindings={bindings.Length}; clipSeconds={clip.length:R}"),
                FormattableString.Invariant($"formalMeshSliceError={maxSliceError:R}; fullMeshMinY={minMesh:R}"),
                FormattableString.Invariant($"supportSamples={samples}; plusZMeanSlipSpeed={total[0] / samples:R}; plusZWorstSlipSpeed={worst[0]:R}"),
                FormattableString.Invariant($"minusZMeanSlipSpeed={total[1] / samples:R}; minusZWorstSlipSpeed={worst[1]:R}"),
                "Editor fixed-root source and extracted clip on formal mesh; navigation and material require separate review."
            });
            RenderMaterialFrames(cat, clip);
            Debug.Log("V12_WALK_CONTACT_CANDIDATE_FINISHED " + maxSliceError + " min=" + minMesh);
        }
        finally { QualitySettings.skinWeights = originalQuality; Object.DestroyImmediate(baked); Object.DestroyImmediate(cat); }
    }

    static void RenderMaterialFrames(GameObject cat, AnimationClip clip)
    {
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh();
        var surface = new GameObject("Walk evaluated surface");
        var filter = surface.AddComponent<MeshFilter>();
        surface.AddComponent<MeshRenderer>().sharedMaterial = skin.sharedMaterial;
        skin.enabled = false;
        clip.SampleAnimation(cat, 0);
        skin.BakeMesh(baked, false);
        filter.sharedMesh = baked;
        var bounds = baked.bounds;
        File.AppendAllText(Report + "result.txt", $"rendererBounds={bounds}; material={skin.sharedMaterial}; rendererEnabled={skin.enabled}; lossyScale={skin.transform.lossyScale}\n");
        var light = new GameObject("Walk candidate light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2;
        light.transform.rotation = Quaternion.Euler(40, -30, 0);
        RenderSettings.ambientLight = Color.gray;
        var camera = new GameObject("Walk candidate camera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.magnitude * 1.25f, .82f);
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 20;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .19f, .2f);
        camera.transform.position = bounds.center + new Vector3(1.9f, .7f, 1.9f);
        camera.transform.LookAt(bounds.center);
        var target = new RenderTexture(800, 700, 24);
        camera.targetTexture = target;
        foreach (int frame in new[] { 0, 15, 30, 45, 60 })
        {
            clip.SampleAnimation(cat, frame / 60f);
            skin.BakeMesh(baked, false);
            filter.sharedMesh = baked;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(800, 700, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 800, 700), 0, 0);
            image.Apply();
            File.WriteAllBytes(Report + $"material-F{frame:D3}.png", image.EncodeToPNG());
            Object.DestroyImmediate(image);
            RenderTexture.active = previous;
        }
        camera.targetTexture = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(light.gameObject);
        Object.DestroyImmediate(surface);
        Object.DestroyImmediate(baked);
    }
}
