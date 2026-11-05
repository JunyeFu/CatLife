using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatLife.Cat;
using CatLife.Mobile;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatLife.Editor
{
    public static class CatLifeMobileBuildValidator
    {
        private const string ScenePath = "Assets/Scenes/CatLifeMobile.unity";
        private static readonly string[] RequiredTownObjects =
        {
            "CL_ENV_IslandBase_01",
            "CL_BLD_FocusHouse_01",
            "CL_BLD_CatHouse_01",
            "CL_BLD_FishShop_01",
            "CL_BLD_TownGate_01",
            "CL_BLD_TomatoClockTower_01",
            "CL_ENV_RewardTree_01",
            "CL_ROAD_PlazaRing_01",
            "CL_ROAD_PlazaPath_01",
            "CL_PROP_CenterStone_01"
        };

        private static readonly string[] RequiredWalkableColliders =
        {
            "CL_ENV_IslandBase_01",
            "CL_ENV_IslandGrass_01",
            "CL_ROAD_CentralGardenRing_01",
            "CL_ROAD_PlazaPath_01",
            "CL_ROAD_PlazaRing_01",
            "CL_PROP_CenterStone_01"
        };

        public static string ValidateReport()
        {
            string report = ValidateSceneReport(ScenePath);
            if (EditorBuildSettings.scenes.Count(scene => scene.enabled) != 1 || !EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == ScenePath))
                return "FAIL Only CatLifeMobile scene must be enabled for Android build. | " + report;
            return report;
        }

        public static string ValidateSceneReport(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            List<string> issues = new List<string>();
            if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null) issues.Add("Global render pipeline asset missing from the restored project.");
            if (UnityEngine.Object.FindFirstObjectByType<CatLifeMobileApp>() == null) issues.Add("CatLifeMobileApp missing.");
            CatLifeCameraDirector director = UnityEngine.Object.FindFirstObjectByType<CatLifeCameraDirector>();
            if (director == null) issues.Add("CatLifeCameraDirector missing.");
            else
            {
                CatLifeCameraDirector.Preset home = director.GetPreset(CatLifeSessionPhase.Normal);
                if (Vector3.Distance(home.Position, new Vector3(0f, 2.2f, 9f)) > .001f ||
                    Vector3.Distance(home.Euler, new Vector3(12f, 180f, 0f)) > .001f ||
                    Mathf.Abs(home.Fov - 74f) > .001f)
                    issues.Add("Approved hero Home camera preset missing.");
            }
            GameObject mobileView = GameObject.Find("CatLifeMobileView");
            if (mobileView == null || PrefabUtility.GetPrefabInstanceStatus(mobileView) != PrefabInstanceStatus.Connected) issues.Add("Serialized mobile UI prefab instance missing.");
            else foreach (string layer in new[] { "HomeHudLayer", "SessionLayer", "PageLayer", "TransientLayer", "SaveRecoveryPanel", "RecoveryRetry", "ClockRecoveryPanel", "GrowthNameText" }) if (FindDescendant(mobileView.transform, layer) == null) issues.Add("UI layer/binding missing: " + layer);

            GameObject cloudLayer = GameObject.Find("CL_ENV_CloudLayer_01");
            if (cloudLayer == null || cloudLayer.GetComponentsInChildren<SpriteRenderer>(true).Length != 3) issues.Add("Three-cloud sky layer missing.");

            GameObject town = GameObject.Find("CatLifeMobileTown");
            int triangles = 0;
            int materials = 0;
            int identityCount = 0;
            if (town == null)
            {
                issues.Add("CatLifeMobileTown missing.");
            }
            else
            {
                MeshFilter[] filters = town.GetComponentsInChildren<MeshFilter>(true);
                MeshRenderer[] renderers = town.GetComponentsInChildren<MeshRenderer>(true);
                triangles = filters.Where(filter => filter.sharedMesh != null).Sum(filter => filter.sharedMesh.triangles.Length / 3);
                materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().Count();
                if (triangles < 150000 || triangles > 300000) issues.Add("Town triangles must be 150000-300000: " + triangles);
                if (materials > 20) issues.Add("Town materials exceed 20: " + materials);

                foreach (string requiredName in RequiredTownObjects)
                {
                    if (FindDescendant(town.transform, requiredName) == null) issues.Add("Required town object missing: " + requiredName);
                }

                foreach (string surfaceName in RequiredWalkableColliders)
                {
                    Transform surface = FindDescendant(town.transform, surfaceName);
                    MeshCollider collider = surface == null ? null : surface.GetComponent<MeshCollider>();
                    if (collider == null || collider.isTrigger || collider.convex)
                        issues.Add("Walkable surface requires a static non-convex MeshCollider: " + surfaceName);
                }

                Transform island = FindDescendant(town.transform, "CL_ENV_IslandBase_01");
                Renderer islandRenderer = island == null ? null : island.GetComponent<Renderer>();
                if (islandRenderer == null)
                {
                    issues.Add("Complete island renderer missing.");
                }
                else
                {
                    Vector3 size = islandRenderer.bounds.size;
                    if (size.x < 35f || size.z < 28f || size.y < 1.5f) issues.Add("Island bounds are incomplete: " + size);
                }

                string[] anonymousNames = renderers.Select(renderer => renderer.name)
                    .Where(name => name.StartsWith("node_", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("Mesh_", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("texture_pbr_", StringComparison.OrdinalIgnoreCase))
                    .Distinct().ToArray();
                if (anonymousNames.Length > 0) issues.Add("Anonymous runtime names remain: " + string.Join(", ", anonymousNames));

                CatLifeArtAssetIdentity[] identities = town.GetComponentsInChildren<CatLifeArtAssetIdentity>(true);
                identityCount = identities.Length;
                if (identityCount != renderers.Length) issues.Add($"Asset identity coverage mismatch: identities={identityCount}, renderers={renderers.Length}");
                if (identities.Any(identity => string.IsNullOrWhiteSpace(identity.assetId))) issues.Add("Empty runtime asset ID found.");
                if (identities.Select(identity => identity.assetId).Distinct(StringComparer.Ordinal).Count() != identities.Length) issues.Add("Duplicate runtime asset ID found.");
            }

            GameObject cat = GameObject.Find("CatLifeMobileCat");
            if (cat == null)
            {
                issues.Add("CatLifeMobileCat missing.");
            }
            else
            {
                SkinnedMeshRenderer[] renderers = cat.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length != 1) issues.Add("Cat requires one skinned mesh, found " + renderers.Length);
                else
                {
                    if (renderers[0].sharedMaterials.Length != 1) issues.Add("Cat requires one material.");
                    Mesh catMesh = renderers[0].sharedMesh;
                    if (catMesh == null || catMesh.GetBlendShapeIndex(CatCrouchShapeDriver.ShapeName) < 0)
                        issues.Add("Cat crouch silhouette blend shape missing.");
                }
                Animator animator = cat.GetComponentInChildren<Animator>();
                if (animator == null || animator.runtimeAnimatorController == null) issues.Add("Cat Animator controller missing.");
                else foreach (string state in new[] { "CL_CAT_StandIdle_v01_loop_96f", "CL_CAT_Walk_v01_loop_60f", "CL_CAT_SitIdle_v01_loop_96f", "CL_CAT_StandToLie_v11_150f", "CL_CAT_ProneBreath_v11_loop_240f", "CL_CAT_ProneAttention_v11_48f", "CL_CAT_LieToStand_v11_120f" })
                    if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state))) issues.Add("Cat Animator state missing: " + state);

                CapsuleCollider bodyCollider = cat.GetComponent<CapsuleCollider>();
                if (bodyCollider == null || bodyCollider.isTrigger) issues.Add("Cat physics CapsuleCollider missing.");
                Rigidbody body = cat.GetComponent<Rigidbody>();
                if (body == null || !body.useGravity || body.isKinematic ||
                    (body.constraints & RigidbodyConstraints.FreezeRotation) != RigidbodyConstraints.FreezeRotation)
                    issues.Add("Cat dynamic gravity Rigidbody is not configured.");
            }

            ValidateDependencies(issues, scenePath);

            return issues.Count == 0
                ? $"PASS CatLife mobile build validation: triangles={triangles}, materials={materials}, asset_ids={identityCount}, complete island and required landmarks present."
                : "FAIL " + string.Join(" | ", issues);
        }

        public static void ValidateBatch()
        {
            string report = ValidateReport();
            if (report.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.Exit(report.StartsWith("PASS", StringComparison.Ordinal) ? 0 : 1);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
        }

        private static void ValidateDependencies(List<string> issues, string scenePath)
        {
            foreach (string dependency in AssetDatabase.GetDependencies(scenePath, true))
            {
                string extension = Path.GetExtension(dependency);
                if (extension.Equals(".blend", StringComparison.OrdinalIgnoreCase) || extension.Equals(".glb", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add("Forbidden source asset in scene dependency tree: " + dependency);
                }
                if (dependency.StartsWith("Assets/Art/Town/Source/", StringComparison.Ordinal))
                {
                    issues.Add("Legacy town source remains in scene dependency tree: " + dependency);
                }

                TextureImporter textureImporter = dependency.StartsWith("Assets/", StringComparison.Ordinal) ? AssetImporter.GetAtPath(dependency) as TextureImporter : null;
                if (textureImporter != null && textureImporter.maxTextureSize > 1024)
                {
                    issues.Add("Runtime texture exceeds 1024 import limit: " + dependency);
                }
            }
        }
    }
}
