using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CatLife.Cat;
using CatLife.Editor;
using CatLife.Recognition;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AI;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CatLifeMobileSceneBuilder
{
    private const string TownSourcePath = "Assets/MobileRuntime/Art/Town/Source/CL_TWN_Runtime.fbx";
    private const string TownManifestPath = "Assets/MobileRuntime/Art/Town/Catalog/asset_manifest.csv";
    private const string CatSourcePath = "Assets/MobileRuntime/Art/Cat/Source/CL_CAT_Runtime.fbx";
    private const string CatBaseColorPath = "Assets/MobileRuntime/Art/Cat/Textures/T_CL_Cat_BaseColor_1024.png";
    private const string CatNormalPath = "Assets/MobileRuntime/Art/Cat/Textures/T_CL_Cat_Normal_1024.png";
    private const string Root = "Assets/MobileRuntime/Art";
    private const string ScenePath = "Assets/Scenes/CatLifeMobile.unity";
    private const string TownPrefabPath = "Assets/MobileRuntime/Art/Town/PF_CL_TWN_Town.prefab";
    private const string CatControllerPath = "Assets/MobileRuntime/Art/Cat/CL_CAT_Mobile.controller";
    private const string NavMeshDataPath = "Assets/MobileRuntime/Navigation/CL_NAV_Mobile.asset";

    private sealed class ManifestRow
    {
        public string AssetId;
        public string DisplayNameZh;
        public string RuntimeName;
        public string Category;
        public string LandmarkId;
    }

    private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>(StringComparer.Ordinal)
    {
        { "MAT_CL_GrassSoftGreen", new Color(.40f, .67f, .22f, 1f) },
        { "MAT_CL_GrassLight", new Color(.60f, .80f, .31f, 1f) },
        { "MAT_CL_GrassDeep", new Color(.27f, .52f, .15f, 1f) },
        { "MAT_CL_SoilWarm", new Color(.58f, .31f, .12f, 1f) },
        { "MAT_CL_SoilEdge", new Color(.72f, .43f, .20f, 1f) },
        { "MAT_CL_IslandDarkBottom", new Color(.25f, .14f, .07f, 1f) },
        { "MAT_CL_StoneLight", new Color(.62f, .57f, .49f, 1f) },
        { "MAT_CL_WoodWarm", new Color(.48f, .27f, .12f, 1f) },
        { "MAT_CL_WallCream", new Color(.88f, .76f, .56f, 1f) },
        { "MAT_CL_FoliageGreen", new Color(.25f, .55f, .19f, 1f) },
        { "MAT_CL_AccentFlower", new Color(.96f, .55f, .38f, 1f) }
    };

    private static readonly Dictionary<string, string> MaterialTextures = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "MAT_CL_FocusHouse", "T_CL_FocusHouse_BaseColor_1024.png" },
        { "MAT_CL_TomatoClockTower", "T_CL_TomatoClockTower_BaseColor_1024.png" },
        { "MAT_CL_CatHouse", "T_CL_CatHouse_BaseColor_1024.png" },
        { "MAT_CL_FishShop", "T_CL_FishShop_BaseColor_1024.png" },
        { "MAT_CL_TownGate", "T_CL_TownGate_BaseColor_1024.png" },
        { "MAT_CL_RewardTree", "T_CL_RewardTree_BaseColor_1024.png" },
        { "MAT_CL_Plaza", "T_CL_Plaza_BaseColor_1024.png" },
        { "MAT_CL_CenterStone", "T_CL_CenterStone_BaseColor_1024.png" },
        { "MAT_CL_TownAtlas_01", "T_CL_TownAtlas_BaseColor_01_1024.png" },
        { "MAT_CL_TownAtlas_02", "T_CL_TownAtlas_BaseColor_02_1024.png" },
        { "MAT_CL_TownAtlas_03", "T_CL_TownAtlas_BaseColor_03_1024.png" }
    };

    [MenuItem("CatLife/Mobile Rebuild/Build Mobile Scene")]
    public static void BuildFromMenu()
    {
        Build();
    }

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    public static void AuditCatClipsBatch()
    {
        string[] names = AssetDatabase.LoadAllAssetsAtPath(CatSourcePath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            .Select(clip => clip.name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Debug.Log("[CatLifeCatClipAudit] " + string.Join(" | ", names));
        EditorApplication.Exit(0);
    }

    private static void Build()
    {
        EnsureFolders();
        Dictionary<string, ManifestRow> manifest = LoadManifest();
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TownSourcePath);
        if (source == null) throw new FileNotFoundException("Standardized town FBX is missing.", TownSourcePath);

        GameObject town = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (town == null) throw new InvalidOperationException("Standardized town FBX could not be instantiated.");
        town.name = "CatLifeMobileTown";
        town.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        town.transform.localScale = Vector3.one;

        HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
        HashSet<Material> materials = new HashSet<Material>();
        int triangles = 0;
        foreach (MeshRenderer renderer in town.GetComponentsInChildren<MeshRenderer>(true))
        {
            ManifestRow row;
            if (!manifest.TryGetValue(renderer.name, out row))
            {
                throw new InvalidDataException("Town renderer is not registered in the art manifest: " + renderer.name);
            }

            found.Add(row.RuntimeName);
            renderer.sharedMaterials = renderer.sharedMaterials.Select(ResolveMaterial).ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            foreach (Material material in renderer.sharedMaterials) if (material != null) materials.Add(material);

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) triangles += filter.sharedMesh.triangles.Length / 3;

            CatLifeArtAssetIdentity identity = renderer.gameObject.GetComponent<CatLifeArtAssetIdentity>();
            if (identity == null) identity = renderer.gameObject.AddComponent<CatLifeArtAssetIdentity>();
            identity.assetId = row.AssetId;
            identity.displayNameZh = row.DisplayNameZh;
            identity.landmarkId = row.LandmarkId;
            AddLandmarkInteraction(renderer.gameObject, row.LandmarkId, filter);
        }

        string[] missing = manifest.Values.Where(row => !found.Contains(row.RuntimeName)).Select(row => row.RuntimeName).ToArray();
        if (missing.Length > 0) throw new InvalidDataException("Manifest assets missing from town FBX: " + string.Join(", ", missing));

        PrefabUtility.SaveAsPrefabAsset(town, TownPrefabPath);
        UnityEngine.Object.DestroyImmediate(town);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownPrefabPath);
        GameObject townInstance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        townInstance.name = "CatLifeMobileTown";
        CatLifeCameraDirector director = BuildCameraAndLighting();
        CatLifeMobileCatPresenter cat = BuildCat();
        GameObject uiPrefab = CatLifeMobileUiPrefabBuilder.Build();
        GameObject ui = PrefabUtility.InstantiatePrefab(uiPrefab, scene) as GameObject;
        ui.name = "CatLifeMobileView";
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        GameObject systems = new GameObject("CatLifeRuntimeSystems", typeof(RealtimeFeatureEngine), typeof(MockRecognitionProvider), typeof(CatLifeMobileRuntimeCoordinator));
        CatLifeMobileRuntimeCoordinator runtime = systems.GetComponent<CatLifeMobileRuntimeCoordinator>();
        CatBehaviorDriver behavior = BuildMobileNavigation(systems.transform, cat.gameObject);
        runtime.Configure(systems.GetComponent<RealtimeFeatureEngine>(), systems.GetComponent<MockRecognitionProvider>(), cat, behavior);
        GameObject appObject = new GameObject("CatLifeMobileApp", typeof(CatLifeMobileApp), typeof(CatLife.LLM.MockCatLLMClient));
        appObject.GetComponent<CatLifeMobileApp>().Configure(ui, director, runtime);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        WriteReport(triangles, materials.Count, manifest.Count);
        Debug.Log($"[CatLifeMobileSceneBuilder] Built standardized mobile scene triangles={triangles} materials={materials.Count} assets={manifest.Count}");
    }

    private static CatBehaviorDriver BuildMobileNavigation(Transform systemsRoot, GameObject cat)
    {
        GameObject navigation = new GameObject("CatLifeNavigation");
        navigation.transform.SetParent(systemsRoot, false);
        float groundY = cat.transform.position.y;

        CreateWalkArea(navigation.transform, "Walk_MainPlaza", new Vector3(0f, groundY - .06f, -6.8f), new Vector3(7.4f, .12f, 4.5f));
        CreateWalkArea(navigation.transform, "Walk_LeftGarden", new Vector3(-3.9f, groundY - .06f, -6.3f), new Vector3(2.8f, .12f, 3.6f));
        CreateWalkArea(navigation.transform, "Walk_RightGarden", new Vector3(3.9f, groundY - .06f, -6.3f), new Vector3(2.8f, .12f, 3.6f));
        CreateWalkArea(navigation.transform, "Walk_FrontPath", new Vector3(0f, groundY - .06f, -9.25f), new Vector3(6.6f, .12f, 2.2f));

        Transform pointsRoot = new GameObject("CatInterestPoints").transform;
        pointsRoot.SetParent(navigation.transform, false);
        CatInterestPoint[] points =
        {
            CreateInterestPoint(pointsRoot, "Interest_HomeFront", "home_front", new Vector3(-.3f, groundY, -6.78f), new[] { "plaza", "quiet" }),
            CreateInterestPoint(pointsRoot, "Interest_Left_Garden", "left_garden", new Vector3(-3.4f, groundY, -6.4f), new[] { "garden", "curious" }),
            CreateInterestPoint(pointsRoot, "Interest_Right_Garden", "right_garden", new Vector3(3.4f, groundY, -6.4f), new[] { "garden", "curious" }),
            CreateInterestPoint(pointsRoot, "Interest_Front_Path", "front_path", new Vector3(0f, groundY, -8.9f), new[] { "path", "quiet" })
        };
        CatInterestPointRegistry registry = navigation.AddComponent<CatInterestPointRegistry>();
        registry.SetPoints(points);

        NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        surface.BuildNavMesh();
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshDataPath) != null)
            AssetDatabase.DeleteAsset(NavMeshDataPath);
        AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);

        NavMeshAgent agent = cat.AddComponent<NavMeshAgent>();
        agent.radius = .18f;
        agent.height = .55f;
        agent.baseOffset = 0f;
        agent.speed = 1.15f;
        agent.angularSpeed = 420f;
        agent.acceleration = 6f;
        agent.stoppingDistance = .16f;
        agent.autoBraking = false;
        agent.updateRotation = true;
        CatNavigationAgent navigationAgent = cat.AddComponent<CatNavigationAgent>();
        cat.AddComponent<CatNavMeshSafetyGuard>();
        CatDestinationPlanner planner = cat.AddComponent<CatDestinationPlanner>();
        CatAnimationController animation = cat.AddComponent<CatAnimationController>();
        CatActionRouter actionRouter = cat.AddComponent<CatActionRouter>();
        CatNeedModel needModel = cat.AddComponent<CatNeedModel>();
        CatBehaviorMemory memory = cat.AddComponent<CatBehaviorMemory>();
        CatBehaviorBrainScorer scorer = cat.AddComponent<CatBehaviorBrainScorer>();
        CatBehaviorDriver behavior = cat.AddComponent<CatBehaviorDriver>();

        SetObjectReference(animation, "animator", cat.GetComponentInChildren<Animator>());
        SetString(animation, "idleStateName", "CL_CAT_SitIdle_v01_loop_96f");
        SetObjectReference(planner, "planningCamera", Camera.main);
        SetObjectReference(planner, "interestPointRegistry", registry);
        SetObjectReference(planner, "needModel", needModel);
        SetObjectReference(planner, "behaviorMemory", memory);
        SetBool(planner, "preferCameraRangeWhenNonFocused", false);
        SetBool(planner, "preferCameraRangeWhenFocused", false);
        SetFloat(planner, "minMoveDistance", .6f);
        SetFloat(planner, "navMeshProbeDistance", 1.2f);
        SetInt(planner, "sampleAttempts", 32);
        SetObjectReference(behavior, "recognitionProviderComponent", systemsRoot.GetComponent<MockRecognitionProvider>());
        SetObjectReference(behavior, "navigationAgent", navigationAgent);
        SetObjectReference(behavior, "animationController", animation);
        SetObjectReference(behavior, "destinationPlanner", planner);
        SetObjectReference(behavior, "actionRouter", actionRouter);
        SetObjectReference(behavior, "featureEngine", systemsRoot.GetComponent<RealtimeFeatureEngine>());
        SetObjectReference(behavior, "needModel", needModel);
        SetObjectReference(behavior, "behaviorMemory", memory);
        SetObjectReference(behavior, "behaviorScorer", scorer);
        return behavior;
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(UnityEngine.Object target, string propertyName, string value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(UnityEngine.Object target, string propertyName, bool value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateWalkArea(Transform parent, string name, Vector3 position, Vector3 size)
    {
        GameObject area = new GameObject(name, typeof(BoxCollider));
        area.transform.SetParent(parent, false);
        area.transform.position = position;
        area.GetComponent<BoxCollider>().size = size;
    }

    private static CatInterestPoint CreateInterestPoint(Transform parent, string name, string id, Vector3 position, string[] tags)
    {
        GameObject point = new GameObject(name, typeof(CatInterestPoint));
        point.transform.SetParent(parent, false);
        point.transform.position = position;
        CatInterestPoint interest = point.GetComponent<CatInterestPoint>();
        interest.Configure(id, tags, 1f, .25f, .45f, true);
        return interest;
    }

    private static Dictionary<string, ManifestRow> LoadManifest()
    {
        string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", TownManifestPath));
        if (!File.Exists(absolutePath)) throw new FileNotFoundException("Town art manifest is missing.", absolutePath);
        string[] lines = File.ReadAllLines(absolutePath);
        Dictionary<string, ManifestRow> result = new Dictionary<string, ManifestRow>(StringComparer.Ordinal);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            string[] cells = lines[i].Split(',');
            if (cells.Length != 14) throw new InvalidDataException($"Manifest row {i + 1} has {cells.Length} columns; expected 14.");
            ManifestRow row = new ManifestRow
            {
                AssetId = cells[0].TrimStart('\uFEFF'),
                DisplayNameZh = cells[1],
                RuntimeName = cells[2],
                Category = cells[3],
                LandmarkId = cells[12]
            };
            if (!result.TryAdd(row.RuntimeName, row)) throw new InvalidDataException("Duplicate runtime_name in art manifest: " + row.RuntimeName);
        }
        return result;
    }

    private static Material ResolveMaterial(Material imported)
    {
        if (imported == null) return null;
        string name = imported.name;
        string path = Root + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;

        string textureName;
        if (MaterialTextures.TryGetValue(name, out textureName))
        {
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Town/Textures/" + textureName));
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", Color.white);
        }
        else
        {
            Color color;
            if (!Palette.TryGetValue(name, out color)) color = new Color(.65f, .62f, .55f, 1f);
            material.SetTexture("_BaseMap", null);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", color);
        }
        material.SetFloat("_Smoothness", .18f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AddLandmarkInteraction(GameObject item, string landmarkId, MeshFilter filter)
    {
        CatLifeLandmarkAction? action = null;
        if (landmarkId == "TOMATO_CLOCK_TOWER") action = CatLifeLandmarkAction.Records;
        if (landmarkId == "CAT_HOUSE") action = CatLifeLandmarkAction.Growth;
        if (!action.HasValue) return;

        CatLifeLandmark landmark = item.GetComponent<CatLifeLandmark>();
        if (landmark == null) landmark = item.AddComponent<CatLifeLandmark>();
        landmark.action = action.Value;
        BoxCollider collider = item.GetComponent<BoxCollider>();
        if (collider == null) collider = item.AddComponent<BoxCollider>();
        if (filter != null && filter.sharedMesh != null)
        {
            collider.center = filter.sharedMesh.bounds.center;
            collider.size = filter.sharedMesh.bounds.size;
        }
    }

    private static CatLifeCameraDirector BuildCameraAndLighting()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        Camera camera = cameraObject.GetComponent<Camera>();
        cameraObject.tag = "MainCamera";
        CatLifeCameraDirector director = cameraObject.AddComponent<CatLifeCameraDirector>();
        director.Configure(camera);
        CatLifeCameraDirector.Preset preset = CatLifeCameraDirector.HomePreset;
        camera.transform.SetPositionAndRotation(preset.Position, Quaternion.Euler(preset.Euler));
        camera.fieldOfView = preset.Fov;
        camera.nearClipPlane = .05f;
        camera.farClipPlane = 180f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.35f, .75f, .95f, 1f);

        GameObject sunObject = new GameObject("Warm Sun", typeof(Light));
        Light sun = sunObject.GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, .91f, .74f, 1f);
        sun.intensity = .9f;
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(48f, -26f, 0f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.52f, .68f, .82f, 1f);
        RenderSettings.ambientEquatorColor = new Color(.48f, .52f, .42f, 1f);
        RenderSettings.ambientGroundColor = new Color(.28f, .24f, .18f, 1f);
        return director;
    }

    private static CatLifeMobileCatPresenter BuildCat()
    {
        GameObject catSource = AssetDatabase.LoadAssetAtPath<GameObject>(CatSourcePath);
        if (catSource == null) throw new FileNotFoundException("Standardized cat FBX is missing.", CatSourcePath);
        GameObject cat = PrefabUtility.InstantiatePrefab(catSource) as GameObject;
        cat.name = "CatLifeMobileCat";
        cat.transform.position = new Vector3(-.3f, .02f, -6.78f);
        cat.transform.rotation = Quaternion.identity;
        cat.transform.localScale = Vector3.one * .0275f;

        string materialPath = Root + "/Materials/MAT_CL_Cat.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CatBaseColorPath));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CatNormalPath));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_BumpScale", .55f);
        material.SetFloat("_Smoothness", .25f);
        material.EnableKeyword("_NORMALMAP");
        foreach (SkinnedMeshRenderer renderer in cat.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.sharedMaterial = material;
        Animator animator = cat.GetComponentInChildren<Animator>();
        if (animator == null) animator = cat.AddComponent<Animator>();
        animator.runtimeAnimatorController = BuildCatAnimator();
        animator.applyRootMotion = false;
        return cat.AddComponent<CatLifeMobileCatPresenter>();
    }

    private static AnimatorController BuildCatAnimator()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CatControllerPath) != null) AssetDatabase.DeleteAsset(CatControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(CatControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(CatSourcePath).OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
        string[] required =
        {
            "CL_CAT_SRC_Walk_60fps",
            "CL_CAT_AlertLook_v01_loop_120f", "CL_CAT_PawWave_v01_loop_96f", "CL_CAT_TailWagHappy_v01_loop_96f",
            "CL_CAT_CuriousSniff_v02_loop_112f", "CL_CAT_HeadTiltListen_v01_loop_96f", "CL_CAT_LookBack_v02_loop_112f",
            "CL_CAT_StretchYawn_v03_slow_loop_264f", "CL_CAT_EarTwitchAlert_v02_loop_120f", "CL_CAT_HeadShakeNo_v01_loop_108f",
            "CL_CAT_SitDownTransition_v01_72f", "CL_CAT_SitIdle_v01_loop_96f", "CL_CAT_LieDownTransition_v01_120f",
            "CL_CAT_FocusRest_v01_loop_96f", "CL_CAT_FocusAttention_v01_48f", "CL_CAT_WakeUpTransition_v01_72f"
        };
        foreach (string name in required)
        {
            AnimationClip clip = clips.FirstOrDefault(item => item.name.EndsWith(name, StringComparison.Ordinal));
            if (clip == null) throw new InvalidDataException("Required cat animation missing: " + name);
            AnimatorState state = machine.AddState(name); state.motion = clip; state.writeDefaultValues = true;
            if (name == "CL_CAT_SitIdle_v01_loop_96f") machine.defaultState = state;
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureFolders()
    {
        foreach (string path in new[]
        {
            "Assets/MobileRuntime", Root, Root + "/Town", Root + "/Town/Source", Root + "/Town/Textures", Root + "/Town/Catalog",
            Root + "/Cat", Root + "/Cat/Source", Root + "/Cat/Textures", Root + "/Cat/Catalog", Root + "/Materials",
            "Assets/MobileRuntime/Navigation"
        })
        {
            if (AssetDatabase.IsValidFolder(path)) continue;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    private static void WriteReport(int triangles, int materials, int assets)
    {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports", "MobileRebuild", "mobile-town"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "mobile_town_budget.txt"),
            "Town assets: " + assets.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
            "Town triangles: " + triangles.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
            "Town materials: " + materials.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
            "Budget: 150000-300000 triangles, <= 20 materials" + Environment.NewLine +
            "Source: standardized CL_TWN_Runtime.fbx and asset_manifest.csv." + Environment.NewLine +
            "Traceability: semantic IDs, source object names, versions, sizes, and visual receipts; no checksum gate.");
    }
}
