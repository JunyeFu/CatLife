using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CatLifeMobilePreviewRenderer
{
    public static void RenderHomeAlignmentBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        Camera camera = Camera.main;
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports", "MobileRebuild", "home-alignment"));
        RenderCandidate(camera, new Vector3(0f, 2.5f, 10f), new Vector3(0f, .15f, -6f), 76f, Path.Combine(directory, "home-a.png"));
        RenderCandidate(camera, new Vector3(0f, 2.2f, 9f), new Vector3(0f, .05f, -6f), 74f, Path.Combine(directory, "home-b.png"));
        RenderCandidate(camera, new Vector3(0f, 2.5f, 9.5f), new Vector3(0f, -.35f, -6f), 78f, Path.Combine(directory, "home-c.png"));
        Debug.Log("CATLIFE_HOME_ALIGNMENT=" + directory);
        EditorApplication.Exit(0);
    }

    public static void RenderCameraCandidatesBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;

        Camera camera = Camera.main;
        Transform cat = GameObject.Find("CatLifeMobileCat")?.transform;
        if (cat != null) cat.SetPositionAndRotation(new Vector3(-.3f, .02f, -6.78f), Quaternion.identity);

        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports", "MobileRebuild", "camera-candidates"));
        RenderCandidate(camera, new Vector3(.1f, 1.9f, 1.2f), new Vector3(0f, 1.5f, -8.5f), 80f, Path.Combine(directory, "candidate-00-current.png"));
        RenderCandidate(camera, new Vector3(0f, 6f, 10f), new Vector3(0f, 1.2f, -7.5f), 72f, Path.Combine(directory, "candidate-01-wide-near.png"));
        RenderCandidate(camera, new Vector3(0f, 7.5f, 16f), new Vector3(0f, 1.0f, -7.0f), 68f, Path.Combine(directory, "candidate-02-wide-mid.png"));
        RenderCandidate(camera, new Vector3(0f, 9f, 22f), new Vector3(0f, .8f, -6.0f), 64f, Path.Combine(directory, "candidate-03-wide-far.png"));
        RenderCandidate(camera, new Vector3(0f, 13f, 20f), new Vector3(0f, .2f, -4.0f), 66f, Path.Combine(directory, "candidate-04-high-wide.png"));
        RenderCandidate(camera, new Vector3(0f, 16f, 27f), new Vector3(0f, 0f, -4f), 85f, Path.Combine(directory, "candidate-05-gate-overlook.png"));
        RenderCandidate(camera, new Vector3(0f, 19f, 30f), new Vector3(0f, 0f, -3f), 90f, Path.Combine(directory, "candidate-06-island-overlook.png"));
        RenderCandidate(camera, new Vector3(-2f, 17f, 25f), new Vector3(0f, 0f, -4f), 92f, Path.Combine(directory, "candidate-07-offset-overlook.png"));
        RenderCandidate(camera, new Vector3(0f, 22f, 32f), new Vector3(0f, 0f, -3f), 80f, Path.Combine(directory, "candidate-08-high-overlook.png"));
        if (cat != null) cat.SetPositionAndRotation(new Vector3(-.3f, .02f, 4.5f), Quaternion.Euler(0f, 180f, 0f));
        RenderCandidate(camera, new Vector3(0f, 16f, 27f), new Vector3(0f, 0f, -4f), 85f, Path.Combine(directory, "candidate-09-foreground-cat.png"));
        if (cat != null) cat.SetPositionAndRotation(new Vector3(-.3f, .02f, 7.5f), Quaternion.Euler(0f, 180f, 0f));
        RenderCandidate(camera, new Vector3(0f, 16f, 27f), new Vector3(0f, 0f, -4f), 85f, Path.Combine(directory, "candidate-10-near-cat.png"));
        if (cat != null) cat.SetPositionAndRotation(new Vector3(-.3f, .02f, 4.5f), Quaternion.Euler(0f, 180f, 0f));
        RenderCandidate(camera, new Vector3(0f, 13f, 23f), new Vector3(0f, 0f, -4f), 96f, Path.Combine(directory, "candidate-11-low-wide-cat.png"));
        RenderCandidate(camera, new Vector3(0f, 6f, 10f), new Vector3(0f, 1.2f, -7.5f), 72f, Path.Combine(directory, "candidate-12-near-feature-cat.png"));
        RenderCandidate(camera, new Vector3(0f, 8f, 15f), new Vector3(0f, .8f, -5f), 82f, Path.Combine(directory, "candidate-13-mid-feature-cat.png"));
        if (cat != null) cat.SetPositionAndRotation(new Vector3(-.3f, .02f, 4.5f), Quaternion.identity);
        RenderCandidate(camera, new Vector3(0f, 6f, 10f), new Vector3(0f, 1.0f, -6f), 100f, Path.Combine(directory, "candidate-14-wide-feature-cat.png"));
        RenderCandidate(camera, new Vector3(0f, 7f, 12f), new Vector3(0f, .8f, -5f), 105f, Path.Combine(directory, "candidate-15-ultrawide-feature-cat.png"));
        RenderCandidate(camera, new Vector3(0f, 8f, 15f), new Vector3(0f, .8f, -5f), 96f, Path.Combine(directory, "candidate-16-midwide-feature-cat.png"));
        if (cat != null)
        {
            cat.SetPositionAndRotation(new Vector3(-.3f, .02f, 4.5f), Quaternion.identity);
            cat.localScale *= 2f;
        }
        RenderCandidate(camera, new Vector3(0f, 6f, 10f), new Vector3(0f, 1.0f, -6f), 100f, Path.Combine(directory, "candidate-17-wide-large-cat.png"));
        RenderCandidate(camera, new Vector3(0f, 7f, 12f), new Vector3(0f, .8f, -5f), 105f, Path.Combine(directory, "candidate-18-ultrawide-large-cat.png"));
        if (cat != null) cat.SetPositionAndRotation(new Vector3(-.3f, .02f, 7f), Quaternion.identity);
        RenderCandidate(camera, new Vector3(0f, 6f, 10f), new Vector3(0f, 1.0f, -6f), 100f, Path.Combine(directory, "candidate-19-near-large-cat.png"));
        if (cat != null) cat.SetPositionAndRotation(new Vector3(-.3f, .02f, 8f), Quaternion.identity);
        RenderCandidate(camera, new Vector3(0f, 6f, 10f), new Vector3(0f, 1.0f, -6f), 100f, Path.Combine(directory, "candidate-20-hero-large-cat.png"));
        if (cat != null) cat.SetPositionAndRotation(new Vector3(-.3f, .02f, 7f), Quaternion.identity);
        RenderCandidate(camera, new Vector3(0f, 6f, 10f), new Vector3(0f, .8f, 0f), 92f, Path.Combine(directory, "candidate-21-hero-composition.png"));
        RenderCandidate(camera, new Vector3(0f, 7f, 12f), new Vector3(0f, .8f, -1f), 96f, Path.Combine(directory, "candidate-22-hero-town.png"));
        RenderCandidate(camera, new Vector3(0f, 6f, 10f), new Vector3(0f, .6f, 3.5f), 92f, Path.Combine(directory, "candidate-23-hero-centered.png"));
        RenderCandidate(camera, new Vector3(0f, 2.5f, 10f), new Vector3(0f, .5f, -5f), 100f, Path.Combine(directory, "candidate-24-low-hero.png"));
        RenderCandidate(camera, new Vector3(0f, 3.5f, 10f), new Vector3(0f, .5f, -4f), 96f, Path.Combine(directory, "candidate-25-eyelevel-hero.png"));
        if (cat != null) cat.SetPositionAndRotation(new Vector3(-.3f, .02f, 7f), Quaternion.Euler(0f, 22f, 0f));
        RenderCandidate(camera, new Vector3(1.4f, 1.6f, 9.5f), new Vector3(-.3f, .4f, 7f), 68f, Path.Combine(directory, "candidate-26-focus-three-quarter.png"));
        RenderCandidate(camera, new Vector3(0f, 2.2f, 9.5f), new Vector3(-.3f, .55f, 7f), 74f, Path.Combine(directory, "candidate-27-reward-hero.png"));
        if (cat != null)
        {
            cat.localScale = Vector3.one * .045f;
            cat.SetPositionAndRotation(new Vector3(0f, .02f, 4.5f), Quaternion.Euler(0f, 22f, 0f));
        }
        RenderCandidate(camera, new Vector3(0f, 2.5f, 10f), new Vector3(0f, .5f, -5f), 85f, Path.Combine(directory, "candidate-28-safe-hero-85.png"));
        RenderCandidate(camera, new Vector3(0f, 2.5f, 10f), new Vector3(0f, .5f, -5f), 90f, Path.Combine(directory, "candidate-29-safe-hero-90.png"));
        Debug.Log("CATLIFE_CAMERA_CANDIDATES=" + directory);
        EditorApplication.Exit(0);
    }

    private static void RenderCandidate(Camera camera, Vector3 position, Vector3 target, float fov, string path)
    {
        camera.transform.position = position;
        camera.transform.LookAt(target);
        camera.fieldOfView = fov;
        RenderCamera(camera, 540, 1200, path);
        Debug.Log($"CATLIFE_CAMERA_CANDIDATE path={path} position={position} target={target} euler={camera.transform.eulerAngles} fov={fov}");
    }

    public static void RenderFullIslandAuditBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases) canvas.enabled = false;

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
            .ToArray();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);

        GameObject cameraObject = new GameObject("CatLifeFullIslandAuditCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.aspect = 1f;
        camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.12f;
        camera.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 40f);
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.35f, 0.72f, 0.9f);

        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports", "MobileRebuild", "simulator-v03", "EditorFullIslandAudit.png"));
        RenderCamera(camera, 1600, 1600, path);
        Object.DestroyImmediate(cameraObject);
        Debug.Log("CATLIFE_FULL_ISLAND_AUDIT=" + path + " bounds=" + bounds);
        EditorApplication.Exit(0);
    }

    public static void RenderExitCandidateIslandAuditBatch()
    {
        const string scenePath = "Assets/Editor/CatProneReview/ExitCandidate/CatLifeExitCandidate.unity";
        string validation = CatLife.Editor.CatLifeMobileBuildValidator.ValidateSceneReport(scenePath);
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        Transform town = GameObject.Find("CatLifeMobileTown").transform;
        MeshRenderer[] renderers = town.GetComponentsInChildren<MeshRenderer>()
            .Where(renderer => renderer.enabled).ToArray();
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Reports", "wp06-island-audit-r22-20261017"));
        Directory.CreateDirectory(directory);
        StringBuilder report = new StringBuilder();
        report.AppendLine("Scene=" + scenePath);
        report.AppendLine(validation);
        report.AppendLine("Editor-only audit cameras; no scene save, model relocation, material replacement or runtime UI.");

        var cameraObject = new GameObject("CatLifeR22IslandAuditCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.35f, .72f, .9f);
        Bounds townBounds = AuditBounds(renderers);
        RenderAuditView(camera, townBounds, Quaternion.Euler(90, 0, 0), 1600, 1600, directory, "island-top", report);
        RenderAuditView(camera, townBounds, Quaternion.Euler(35, 180, 0), 1600, 1200, directory, "island-front", report);
        RenderAuditView(camera, townBounds, Quaternion.Euler(30, 35, 0), 1600, 1200, directory, "island-rear", report);
        RenderAuditView(camera, townBounds, Quaternion.Euler(-65, 180, 0), 1600, 1200, directory, "island-bottom", report);

        string[][] groups =
        {
            new[] { "CL_BLD_FishShop_01", "CL_BLD_TomatoClockTower_01" },
            new[] { "CL_BLD_CatHouse_01", "CL_ROAD_PlazaRing_01", "CL_PROP_CenterStone_01" },
            new[] { "CL_BLD_FocusHouse_01", "CL_ENV_RewardTree_01" }
        };
        string[] labels = { "landmarks-west", "landmarks-center", "landmarks-east" };
        for (int i = 0; i < groups.Length; i++)
        {
            MeshRenderer[] selected = groups[i].Select(name => renderers.Single(renderer => renderer.name == name)).ToArray();
            RenderAuditView(camera, AuditBounds(selected), Quaternion.Euler(24, 180, 0), 1600, 1200, directory, labels[i], report);
            foreach (MeshRenderer renderer in selected)
            {
                var identity = renderer.GetComponent<CatLifeArtAssetIdentity>();
                report.AppendLine($"Landmark {identity.assetId} {identity.landmarkId} {renderer.name} bounds={renderer.bounds}");
            }
        }

        MeshRenderer island = renderers.Single(renderer => renderer.name == "CL_ENV_IslandBase_01");
        Mesh islandMesh = island.GetComponent<MeshFilter>().sharedMesh;
        report.AppendLine("Island mesh=" + AssetDatabase.GetAssetPath(islandMesh) + " bounds=" + island.bounds);
        for (int i = 0; i < islandMesh.subMeshCount; i++)
            report.AppendLine($"Island submesh {i}: material={island.sharedMaterials[i].name} indices={islandMesh.GetIndexCount(i)}");
        foreach (Material material in renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().OrderBy(material => material.name))
        {
            Texture texture = material.mainTexture;
            string textureInfo = texture == null ? "solid_color" : AssetDatabase.GetAssetPath(texture) + $" {texture.width}x{texture.height}";
            report.AppendLine($"Material {material.name} shader={material.shader.name} color={material.color} texture={textureInfo}");
        }
        Object.DestroyImmediate(cameraObject);
        File.WriteAllText(Path.Combine(directory, "audit.txt"), report.ToString());
        Debug.Log("CATLIFE_R22_ISLAND_AUDIT=" + directory + " | " + validation);
        EditorApplication.Exit(validation.StartsWith("PASS", System.StringComparison.Ordinal) ? 0 : 1);
    }

    private static Bounds AuditBounds(MeshRenderer[] renderers)
    {
        Bounds bounds = renderers[0].bounds;
        foreach (MeshRenderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static void RenderAuditView(Camera camera, Bounds bounds, Quaternion rotation, int width, int height,
        string directory, string label, StringBuilder report)
    {
        camera.orthographic = true;
        Vector3 extent = bounds.extents;
        Quaternion inverse = Quaternion.Inverse(rotation);
        float halfWidth = 0, halfHeight = 0;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 local = inverse * new Vector3(x * extent.x, y * extent.y, z * extent.z);
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
        }
        float distance = extent.magnitude * 3 + 10;
        camera.aspect = (float)width / height;
        camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / camera.aspect) * 1.12f;
        camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
        camera.nearClipPlane = .1f;
        camera.farClipPlane = distance + extent.magnitude * 2;
        RenderCamera(camera, width, height, Path.Combine(directory, label + ".png"));
        report.AppendLine($"View {label}: position={camera.transform.position} euler={camera.transform.eulerAngles} size={camera.orthographicSize} bounds={bounds}");
    }

    public static void RenderAtlas512ComparisonBatch()
    {
        const string scenePath = "Assets/Editor/CatProneReview/ExitCandidate/CatLifeExitCandidate.unity";
        const string candidateFolder = "Assets/Editor/CatProneReview/Atlas512Candidate";
        EditorSceneManager.OpenScene(scenePath);
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        MeshRenderer[] renderers = GameObject.Find("CatLifeMobileTown").GetComponentsInChildren<MeshRenderer>();
        Material[] atlases = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct()
            .Where(material => material.name.StartsWith("MAT_CL_TownAtlas_", System.StringComparison.Ordinal)).OrderBy(material => material.name).ToArray();
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Reports", "wp06-atlas512-comparison-20261017"));
        Directory.CreateDirectory(directory);
        StringBuilder report = new StringBuilder("Isolated Editor comparison; original textures/materials/scenes and APK are unchanged.\n");
        var cameraObject = new GameObject("CatLifeAtlasComparisonCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.35f, .72f, .9f);
        RenderAtlasComparisonViews(camera, renderers, directory, "1024", report);

        AssetDatabase.CreateFolder("Assets/Editor/CatProneReview", "Atlas512Candidate");
        Material[] replacements = new Material[atlases.Length];
        for (int i = 0; i < atlases.Length; i++)
        {
            string sourcePath = AssetDatabase.GetAssetPath(atlases[i].mainTexture);
            string path = candidateFolder + "/" + Path.GetFileName(sourcePath).Replace("_1024.png", "_512_candidate.png");
            if (!AssetDatabase.CopyAsset(sourcePath, path)) throw new System.IO.IOException("Cannot copy atlas candidate: " + path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = 512;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = 512;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            replacements[i] = new Material(atlases[i]) { name = atlases[i].name + "_512_candidate", mainTexture = texture };
            report.AppendLine($"Atlas {atlases[i].name}: {atlases[i].mainTexture.width}x{atlases[i].mainTexture.height} -> {texture.width}x{texture.height}; Android={android.format}; candidate={path}");
        }
        int changed = 0;
        foreach (MeshRenderer renderer in renderers)
        {
            Material[] slots = renderer.sharedMaterials;
            bool replaced = false;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                int index = System.Array.IndexOf(atlases, slots[slot]);
                if (index < 0) continue;
                slots[slot] = replacements[index]; replaced = true;
            }
            if (replaced) { renderer.sharedMaterials = slots; changed++; }
        }
        report.AppendLine("Remapped renderers=" + changed + "; dedicated landmark textures unchanged.");
        RenderAtlasComparisonViews(camera, renderers, directory, "512", report);
        File.WriteAllText(Path.Combine(directory, "comparison.txt"), report.ToString());
        foreach (Material material in replacements) Object.DestroyImmediate(material);
        Object.DestroyImmediate(cameraObject);
        Debug.Log("CATLIFE_ATLAS512_COMPARISON=" + directory + " remapped=" + changed);
        EditorApplication.Exit(0);
    }

    public static void RenderRepackedAtlas512Batch()
    {
        const string scenePath = "Assets/Editor/CatProneReview/ExitCandidate/CatLifeExitCandidate.unity";
        const string candidateFolder = "Assets/Editor/CatProneReview/Atlas512RepackedCandidate";
        const string originalModel = "Assets/MobileRuntime/Art/Town/Source/CL_TWN_Runtime.fbx";
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Reports", "wp06-atlas512-repacked-20261017"));
        EditorSceneManager.OpenScene(scenePath);
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        MeshRenderer[] renderers = GameObject.Find("CatLifeMobileTown").GetComponentsInChildren<MeshRenderer>();
        var originalMaterials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().ToDictionary(material => material.name);
        StringBuilder report = new StringBuilder("Isolated pixel-preserving atlas repack; no scene save or formal asset replacement.\n");

        AssetDatabase.CreateFolder("Assets/Editor/CatProneReview", "Atlas512RepackedCandidate");
        var replacements = new System.Collections.Generic.Dictionary<string, Material>();
        for (int page = 1; page <= 2; page++)
        {
            string textureName = $"T_CL_TownAtlas_Mobile_BaseColor_{page:00}_512.png";
            string path = candidateFolder + "/" + textureName;
            string sourceTexture = AssetDatabase.GetAssetPath(originalMaterials["MAT_CL_TownAtlas_01"].mainTexture);
            if (!AssetDatabase.CopyAsset(sourceTexture, path)) throw new IOException("Cannot copy candidate texture: " + path);
            File.Copy(Path.Combine(directory, "tiles", textureName), path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = 512;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = 512;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var material = new Material(originalMaterials["MAT_CL_TownAtlas_01"])
                { name = $"MAT_CL_TownAtlasMobile_{page:00}", mainTexture = texture };
            AssetDatabase.CreateAsset(material, candidateFolder + "/" + material.name + ".mat");
            replacements.Add(material.name, material);
            report.AppendLine($"Atlas {texture.name}: {texture.width}x{texture.height}, Android={android.format}, sRGB={importer.sRGBTexture}, mipmaps={importer.mipmapEnabled}; tile=128; no resampling");
        }

        string modelPath = candidateFolder + "/CL_TWN_Runtime_atlas512_candidate.fbx";
        if (!AssetDatabase.CopyAsset(originalModel, modelPath)) throw new IOException("Cannot copy candidate FBX settings.");
        File.Copy(Path.Combine(directory, "CL_TWN_Runtime_atlas512_candidate.fbx"), modelPath, true);
        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var candidateMeshes = model.GetComponentsInChildren<MeshFilter>().ToDictionary(filter => filter.name);
        if (candidateMeshes.Count != 167 || renderers.Length != 167) throw new System.InvalidOperationException("Town object count changed.");
        int changed = 0;
        float maxBoundsDelta = 0;
        foreach (MeshRenderer renderer in renderers)
        {
            MeshFilter original = renderer.GetComponent<MeshFilter>();
            MeshFilter candidate = candidateMeshes[renderer.name];
            Mesh before = original.sharedMesh, after = candidate.sharedMesh;
            if (before.triangles.Length != after.triangles.Length) throw new System.InvalidOperationException("Triangle count changed: " + renderer.name);
            maxBoundsDelta = Mathf.Max(maxBoundsDelta, (before.bounds.center - after.bounds.center).magnitude,
                (before.bounds.size - after.bounds.size).magnitude);
            Material[] slots = candidate.GetComponent<MeshRenderer>().sharedMaterials;
            bool usesAtlas = false;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                string name = slots[slot].name;
                if (replacements.TryGetValue(name, out Material material)) { slots[slot] = material; usesAtlas = true; }
                else slots[slot] = originalMaterials[name];
            }
            original.sharedMesh = after;
            renderer.sharedMaterials = slots;
            if (usesAtlas) changed++;
        }
        int triangles = renderers.Sum(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
        int materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().Count();
        var identities = renderers.Select(renderer => renderer.GetComponent<CatLifeArtAssetIdentity>().assetId).Distinct().Count();
        if (triangles != 229286 || materials != 16 || identities != 167 || changed != 155 || maxBoundsDelta > .001f)
            throw new System.InvalidOperationException($"Repack changed geometry/budget: triangles={triangles}, materials={materials}, IDs={identities}, remapped={changed}, boundsDelta={maxBoundsDelta}");
        MeshRenderer island = renderers.Single(renderer => renderer.name == "CL_ENV_IslandBase_01");
        Mesh islandMesh = island.GetComponent<MeshFilter>().sharedMesh;
        if (islandMesh.subMeshCount != 3) throw new System.InvalidOperationException("Island material partition changed.");
        for (int i = 0; i < islandMesh.subMeshCount; i++)
            report.AppendLine($"Island submesh {i}: material={island.sharedMaterials[i].name}, indices={islandMesh.GetIndexCount(i)}");
        report.AppendLine($"triangles={triangles}; used_materials={materials}; IDs={identities}; remapped={changed}; max_mesh_bounds_delta={maxBoundsDelta}");

        var cameraObject = new GameObject("CatLifeRepackedAtlasCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.35f, .72f, .9f);
        RenderAtlasComparisonViews(camera, renderers, directory, "512-repacked", report);
        Object.DestroyImmediate(cameraObject);
        File.WriteAllText(Path.Combine(directory, "unity-comparison.txt"), report.ToString());
        AssetDatabase.SaveAssets();
        Debug.Log("CATLIFE_ATLAS512_REPACKED=" + directory + $" triangles={triangles} materials={materials} IDs={identities} remapped={changed}");
        EditorApplication.Exit(0);
    }

    private static void RenderAtlasComparisonViews(Camera camera, MeshRenderer[] renderers, string directory, string suffix, StringBuilder report)
    {
        RenderAuditView(camera, AuditBounds(renderers), Quaternion.Euler(35, 180, 0), 1600, 1200, directory, "island-front-" + suffix, report);
        MeshRenderer[] center = renderers.Where(renderer => new[] { "CL_BLD_CatHouse_01", "CL_ROAD_PlazaRing_01", "CL_PROP_CenterStone_01" }.Contains(renderer.name)).ToArray();
        RenderAuditView(camera, AuditBounds(center), Quaternion.Euler(24, 180, 0), 1600, 1200, directory, "landmarks-center-" + suffix, report);
        var director = Object.FindFirstObjectByType<CatLifeCameraDirector>();
        foreach (var phase in new[] { CatLife.Mobile.CatLifeSessionPhase.Normal, CatLife.Mobile.CatLifeSessionPhase.Focus })
        {
            var preset = director.GetPreset(phase);
            camera.orthographic = false;
            camera.aspect = 1080f / 2424f;
            camera.transform.SetPositionAndRotation(preset.Position, Quaternion.Euler(preset.Euler));
            camera.fieldOfView = preset.Fov; camera.nearClipPlane = .1f; camera.farClipPlane = 200;
            string label = phase == CatLife.Mobile.CatLifeSessionPhase.Normal ? "home-world-" : "focus-world-";
            RenderCamera(camera, 1080, 2424, Path.Combine(directory, label + suffix + ".png"));
            report.AppendLine($"View {label}{suffix}: preset={phase}; UI disabled; edit-mode cat pose is not an animation test.");
        }
    }

    public static void RenderBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
        Camera camera = Camera.main;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
        }
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            string materials = string.Join(";", renderer.sharedMaterials.Select(material =>
                material == null
                    ? "null"
                    : material.name + ":" + AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap"))));
            Debug.Log("CATLIFE_RENDERER name=" + renderer.name + " position=" + renderer.transform.position + " bounds=" + renderer.bounds + " visible=" + GeometryUtility.TestPlanesAABB(planes, renderer.bounds) + " materials=" + materials);
        }
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports", "MobileRebuild", "preview", "catlife-mobile-scene.png"));
        RenderCamera(camera, 1080, 2400, path);
        Debug.Log("CATLIFE_MOBILE_PREVIEW=" + path);
        EditorApplication.Exit(0);
    }

    private static void RenderCamera(Camera camera, int width, int height, string path)
    {
        RenderTexture target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(target);
    }
}
