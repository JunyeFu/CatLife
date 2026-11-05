using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CatLifeProductGapInstaller
{
    public static void InstallAndBuild()
    {
        Install();
        CatLife.Editor.CatLifeAndroidBuild.BuildApk();
    }
    public static void Install()
    {
        const string path = "Assets/Scenes/CatLifeMobile.unity";
        string backup = "../../Reports/product-gap-20261002/before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(path, Path.Combine(backup, "CatLifeMobile.unity"));
        var scene = EditorSceneManager.OpenScene(path);
        foreach (var identity in UnityEngine.Object.FindObjectsByType<CatLifeArtAssetIdentity>(FindObjectsSortMode.None))
        {
            if (identity.assetId == "CL.BLD.CatHouse.01") { identity.landmarkId = "FOCUS_HOUSE"; identity.displayNameZh = "专注小屋"; }
            if (identity.assetId == "CL.BLD.FocusHouse.01") { identity.landmarkId = "CAT_HOUSE"; identity.displayNameZh = "猫咪小屋"; }
            EditorUtility.SetDirty(identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(identity);
        }
        CaptureBanners();
        CaptureActionCards();
        CaptureIsland();
        CatLifeMobileUiPrefabBuilder.Build();
        var planner = UnityEngine.Object.FindFirstObjectByType<CatLife.Cat.CatDestinationPlanner>();
        var plannerData = new SerializedObject(planner);
        plannerData.FindProperty("visibleBodyRadius").floatValue = 1.05f;
        plannerData.ApplyModifiedPropertiesWithoutUndo();
        foreach (var identity in UnityEngine.Object.FindObjectsByType<CatLifeArtAssetIdentity>(FindObjectsSortMode.None))
        {
            CatLifeLandmarkAction action;
            switch (identity.landmarkId)
            {
                case "TOMATO_CLOCK_TOWER": action = CatLifeLandmarkAction.Records; break;
                case "CAT_HOUSE": action = CatLifeLandmarkAction.Growth; break;
                case "FISH_SHOP": action = CatLifeLandmarkAction.FishShop; break;
                case "REWARD_TREE": action = CatLifeLandmarkAction.RewardTree; break;
                case "PLAZA_RING": action = CatLifeLandmarkAction.Plaza; break;
                default:
                    if (identity.landmarkId == "FOCUS_HOUSE")
                    {
                        var obsolete = identity.GetComponent<CatLifeLandmark>();
                        if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete);
                    }
                    continue;
            }
            var landmark = identity.GetComponent<CatLifeLandmark>();
            if (landmark == null) landmark = identity.gameObject.AddComponent<CatLifeLandmark>();
            landmark.action = action;
            // Existing ground and navigation colliders remain untouched.
            if (identity.GetComponent<Collider>() == null)
            {
                var filter = identity.GetComponent<MeshFilter>();
                var collider = identity.gameObject.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
            }
            EditorUtility.SetDirty(landmark);
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CATLIFE_PRODUCT_GAPS_INSTALLED backup=" + backup);
    }

    private static void CaptureBanners()
    {
        var identities = UnityEngine.Object.FindObjectsByType<CatLifeArtAssetIdentity>(FindObjectsSortMode.None);
        var cameraObject = new GameObject("BannerCaptureCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.fieldOfView = 48;
        camera.aspect = 1080f / 460;
        var target = new RenderTexture(1080, 460, 24);
        camera.targetTexture = target;
        string[] ids = { "TOMATO_CLOCK_TOWER", "CAT_HOUSE", "FOCUS_HOUSE" };
        string[] pages = { "Records", "Growth", "Settings" };
        for (int i = 0; i < ids.Length; i++)
        {
            Bounds bounds = default;
            bool found = false;
            foreach (var identity in identities)
                if (identity.landmarkId == ids[i])
                {
                    foreach (var renderer in identity.GetComponentsInChildren<Renderer>())
                    { if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds); }
                }
            if (!found) throw new InvalidOperationException("Missing banner landmark " + ids[i]);
            float distance = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) / Mathf.Tan(24 * Mathf.Deg2Rad) * 1.22f + bounds.extents.z;
            camera.transform.position = bounds.center + new Vector3(distance * (i == 0 ? -.55f : .12f), bounds.extents.y * .35f, distance);
            camera.transform.LookAt(bounds.center);
            camera.ResetWorldToCameraMatrix();
            camera.ResetProjectionMatrix();
            Debug.Log("BANNER " + ids[i] + " bounds=" + bounds + " camera=" + camera.transform.position);
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1080, 460, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1080, 460), 0, 0); image.Apply();
            string output = "Assets/MobileRuntime/UI/Art/Banners/T_CL_UI_" + pages[i] + "Runtime_1080x460.png";
            File.WriteAllBytes(output, image.EncodeToPNG());
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(output);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        camera.targetTexture = null;
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }

    private static void CaptureActionCards()
    {
        // Capture a temporary clone, never save a sampled pose into the formal cat.
        var clone = UnityEngine.Object.Instantiate(GameObject.Find("CatLifeMobileCat"));
        var animator = clone.GetComponentInChildren<Animator>();
        animator.enabled = false;
        foreach (var transform in clone.GetComponentsInChildren<Transform>()) transform.gameObject.layer = 31;
        var cameraObject = new GameObject("ActionCardCapture");
        var camera = cameraObject.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear; camera.fieldOfView = 35; camera.aspect = 1;
        var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        var lightObject = new GameObject("ActionCardLight");
        var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
        light.intensity = 1.4f; light.cullingMask = 1 << 31;
        light.transform.rotation = Quaternion.Euler(35, -25, 0);
        string[] clips = { "CL_CAT_CrouchPawWave_v01_loop_96f", "CL_CAT_SitTailGreeting_v01_loop_96f", "CL_CAT_ProneBreath_v11_loop_240f", "CL_CAT_StandStretchYawn_v01_144f" };
        string[] names = { "Wave", "Tail", "Loaf", "Stretch" };
        float[] times = { .55f, .6f, 1f, 1f };
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MobileRuntime/Art/Cat/AnimationsV12/" + clips[i] + ".anim");
            clip.SampleAnimation(animator.gameObject, times[i]);
            var renderer = clone.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh(); renderer.BakeMesh(mesh);
            // BakeMesh already includes this imported rig's scale; do not apply it twice.
            var bounds = new Bounds(renderer.transform.position + renderer.transform.rotation * mesh.vertices[0], Vector3.zero);
            foreach (var point in mesh.vertices) bounds.Encapsulate(renderer.transform.position + renderer.transform.rotation * point);
            renderer.enabled = false;
            var posed = new GameObject("SampledAction", typeof(MeshFilter), typeof(MeshRenderer)); posed.layer = 31;
            posed.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
            posed.GetComponent<MeshFilter>().sharedMesh = mesh;
            posed.GetComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
            Vector3 direction = new Vector3(-1f, .32f, 1f).normalized;
            float distance = bounds.extents.magnitude / Mathf.Tan(17.5f * Mathf.Deg2Rad) * 1.05f;
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);
            camera.ResetWorldToCameraMatrix(); camera.ResetProjectionMatrix(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
            string output = "Assets/MobileRuntime/UI/Art/Actions/T_CL_CatAction_" + names[i] + "_512.png";
            File.WriteAllBytes(output, image.EncodeToPNG());
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
            UnityEngine.Object.DestroyImmediate(posed); UnityEngine.Object.DestroyImmediate(mesh);
        }
        camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(lightObject); UnityEngine.Object.DestroyImmediate(clone);
    }

    private static void CaptureIsland()
    {
        var cameraObject = new GameObject("IslandAuditCamera");
        var camera = cameraObject.AddComponent<Camera>(); camera.CopyFrom(Camera.main);
        camera.rect = new Rect(0, 0, 1, 1); camera.fieldOfView = 50; camera.aspect = 1.5f;
        camera.transform.position = new Vector3(30, 30, 42); camera.transform.LookAt(new Vector3(0, -1, -2));
        camera.ResetProjectionMatrix(); camera.ResetWorldToCameraMatrix();
        var target = new RenderTexture(1200, 800, 24); camera.targetTexture = target; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(1200, 800, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply();
        Directory.CreateDirectory("../../Reports/product-gap-20261002/editor");
        File.WriteAllBytes("../../Reports/product-gap-20261002/editor/island.png", image.EncodeToPNG());
        camera.transform.position = new Vector3(28, 5, 42); camera.transform.LookAt(new Vector3(0, -1, -2));
        camera.ResetWorldToCameraMatrix(); camera.Render();
        image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply();
        File.WriteAllBytes("../../Reports/product-gap-20261002/editor/island-side.png", image.EncodeToPNG());
        Debug.Log("ISLAND_BASE_BOUNDS " + GameObject.Find("CL_ENV_IslandBase_01").GetComponent<Renderer>().bounds);
        RenderTexture.active = previous; camera.targetTexture = null; target.Release();
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
    }
}
