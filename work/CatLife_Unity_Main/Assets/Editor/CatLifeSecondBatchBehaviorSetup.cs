using CatLife.Cat;
using CatLife.LLM;
using CatLife.Recognition;
using CatLife.UI;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace CatLife.EditorTools
{
    public static class CatLifeSecondBatchBehaviorSetup
    {
        private const string MenuPath = "CatLife/Runtime/Setup Second Batch Behavior";
        private const string RuntimeName = "Runtime";
        private const string NavigationName = "Navigation";
        private const string SystemsName = "CatBehaviorSystems";
        private const string AnchorRootName = "CatDestinationAnchors";
        private const string ForbiddenRootName = "CatForbiddenZones";
        private const string InterestRootName = "CatInterestPoints";
        private const string CatName = "CatCompanionModel";
        private const float ForbiddenProjectionScale = 1.05f;
        private const float ForbiddenZoneHeight = 0.9f;
        private const float MinObstacleHeight = 0.28f;
        private const float MinObstacleProjectionSize = 0.18f;
        private const float ComplexManualAreaThreshold = 30f;
        private const float MaxAutomaticObstacleProjectionArea = 500f;

        [MenuItem(MenuPath)]
        public static void Setup()
        {
            GameObject cat = GameObject.Find(CatName);
            if (cat == null)
            {
                Debug.LogError("CatLife second batch setup failed: CatCompanionModel was not found.");
                return;
            }

            Transform runtime = GetOrCreateRoot(RuntimeName);
            Transform navigationRoot = GetOrCreateChild(runtime, NavigationName);
            Transform systemsRoot = GetOrCreateChild(runtime, SystemsName);
            Transform anchorRoot = GetOrCreateChild(navigationRoot, AnchorRootName);
            Transform forbiddenRoot = GetOrCreateChild(navigationRoot, ForbiddenRootName);
            Transform interestRoot = GetOrCreateChild(navigationRoot, InterestRootName);

            float groundY = cat.transform.position.y;
            CreateWalkArea(navigationRoot, "CatWalkableArea_MainPlaza", new Vector3(0f, groundY - 0.06f, -6.8f), new Vector3(7.4f, 0.12f, 4.5f));
            CreateWalkArea(navigationRoot, "CatWalkableArea_LeftGardenPath", new Vector3(-3.9f, groundY - 0.06f, -6.3f), new Vector3(2.8f, 0.12f, 3.6f));
            CreateWalkArea(navigationRoot, "CatWalkableArea_RightGardenPath", new Vector3(3.9f, groundY - 0.06f, -6.3f), new Vector3(2.8f, 0.12f, 3.6f));
            CreateWalkArea(navigationRoot, "CatWalkableArea_FrontStoneRing", new Vector3(0f, groundY - 0.06f, -9.25f), new Vector3(6.6f, 0.12f, 2.2f));
            CreateCenterSecondStoneRingWalkAreas(navigationRoot, groundY);
            CatForbiddenZone[] forbiddenZones = BuildForbiddenZones(forbiddenRoot, navigationRoot, groundY);

            NavMeshSurface surface = navigationRoot.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = navigationRoot.gameObject.AddComponent<NavMeshSurface>();
            }

            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.defaultArea = 0;
            surface.agentTypeID = 0;
            surface.overrideTileSize = false;
            surface.overrideVoxelSize = false;
            surface.BuildNavMesh();

            Transform[] anchors = new[]
            {
                GetOrCreateAnchor(anchorRoot, "Anchor_CenterPlaza", new Vector3(-0.3f, groundY, -6.78f)),
                GetOrCreateAnchor(anchorRoot, "Anchor_FrontPath", new Vector3(0f, groundY, -8.9f)),
                GetOrCreateAnchor(anchorRoot, "Anchor_LeftGarden", new Vector3(-3.4f, groundY, -6.4f)),
                GetOrCreateAnchor(anchorRoot, "Anchor_RightGarden", new Vector3(3.4f, groundY, -6.4f)),
                GetOrCreateAnchor(anchorRoot, "Anchor_LeftFront", new Vector3(-2.4f, groundY, -8.6f)),
                GetOrCreateAnchor(anchorRoot, "Anchor_RightFront", new Vector3(2.4f, groundY, -8.6f)),
            };
            CatInterestPoint[] interestPoints = CreateInterestPoints(interestRoot, groundY);
            CatInterestPointRegistry interestPointRegistry = GetOrAdd<CatInterestPointRegistry>(navigationRoot.gameObject);
            interestPointRegistry.SetPoints(interestPoints);
            EditorUtility.SetDirty(interestPointRegistry);

            MockRecognitionProvider recognitionProvider = GetOrAdd<MockRecognitionProvider>(systemsRoot.gameObject);
            RealtimeFeatureEngine featureEngine = GetOrAdd<RealtimeFeatureEngine>(systemsRoot.gameObject);
            MockCatLLMClient llmClient = GetOrAdd<MockCatLLMClient>(systemsRoot.gameObject);
            AssignObject(recognitionProvider, "featureEngine", featureEngine);

            NavMeshAgent agent = GetOrAdd<NavMeshAgent>(cat);
            agent.radius = 0.18f;
            agent.height = 0.55f;
            agent.baseOffset = 0f;
            agent.speed = 1.15f;
            agent.angularSpeed = 420f;
            agent.acceleration = 6f;
            agent.stoppingDistance = 0.16f;
            agent.autoBraking = false;
            agent.updatePosition = true;
            agent.updateRotation = true;

            CatNavigationAgent navigationAgent = GetOrAdd<CatNavigationAgent>(cat);
            CatNavMeshSafetyGuard safetyGuard = GetOrAdd<CatNavMeshSafetyGuard>(cat);
            CatDestinationPlanner destinationPlanner = GetOrAdd<CatDestinationPlanner>(cat);
            CatAnimationController animationController = GetOrAdd<CatAnimationController>(cat);
            CatActionRouter actionRouter = GetOrAdd<CatActionRouter>(cat);
            CatNeedModel needModel = GetOrAdd<CatNeedModel>(cat);
            CatBehaviorMemory behaviorMemory = GetOrAdd<CatBehaviorMemory>(cat);
            CatBehaviorBrainScorer behaviorScorer = GetOrAdd<CatBehaviorBrainScorer>(cat);
            CatBehaviorDriver behaviorDriver = GetOrAdd<CatBehaviorDriver>(cat);
            Animator animator = cat.GetComponent<Animator>();

            AssignObject(navigationAgent, "agent", agent);
            AssignObject(safetyGuard, "agent", agent);
            AssignObject(safetyGuard, "navigationAgent", navigationAgent);
            AssignPlanner(destinationPlanner, anchors, forbiddenZones, interestPointRegistry, needModel, behaviorMemory);
            AssignObject(animationController, "animator", animator);
            AssignDriver(
                behaviorDriver,
                recognitionProvider,
                llmClient,
                navigationAgent,
                animationController,
                destinationPlanner,
                actionRouter,
                featureEngine,
                needModel,
                behaviorMemory,
                behaviorScorer);

            CatTownWalker legacyWalker = cat.GetComponent<CatTownWalker>();
            if (legacyWalker != null)
            {
                legacyWalker.enabled = false;
            }

            CatLifeHomeUiController uiController = Object.FindAnyObjectByType<CatLifeHomeUiController>();
            if (uiController != null)
            {
                AssignObject(uiController, "catBehaviorDriver", behaviorDriver);
                AssignObject(uiController, "catWalker", legacyWalker);
            }

            EditorUtility.SetDirty(cat);
            EditorUtility.SetDirty(navigationRoot.gameObject);
            EditorUtility.SetDirty(forbiddenRoot.gameObject);
            EditorUtility.SetDirty(interestRoot.gameObject);
            EditorUtility.SetDirty(systemsRoot.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("CatLife second batch setup completed: NavMeshSurface, walk areas, cat NavMeshAgent, NavMesh safety guard, behavior driver, recognition mock, LLM mock, and UI binding are installed.");
        }

        private static Transform GetOrCreateRoot(string name)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                return found.transform;
            }

            GameObject created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create CatLife runtime root");
            return created.transform;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            GameObject created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create CatLife runtime child");
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        private static void CreateWalkArea(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            Transform existing = parent.Find(name);
            GameObject area = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            area.name = name;
            area.transform.SetParent(parent, true);
            area.transform.position = position;
            area.transform.rotation = Quaternion.identity;
            area.transform.localScale = scale;

            MeshRenderer renderer = area.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            BoxCollider collider = area.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = area.AddComponent<BoxCollider>();
            }

            collider.isTrigger = false;
            area.isStatic = true;
            EditorUtility.SetDirty(area);
        }

        private static void CreateCenterSecondStoneRingWalkAreas(Transform parent, float groundY)
        {
            Vector3 center = new Vector3(-0.14f, groundY - 0.06f, 0.18f);
            float outerSize = 11.96f;
            float innerSize = 4.82f;
            float ringWidth = (outerSize - innerSize) * 0.5f;
            float offset = innerSize * 0.5f + ringWidth * 0.5f;

            CreateWalkArea(
                parent,
                "CatWalkableArea_CenterSecondRing_North",
                new Vector3(center.x, center.y, center.z + offset),
                new Vector3(outerSize, 0.12f, ringWidth));
            CreateWalkArea(
                parent,
                "CatWalkableArea_CenterSecondRing_South",
                new Vector3(center.x, center.y, center.z - offset),
                new Vector3(outerSize, 0.12f, ringWidth));
            CreateWalkArea(
                parent,
                "CatWalkableArea_CenterSecondRing_West",
                new Vector3(center.x - offset, center.y, center.z),
                new Vector3(ringWidth, 0.12f, innerSize));
            CreateWalkArea(
                parent,
                "CatWalkableArea_CenterSecondRing_East",
                new Vector3(center.x + offset, center.y, center.z),
                new Vector3(ringWidth, 0.12f, innerSize));
        }

        private static Transform GetOrCreateAnchor(Transform parent, string name, Vector3 position)
        {
            Transform anchor = parent.Find(name);
            if (anchor == null)
            {
                GameObject created = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(created, "Create cat destination anchor");
                anchor = created.transform;
                anchor.SetParent(parent, false);
            }

            anchor.position = position;
            return anchor;
        }

        private static CatInterestPoint[] CreateInterestPoints(Transform parent, float groundY)
        {
            return new[]
            {
                GetOrCreateInterestPoint(parent, "Interest_Plaza_HomeFront", "plaza_home_front", new Vector3(-0.3f, groundY, -6.78f), new[] { "plaza", "near_home", "path", "quiet" }, 1.4f, 0.7f, 0.7f, true),
                GetOrCreateInterestPoint(parent, "Interest_Front_Path", "front_path", new Vector3(0f, groundY, -8.9f), new[] { "path", "edge" }, 1.2f, 0.45f, 0.8f, true),
                GetOrCreateInterestPoint(parent, "Interest_Left_Garden", "left_garden", new Vector3(-3.4f, groundY, -6.4f), new[] { "garden", "flower", "shade" }, 1.45f, 0.35f, 0.75f, true),
                GetOrCreateInterestPoint(parent, "Interest_Right_Garden", "right_garden", new Vector3(3.4f, groundY, -6.4f), new[] { "garden", "flower", "shade" }, 1.45f, 0.35f, 0.75f, true),
                GetOrCreateInterestPoint(parent, "Interest_Left_Bench_Path", "left_bench_path", new Vector3(-2.4f, groundY, -8.6f), new[] { "bench", "path", "quiet" }, 1.1f, 0.65f, 0.65f, true),
                GetOrCreateInterestPoint(parent, "Interest_Right_Front_Path", "right_front_path", new Vector3(2.4f, groundY, -8.6f), new[] { "path", "edge" }, 1.1f, 0.45f, 0.65f, true),
                GetOrCreateInterestPoint(parent, "Interest_SecondRing_West", "second_ring_west", new Vector3(-3.15f, groundY, 0.18f), new[] { "path", "plaza", "edge" }, 1.0f, 0.25f, 0.65f, true),
                GetOrCreateInterestPoint(parent, "Interest_SecondRing_East", "second_ring_east", new Vector3(3.15f, groundY, 0.18f), new[] { "path", "plaza", "edge" }, 1.0f, 0.25f, 0.65f, true),
                GetOrCreateInterestPoint(parent, "Interest_Quiet_North_Path", "quiet_north_path", new Vector3(0f, groundY, 3.35f), new[] { "quiet", "path", "shade" }, 0.85f, 0.8f, 0.65f, true)
            };
        }

        private static CatInterestPoint GetOrCreateInterestPoint(
            Transform parent,
            string objectName,
            string interestId,
            Vector3 position,
            string[] tags,
            float nonFocusWeight,
            float focusWeight,
            float sampleRadius,
            bool allowedInFocus)
        {
            Transform existing = parent.Find(objectName);
            GameObject pointObject;
            if (existing == null)
            {
                pointObject = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(pointObject, "Create cat interest point");
                pointObject.transform.SetParent(parent, false);
            }
            else
            {
                pointObject = existing.gameObject;
            }

            pointObject.transform.position = position;
            CatInterestPoint point = GetOrAdd<CatInterestPoint>(pointObject);
            point.Configure(interestId, tags, nonFocusWeight, focusWeight, sampleRadius, allowedInFocus);
            EditorUtility.SetDirty(pointObject);
            return point;
        }

        private static CatForbiddenZone[] BuildForbiddenZones(Transform parent, Transform navigationRoot, float groundY)
        {
            ClearChildren(parent);

            List<CatForbiddenZone> zones = new List<CatForbiddenZone>();
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            HashSet<string> usedNames = new HashSet<string>();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!CanCreateForbiddenZone(renderer, navigationRoot))
                {
                    continue;
                }

                Bounds sourceBounds;
                CatForbiddenZone.ZoneSourceKind sourceKind;
                if (TryGetColliderBounds(renderer.transform, out sourceBounds))
                {
                    sourceKind = CatForbiddenZone.ZoneSourceKind.Collider;
                }
                else
                {
                    sourceBounds = renderer.bounds;
                    sourceKind = IsComplexManualOverride(renderer, sourceBounds)
                        ? CatForbiddenZone.ZoneSourceKind.ManualOverride
                        : CatForbiddenZone.ZoneSourceKind.RendererBounds;
                }

                if (!IsUsableForbiddenBounds(sourceBounds))
                {
                    continue;
                }

                Vector3 zoneSize = new Vector3(
                    Mathf.Max(MinObstacleProjectionSize, sourceBounds.size.x * ForbiddenProjectionScale),
                    ForbiddenZoneHeight,
                    Mathf.Max(MinObstacleProjectionSize, sourceBounds.size.z * ForbiddenProjectionScale));
                Vector3 zoneCenter = new Vector3(sourceBounds.center.x, groundY, sourceBounds.center.z);

                string zoneName = MakeUniqueZoneName(renderer.name, sourceKind, usedNames);
                CatForbiddenZone zone = CreateForbiddenZone(
                    parent,
                    zoneName,
                    GetHierarchyPath(renderer.transform),
                    sourceKind,
                    zoneCenter,
                    zoneSize);
                zones.Add(zone);
            }

            AddCenterPawPlatformForbiddenZone(parent, groundY, usedNames, zones);
            return zones.ToArray();
        }

        private static void AddCenterPawPlatformForbiddenZone(
            Transform parent,
            float groundY,
            HashSet<string> usedNames,
            List<CatForbiddenZone> zones)
        {
            Renderer sourceRenderer = FindRendererByName("Mesh_0.010");
            Bounds bounds;
            string sourceName;
            if (sourceRenderer != null)
            {
                bounds = sourceRenderer.bounds;
                sourceName = GetHierarchyPath(sourceRenderer.transform);
            }
            else
            {
                bounds = new Bounds(new Vector3(-0.12f, groundY, 0.19f), new Vector3(4.82f, 0.24f, 4.86f));
                sourceName = "ManualFallback/CenterPawPlatform";
            }

            Vector3 zoneSize = new Vector3(
                Mathf.Max(MinObstacleProjectionSize, bounds.size.x * ForbiddenProjectionScale),
                ForbiddenZoneHeight,
                Mathf.Max(MinObstacleProjectionSize, bounds.size.z * ForbiddenProjectionScale));
            Vector3 zoneCenter = new Vector3(bounds.center.x, groundY, bounds.center.z);
            string zoneName = MakeUniqueZoneName(
                "CenterPawPlatform",
                CatForbiddenZone.ZoneSourceKind.ManualOverride,
                usedNames);

            zones.Add(CreateForbiddenZone(
                parent,
                zoneName,
                sourceName,
                CatForbiddenZone.ZoneSourceKind.ManualOverride,
                zoneCenter,
                zoneSize));
        }

        private static Renderer FindRendererByName(string name)
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].name == name)
                {
                    return renderers[i];
                }
            }

            return null;
        }

        private static CatForbiddenZone CreateForbiddenZone(
            Transform parent,
            string zoneName,
            string sourceName,
            CatForbiddenZone.ZoneSourceKind sourceKind,
            Vector3 zoneCenter,
            Vector3 zoneSize)
        {
            GameObject zoneObject = new GameObject(zoneName);
            Undo.RegisterCreatedObjectUndo(zoneObject, "Create cat forbidden navigation zone");
            zoneObject.transform.SetParent(parent, false);

            CatForbiddenZone zone = zoneObject.AddComponent<CatForbiddenZone>();
            zone.Configure(sourceName, sourceKind, ForbiddenProjectionScale, zoneCenter, zoneSize);
            BoxCollider collider = zoneObject.GetComponent<BoxCollider>();
            collider.isTrigger = false;

            NavMeshModifier modifier = zoneObject.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = GetNotWalkableArea();
            modifier.ignoreFromBuild = false;

            EditorUtility.SetDirty(zoneObject);
            return zone;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static bool CanCreateForbiddenZone(Renderer renderer, Transform navigationRoot)
        {
            if (renderer == null || renderer.GetComponentInParent<Canvas>() != null)
            {
                return false;
            }

            Transform transform = renderer.transform;
            if (IsChildOf(transform, navigationRoot) || transform.GetComponentInParent<CatForbiddenZone>() != null)
            {
                return false;
            }

            if (transform.GetComponentInParent<CatBehaviorDriver>() != null || transform.GetComponentInParent<Camera>() != null)
            {
                return false;
            }

            string path = GetHierarchyPath(transform).ToLowerInvariant();
            if (path.Contains("sky") || path.Contains("backdrop") || path.Contains("catcompanionmodel") ||
                path.Contains("catwalkablearea") || path.Contains("grasspuffs") || path.Contains("tanstones") ||
                path.Contains("colorflowers") || path.Contains("island") || path.Contains("ground"))
            {
                return false;
            }

            Bounds bounds = renderer.bounds;
            return IsUsableForbiddenBounds(bounds);
        }

        private static bool IsUsableForbiddenBounds(Bounds bounds)
        {
            if (bounds.size.x < MinObstacleProjectionSize || bounds.size.z < MinObstacleProjectionSize)
            {
                return false;
            }

            if (bounds.size.y < MinObstacleHeight)
            {
                return false;
            }

            float projectedArea = bounds.size.x * bounds.size.z;
            if (projectedArea > MaxAutomaticObstacleProjectionArea)
            {
                return false;
            }

            if (projectedArea > 250f && bounds.size.y < 1.5f)
            {
                return false;
            }

            return true;
        }

        private static bool TryGetColliderBounds(Transform source, out Bounds bounds)
        {
            bounds = default(Bounds);
            Collider[] colliders = source.GetComponentsInChildren<Collider>(false);
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider.isTrigger || collider.GetComponentInParent<CatForbiddenZone>() != null)
                {
                    continue;
                }

                if (!IsUsableForbiddenBounds(collider.bounds))
                {
                    continue;
                }

                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return found;
        }

        private static bool IsComplexManualOverride(Renderer renderer, Bounds bounds)
        {
            float projectedArea = bounds.size.x * bounds.size.z;
            if (projectedArea >= ComplexManualAreaThreshold || bounds.size.y >= 5f)
            {
                return true;
            }

            string name = renderer.name.ToLowerInvariant();
            return name.Contains("house") || name.Contains("building") || name.Contains("shop") ||
                name.Contains("cafe") || name.Contains("cat") || name.Contains("tree");
        }

        private static int GetNotWalkableArea()
        {
            int area = NavMesh.GetAreaFromName("Not Walkable");
            return area >= 0 ? area : 1;
        }

        private static bool IsChildOf(Transform child, Transform parent)
        {
            Transform current = child;
            while (current != null)
            {
                if (current == parent)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static string MakeUniqueZoneName(
            string sourceName,
            CatForbiddenZone.ZoneSourceKind sourceKind,
            HashSet<string> usedNames)
        {
            string safeName = string.IsNullOrEmpty(sourceName) ? "Unnamed" : sourceName;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
            {
                safeName = safeName.Replace(invalidChars[i], '_');
            }

            string prefix = sourceKind == CatForbiddenZone.ZoneSourceKind.ManualOverride
                ? "CatForbiddenManual_"
                : sourceKind == CatForbiddenZone.ZoneSourceKind.Collider
                    ? "CatForbiddenCollider_"
                    : "CatForbiddenRenderer_";
            string candidate = prefix + safeName;
            string unique = candidate;
            int suffix = 1;
            while (usedNames.Contains(unique))
            {
                unique = candidate + "_" + suffix;
                suffix += 1;
            }

            usedNames.Add(unique);
            return unique;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            component = target.AddComponent<T>();
            EditorUtility.SetDirty(target);
            return component;
        }

        private static void AssignDriver(
            CatBehaviorDriver driver,
            MockRecognitionProvider recognitionProvider,
            MockCatLLMClient llmClient,
            CatNavigationAgent navigationAgent,
            CatAnimationController animationController,
            CatDestinationPlanner destinationPlanner,
            CatActionRouter actionRouter,
            RealtimeFeatureEngine featureEngine,
            CatNeedModel needModel,
            CatBehaviorMemory behaviorMemory,
            CatBehaviorBrainScorer behaviorScorer)
        {
            SerializedObject serialized = new SerializedObject(driver);
            serialized.FindProperty("recognitionProviderComponent").objectReferenceValue = recognitionProvider;
            serialized.FindProperty("llmClientComponent").objectReferenceValue = llmClient;
            serialized.FindProperty("navigationAgent").objectReferenceValue = navigationAgent;
            serialized.FindProperty("animationController").objectReferenceValue = animationController;
            serialized.FindProperty("destinationPlanner").objectReferenceValue = destinationPlanner;
            serialized.FindProperty("actionRouter").objectReferenceValue = actionRouter;
            serialized.FindProperty("featureEngine").objectReferenceValue = featureEngine;
            serialized.FindProperty("needModel").objectReferenceValue = needModel;
            serialized.FindProperty("behaviorMemory").objectReferenceValue = behaviorMemory;
            serialized.FindProperty("behaviorScorer").objectReferenceValue = behaviorScorer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
        }

        private static void AssignPlanner(
            CatDestinationPlanner planner,
            Transform[] anchors,
            CatForbiddenZone[] forbiddenZones,
            CatInterestPointRegistry interestPointRegistry,
            CatNeedModel needModel,
            CatBehaviorMemory behaviorMemory)
        {
            SerializedObject serialized = new SerializedObject(planner);
            serialized.FindProperty("userAnchor").objectReferenceValue = Camera.main != null ? Camera.main.transform : null;
            serialized.FindProperty("interestPointRegistry").objectReferenceValue = interestPointRegistry;
            serialized.FindProperty("needModel").objectReferenceValue = needModel;
            serialized.FindProperty("behaviorMemory").objectReferenceValue = behaviorMemory;
            serialized.FindProperty("nonFocusSampleRadius").floatValue = 8.5f;
            serialized.FindProperty("focusSampleRadius").floatValue = 3.5f;
            serialized.FindProperty("minMoveDistance").floatValue = 1.1f;
            serialized.FindProperty("minDistanceFromUserAnchorWhenFocused").floatValue = 2.5f;
            serialized.FindProperty("sampleAttempts").intValue = 24;
            serialized.FindProperty("blockerMask").intValue = 0;
            serialized.FindProperty("blockerCheckRadius").floatValue = 0.26f;
            serialized.FindProperty("navMeshProbeDistance").floatValue = 1.8f;
            serialized.FindProperty("forbiddenPathSampleStep").floatValue = 0.2f;

            SerializedProperty anchorProperty = serialized.FindProperty("anchors");
            anchorProperty.arraySize = anchors.Length;
            for (int i = 0; i < anchors.Length; i++)
            {
                SerializedProperty element = anchorProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("point").objectReferenceValue = anchors[i];
                element.FindPropertyRelative("nonFocusWeight").floatValue = 1f;
                element.FindPropertyRelative("focusWeight").floatValue = i <= 1 ? 1f : 0.25f;
            }

            SerializedProperty forbiddenProperty = serialized.FindProperty("forbiddenZones");
            forbiddenProperty.arraySize = forbiddenZones != null ? forbiddenZones.Length : 0;
            for (int i = 0; i < forbiddenProperty.arraySize; i++)
            {
                forbiddenProperty.GetArrayElementAtIndex(i).objectReferenceValue = forbiddenZones[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(planner);
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            if (target == null)
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"CatLife second batch setup could not assign {propertyName} on {target.name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
