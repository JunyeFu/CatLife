using CatLife.Cat;
using CatLife.LLM;
using CatLife.Recognition;
using CatLife.UI;
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
        private const string CatName = "CatCompanionModel";

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

            float groundY = cat.transform.position.y;
            CreateWalkArea(navigationRoot, "CatWalkableArea_MainPlaza", new Vector3(0f, groundY - 0.06f, -6.8f), new Vector3(7.4f, 0.12f, 4.5f));
            CreateWalkArea(navigationRoot, "CatWalkableArea_LeftGardenPath", new Vector3(-3.9f, groundY - 0.06f, -6.3f), new Vector3(2.8f, 0.12f, 3.6f));
            CreateWalkArea(navigationRoot, "CatWalkableArea_RightGardenPath", new Vector3(3.9f, groundY - 0.06f, -6.3f), new Vector3(2.8f, 0.12f, 3.6f));
            CreateWalkArea(navigationRoot, "CatWalkableArea_FrontStoneRing", new Vector3(0f, groundY - 0.06f, -9.25f), new Vector3(6.6f, 0.12f, 2.2f));

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
            CatDestinationPlanner destinationPlanner = GetOrAdd<CatDestinationPlanner>(cat);
            CatAnimationController animationController = GetOrAdd<CatAnimationController>(cat);
            CatActionRouter actionRouter = GetOrAdd<CatActionRouter>(cat);
            CatBehaviorDriver behaviorDriver = GetOrAdd<CatBehaviorDriver>(cat);
            Animator animator = cat.GetComponent<Animator>();

            AssignObject(navigationAgent, "agent", agent);
            AssignPlanner(destinationPlanner, anchors);
            AssignObject(animationController, "animator", animator);
            AssignDriver(behaviorDriver, recognitionProvider, llmClient, navigationAgent, animationController, destinationPlanner, actionRouter, featureEngine);

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
            EditorUtility.SetDirty(systemsRoot.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("CatLife second batch setup completed: NavMeshSurface, walk areas, cat NavMeshAgent, behavior driver, recognition mock, LLM mock, and UI binding are installed.");
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
            RealtimeFeatureEngine featureEngine)
        {
            SerializedObject serialized = new SerializedObject(driver);
            serialized.FindProperty("recognitionProviderComponent").objectReferenceValue = recognitionProvider;
            serialized.FindProperty("llmClientComponent").objectReferenceValue = llmClient;
            serialized.FindProperty("navigationAgent").objectReferenceValue = navigationAgent;
            serialized.FindProperty("animationController").objectReferenceValue = animationController;
            serialized.FindProperty("destinationPlanner").objectReferenceValue = destinationPlanner;
            serialized.FindProperty("actionRouter").objectReferenceValue = actionRouter;
            serialized.FindProperty("featureEngine").objectReferenceValue = featureEngine;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
        }

        private static void AssignPlanner(CatDestinationPlanner planner, Transform[] anchors)
        {
            SerializedObject serialized = new SerializedObject(planner);
            serialized.FindProperty("userAnchor").objectReferenceValue = Camera.main != null ? Camera.main.transform : null;
            serialized.FindProperty("nonFocusSampleRadius").floatValue = 8.5f;
            serialized.FindProperty("focusSampleRadius").floatValue = 3.5f;
            serialized.FindProperty("minMoveDistance").floatValue = 1.1f;
            serialized.FindProperty("minDistanceFromUserAnchorWhenFocused").floatValue = 2.5f;
            serialized.FindProperty("sampleAttempts").intValue = 24;
            serialized.FindProperty("blockerMask").intValue = 0;
            serialized.FindProperty("blockerCheckRadius").floatValue = 0.26f;
            serialized.FindProperty("navMeshProbeDistance").floatValue = 1.8f;

            SerializedProperty anchorProperty = serialized.FindProperty("anchors");
            anchorProperty.arraySize = anchors.Length;
            for (int i = 0; i < anchors.Length; i++)
            {
                SerializedProperty element = anchorProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("point").objectReferenceValue = anchors[i];
                element.FindPropertyRelative("nonFocusWeight").floatValue = 1f;
                element.FindPropertyRelative("focusWeight").floatValue = i <= 1 ? 1f : 0.25f;
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
