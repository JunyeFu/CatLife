using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatLife.Editor
{
    public static class CatLifeTownAtlasReview
    {
        public const string ScenePath = "Assets/Editor/CatProneReview/Atlas512RepackedCandidate/CatLifeTownAtlasReview.unity";
        private const string Folder = "Assets/Editor/CatProneReview/Atlas512RepackedCandidate";

        public static void PrepareScene()
        {
            EditorSceneManager.OpenScene("Assets/Editor/CatProneReview/ExitCandidate/CatLifeExitCandidate.unity");
            GameObject town = GameObject.Find("CatLifeMobileTown");
            var originalMaterials = town.GetComponentsInChildren<MeshRenderer>().SelectMany(renderer => renderer.sharedMaterials)
                .Distinct().ToDictionary(material => material.name);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CL_TWN_Runtime_atlas512_candidate.fbx");
            var meshes = model.GetComponentsInChildren<MeshFilter>().ToDictionary(filter => filter.name);
            // Isolate the review scene from the production town prefab and its old atlas dependencies.
            if (PrefabUtility.IsPartOfPrefabInstance(town))
                PrefabUtility.UnpackPrefabInstance(town, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            int colliders = 0;
            foreach (MeshFilter filter in town.GetComponentsInChildren<MeshFilter>())
            {
                MeshFilter replacement = meshes[filter.name];
                MeshCollider collider = filter.GetComponent<MeshCollider>();
                if (collider != null)
                {
                    if (collider.sharedMesh != filter.sharedMesh) throw new InvalidOperationException("Separate collision geometry requires review: " + filter.name);
                    collider.sharedMesh = replacement.sharedMesh;
                    colliders++;
                }
                filter.sharedMesh = replacement.sharedMesh;
                filter.GetComponent<MeshRenderer>().sharedMaterials = replacement.GetComponent<MeshRenderer>().sharedMaterials.Select(material =>
                    material.name.StartsWith("MAT_CL_TownAtlasMobile_", StringComparison.Ordinal)
                        ? AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + material.name + ".mat")
                        : originalMaterials[material.name]).ToArray();
            }
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            string validation = CatLifeMobileBuildValidator.ValidateSceneReport(ScenePath);
            if (!validation.StartsWith("PASS", StringComparison.Ordinal)) throw new InvalidOperationException(validation);
            string[] dependencies = AssetDatabase.GetDependencies(ScenePath, true).OrderBy(path => path).ToArray();
            if (dependencies.Contains("Assets/MobileRuntime/Art/Town/Source/CL_TWN_Runtime.fbx") ||
                dependencies.Any(path => Path.GetFileName(path).StartsWith("T_CL_TownAtlas_BaseColor_", StringComparison.Ordinal)))
                throw new InvalidOperationException("Original town FBX/atlas still enters the isolated scene dependency tree.");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Reports", "wp06-android-atlas-r23-20261017"));
            Directory.CreateDirectory(directory);
            var report = new StringBuilder(validation + "\nScene=" + ScenePath + "\nRebound walkable colliders=" + colliders + "\n");
            foreach (string path in dependencies.Where(path => Path.GetFileName(path).StartsWith("T_CL_TownAtlas_Mobile_", StringComparison.Ordinal)))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var android = importer.GetPlatformTextureSettings("Android");
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture.width != 512 || texture.height != 512 || importer.maxTextureSize != 512 || android.maxTextureSize != 512)
                    throw new InvalidOperationException("Ordinary atlas exceeds the 512 budget: " + path);
                report.AppendLine($"Atlas={path}; size={texture.width}x{texture.height}; Android={android.format}");
            }
            File.WriteAllText(Path.Combine(directory, "candidate-scene.txt"), report.ToString());
            File.WriteAllLines(Path.Combine(directory, "scene-dependencies.txt"), dependencies);
            Debug.Log("CATLIFE_TOWN_ATLAS_SCENE=" + ScenePath + " | " + validation + " colliders=" + colliders);
        }
    }
}
