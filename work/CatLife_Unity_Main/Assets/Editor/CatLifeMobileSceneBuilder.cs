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
    private const string CatSourcePath = "Assets/MobileRuntime/Art/Cat/PF_CL_CAT_Runtime_v12.prefab";
    private const string CatBaseColorPath = "Assets/MobileRuntime/Art/Cat/Textures/T_CL_Cat_BaseColor_1024.png";
    private const string CatNormalPath = "Assets/MobileRuntime/Art/Cat/Textures/T_CL_Cat_Normal_1024.png";
    private const string CloudTexturePath = "Assets/MobileRuntime/Art/Environment/T_CL_CloudBillboard_v01.png";
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
        string[] names = AssetDatabase.FindAssets("t:AnimationClip", new[] { CatLife.EditorTools.CatLifeRuntimeAnimatorSetup.ClipFolder.TrimEnd('/') })
            .Select(guid => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid)).name)
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
            AddWalkableSurfaceCollider(renderer.gameObject, row.RuntimeName, filter);

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
        BuildCloudLayer();
        CatLifeMobileCatPresenter cat = BuildCat();
        GameObject uiPrefab = CatLifeMobileUiPrefabBuilder.Build();
        GameObject ui = PrefabUtility.InstantiatePrefab(uiPrefab, scene) as GameObject;
        ui.name = "CatLifeMobileView";
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        GameObject systems = new GameObject("CatLifeRuntimeSystems", typeof(RealtimeFeatureEngine), typeof(MockRecognitionProvider), typeof(CatLifeMobileRuntimeCoordinator));
        CatLifeMobileRuntimeCoordinator runtime = systems.GetComponent<CatLifeMobileRuntimeCoordinator>();
        CatBehaviorDriver behavior = BuildMobileNavigation(systems.transform, cat.gameObject);
        Canvas canvas = ui.GetComponentInChildren<Canvas>(true);
        if (canvas == null) throw new InvalidDataException("Mobile UI prefab is missing its Canvas.");
        CatLife.UI.CatBubblePresenter catBubble = canvas.gameObject.AddComponent<CatLife.UI.CatBubblePresenter>();
        SetObjectReference(behavior, "bubblePresenter", catBubble);
        GameObject safeArea = GameObject.Find("SafeArea");
        if (safeArea == null) throw new InvalidDataException("Mobile UI prefab is missing SafeArea.");
        UnityEngine.UI.Image safeAreaImage = safeArea.GetComponent<UnityEngine.UI.Image>();
        safeAreaImage.raycastTarget = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(safeAreaImage);
        CatUiInteractionBridge uiCatInteraction = safeArea.AddComponent<CatUiInteractionBridge>();
        uiCatInteraction.Configure(behavior, Camera.main, cat.transform);
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
        CreateWalkArea(navigation.transform, "Walk_HomeForeground", new Vector3(0f, groundY - .06f, 4.5f), new Vector3(1.5f, .12f, 1.5f));

        Transform pointsRoot = new GameObject("CatInterestPoints").transform;
        pointsRoot.SetParent(navigation.transform, false);
        CatInterestPoint[] points =
        {
            CreateInterestPoint(pointsRoot, "Interest_HomeFront", "home_front", new Vector3(0f, groundY, 4.5f), new[] { "plaza", "quiet" }),
            CreateInterestPoint(pointsRoot, "Interest_Left_Garden", "left_garden", new Vector3(-.45f, groundY, 4.45f), new[] { "garden", "curious" }),
            CreateInterestPoint(pointsRoot, "Interest_Right_Garden", "right_garden", new Vector3(.45f, groundY, 4.45f), new[] { "garden", "curious" }),
            CreateInterestPoint(pointsRoot, "Interest_Front_Path", "front_path", new Vector3(0f, groundY, 4.3f), new[] { "path", "quiet" })
        };
        CatInterestPointRegistry registry = navigation.AddComponent<CatInterestPointRegistry>();
        registry.SetPoints(points);

        NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;
        CatLifeMobileNavigationRepair.Bake(surface, GameObject.Find("CatLifeMobileTown"));

        CatForbiddenZone[] forbiddenZones = BuildSemanticForbiddenZones(navigation.transform, groundY);

        NavMeshAgent agent = cat.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        agent.radius = .18f;
        agent.height = .55f;
        agent.baseOffset = 0f;
        agent.speed = 1.15f;
        agent.angularSpeed = 420f;
        agent.acceleration = 6f;
        agent.stoppingDistance = .01f;
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
        SetString(animation, "idleStateName", "CL_CAT_StandIdle_v01_loop_96f");
        SetObjectReference(planner, "planningCamera", Camera.main);
        SetObjectReference(planner, "interestPointRegistry", registry);
        SetObjectReference(planner, "needModel", needModel);
        SetObjectReference(planner, "behaviorMemory", memory);
        SetObjectArray(planner, "forbiddenZones", forbiddenZones);
        SetBool(planner, "preferCameraRangeWhenNonFocused", true);
        SetBool(planner, "preferCameraRangeWhenFocused", false);
        SetFloat(planner, "nonFocusSampleRadius", .55f);
        SetFloat(planner, "visibleBodyRadius", 1.25f);
        SetFloat(planner, "minMoveDistance", .35f);
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
        ConfigureCatInteraction(cat, behavior);
        CatLifeV12FootContactPreparation.Configure(cat);
        return behavior;
    }

    private static void ConfigureCatInteraction(GameObject cat, CatBehaviorDriver behavior)
    {
        Renderer[] renderers = cat.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidDataException("Mobile cat has no renderer for interaction bounds.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        BoxCollider collider = cat.AddComponent<BoxCollider>();
        collider.center = cat.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = cat.transform.lossyScale;
        collider.size = new Vector3(
            bounds.size.x / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(.0001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
        collider.isTrigger = true;

        CatInteractionMapper interaction = cat.AddComponent<CatInteractionMapper>();
        interaction.Configure(behavior, Camera.main, cat.transform);
    }

    private static CatForbiddenZone[] BuildSemanticForbiddenZones(Transform navigationRoot, float groundY)
    {
        Transform zonesRoot = new GameObject("CatForbiddenZones").transform;
        zonesRoot.SetParent(navigationRoot, false);
        string[] sourceNames =
        {
            "CL_BLD_FocusHouse_01", "CL_BLD_TomatoClockTower_01", "CL_BLD_CatHouse_01",
            "CL_BLD_FishShop_01", "CL_BLD_TownGate_01", "CL_ENV_RewardTree_01"
        };
        List<CatForbiddenZone> zones = new List<CatForbiddenZone>();
        foreach (string sourceName in sourceNames)
        {
            GameObject source = GameObject.Find(sourceName);
            if (source == null) throw new InvalidDataException("Forbidden-zone source is missing: " + sourceName);
            Renderer[] renderers = source.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidDataException("Forbidden-zone source has no renderer: " + sourceName);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            GameObject zoneObject = new GameObject("Forbidden_" + sourceName, typeof(CatForbiddenZone));
            zoneObject.transform.SetParent(zonesRoot, false);
            CatForbiddenZone zone = zoneObject.GetComponent<CatForbiddenZone>();
            zone.Configure(
                sourceName,
                CatForbiddenZone.ZoneSourceKind.RendererBounds,
                1.05f,
                new Vector3(bounds.center.x, groundY, bounds.center.z),
                new Vector3(Mathf.Max(.4f, bounds.size.x * 1.05f), .8f, Mathf.Max(.4f, bounds.size.z * 1.05f)));
            zoneObject.GetComponent<BoxCollider>().isTrigger = true;
            zones.Add(zone);
        }
        return zones.ToArray();
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray(UnityEngine.Object target, string propertyName, UnityEngine.Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
        interest.Configure(id, tags, 1f, .25f, .2f, true);
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

    internal static Material ResolveMaterial(Material imported)
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
        // SwiftShader GLES rejects the town's instanced Unlit variant (uniform limit).
        material.enableInstancing = false;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AddLandmarkInteraction(GameObject item, string landmarkId, MeshFilter filter)
    {
        CatLifeLandmarkAction? action = null;
        if (landmarkId == "TOMATO_CLOCK_TOWER") action = CatLifeLandmarkAction.Records;
        if (landmarkId == "CAT_HOUSE") action = CatLifeLandmarkAction.Growth;
        if (landmarkId == "FISH_SHOP") action = CatLifeLandmarkAction.FishShop;
        if (landmarkId == "REWARD_TREE") action = CatLifeLandmarkAction.RewardTree;
        if (landmarkId == "PLAZA_RING") action = CatLifeLandmarkAction.Plaza;
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
        sun.intensity = 1.12f;
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(48f, -26f, 0f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.58f, .70f, .82f, 1f);
        RenderSettings.ambientEquatorColor = new Color(.34f, .39f, .31f, 1f);
        RenderSettings.ambientGroundColor = new Color(.18f, .16f, .12f, 1f);
        return director;
    }

    private static void BuildCloudLayer()
    {
        TextureImporter importer = AssetImporter.GetAtPath(CloudTexturePath) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Cloud billboard texture is missing.", CloudTexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 1024;
        TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 512;
        android.format = TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CloudTexturePath);
        if (sprite == null) throw new InvalidDataException("Cloud billboard could not be imported as a sprite.");
        Transform root = new GameObject("CL_ENV_CloudLayer_01").transform;
        CreateCloud(root, sprite, "CL_ENV_Cloud_01", new Vector3(-8.5f, 8.2f, -14f), .58f, .80f);
        CreateCloud(root, sprite, "CL_ENV_Cloud_02", new Vector3(8.2f, 10.0f, -20f), .48f, .68f);
        CreateCloud(root, sprite, "CL_ENV_Cloud_03", new Vector3(1.0f, 14.5f, -31f), .72f, .42f);
    }

    private static void CreateCloud(Transform parent, Sprite sprite, string name, Vector3 position, float scale, float alpha)
    {
        GameObject cloud = new GameObject(name, typeof(SpriteRenderer));
        cloud.transform.SetParent(parent, false);
        cloud.transform.position = position;
        cloud.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = cloud.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 1f, 1f, alpha);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static CatLifeMobileCatPresenter BuildCat()
    {
        GameObject catSource = AssetDatabase.LoadAssetAtPath<GameObject>(CatSourcePath);
        if (catSource == null) throw new FileNotFoundException("Standardized cat FBX is missing.", CatSourcePath);
        GameObject cat = PrefabUtility.InstantiatePrefab(catSource) as GameObject;
        cat.name = "CatLifeMobileCat";
        cat.transform.position = new Vector3(0f, .02f, 4.5f);
        cat.transform.rotation = Quaternion.Euler(0f, 22f, 0f);
        cat.transform.localScale = Vector3.one * .045f;

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
        foreach (SkinnedMeshRenderer renderer in cat.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            renderer.sharedMaterial = material;
            renderer.updateWhenOffscreen = true;
            for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++) renderer.SetBlendShapeWeight(i, 0f);
        }
        ConfigureCatPhysics(cat);
        Animator animator = cat.GetComponentInChildren<Animator>();
        if (animator == null) animator = cat.AddComponent<Animator>();
        animator.runtimeAnimatorController = BuildCatAnimator();
        animator.applyRootMotion = false;
        CatLifeMobileCatPresenter presenter = cat.AddComponent<CatLifeMobileCatPresenter>();
        presenter.ConfigureSessionAnchor(cat.transform.position, cat.transform.rotation);
        return presenter;
    }

    private static void AddWalkableSurfaceCollider(GameObject item, string runtimeName, MeshFilter filter)
    {
        if (filter == null || filter.sharedMesh == null) return;
        bool walkableSurface = runtimeName == "CL_ENV_IslandBase_01" ||
            runtimeName == "CL_ENV_IslandGrass_01" ||
            runtimeName == "CL_ROAD_CentralGardenRing_01" ||
            runtimeName == "CL_ROAD_PlazaPath_01" ||
            runtimeName == "CL_ROAD_PlazaRing_01" ||
            runtimeName == "CL_PROP_CenterStone_01";
        if (!walkableSurface) return;

        MeshCollider collider = item.GetComponent<MeshCollider>();
        if (collider == null) collider = item.AddComponent<MeshCollider>();
        collider.sharedMesh = filter.sharedMesh;
        collider.convex = false;
        collider.isTrigger = false;
    }

    private static void ConfigureCatPhysics(GameObject cat)
    {
        Renderer[] renderers = cat.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidDataException("Mobile cat has no renderer for physics bounds.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        Vector3 scale = cat.transform.lossyScale;
        float worldRadius = Mathf.Min(bounds.size.x, bounds.size.z) * .26f;
        float worldHeight = Mathf.Max(worldRadius * 2f, bounds.size.y * .88f);
        Vector3 worldCenter = new Vector3(bounds.center.x, cat.transform.position.y + worldHeight * .5f, bounds.center.z);

        CapsuleCollider bodyCollider = cat.AddComponent<CapsuleCollider>();
        bodyCollider.direction = 1;
        bodyCollider.center = cat.transform.InverseTransformPoint(worldCenter);
        bodyCollider.radius = worldRadius / Mathf.Max(.0001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        bodyCollider.height = worldHeight / Mathf.Max(.0001f, Mathf.Abs(scale.y));
        bodyCollider.isTrigger = false;

        Rigidbody body = cat.AddComponent<Rigidbody>();
        body.mass = 3f;
        body.useGravity = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private static AnimatorController BuildCatAnimator()
    {
        AnimatorController controller = CatLife.EditorTools.CatLifeRuntimeAnimatorSetup.EnsureController();
        CatLife.EditorTools.CatLifeRuntimeAnimatorSetup.RebuildControllerStates(controller);
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
