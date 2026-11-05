using System;
using System.IO;
using CatLife.LLM;
using CatLife.Mobile;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatLife.Editor
{
    // Export the actual application's prompt and local insight, never credentials.
    public static class CatLifeInterviewEvaluation
    {
        public static void RestoreIslandMaterialBindings()
        {
            const string sourcePath = "Assets/MobileRuntime/Art/Town/Source/CL_TWN_Runtime.fbx";
            const string prefabPath = "Assets/MobileRuntime/Art/Town/PF_CL_TWN_Town.prefab";
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            MeshRenderer island = Array.Find(source.GetComponentsInChildren<MeshRenderer>(true),
                renderer => renderer.name == "CL_ENV_IslandBase_01");
            var mesh = island.GetComponent<MeshFilter>().sharedMesh;
            if (mesh.subMeshCount != 3 || island.sharedMaterials.Length != 3)
                throw new InvalidOperationException("Restored source must contain top, side and bottom submeshes.");
            var materials = Array.ConvertAll(island.sharedMaterials, CatLifeMobileSceneBuilder.ResolveMaterial);
            AssetDatabase.SaveAssets();
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name == island.name) renderer.sharedMaterials = materials;
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefab);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (renderer.name == island.name)
                {
                    renderer.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            EditorSceneManager.SaveScene(scene);
            InspectIslandMaterialSlots();
            CaptureIslandAudit();
        }

        public static void InspectIslandMaterialSlots()
        {
            foreach (string path in new[] {
                "Assets/MobileRuntime/Art/Town/Source/CL_TWN_Runtime.fbx",
                "Assets/MobileRuntime/Art/Town/PF_CL_TWN_Town.prefab" })
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.name == "CL_ENV_IslandBase_01") ReportIslandSlots(path, renderer);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (renderer.name == "CL_ENV_IslandBase_01") ReportIslandSlots("scene", renderer);
        }

        private static void ReportIslandSlots(string source, MeshRenderer renderer)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            string materials = string.Join(",", Array.ConvertAll(renderer.sharedMaterials,
                material => material == null ? "null" : material.name + "@" + AssetDatabase.GetAssetPath(material)));
            string counts = "";
            for (int i = 0; i < mesh.subMeshCount; i++) counts += i + ":" + mesh.GetIndexCount(i) + ";";
            Debug.Log("CATLIFE_ISLAND_SLOTS source=" + source + " mesh=" + mesh.name
                + " submeshes=" + mesh.subMeshCount + " indices=" + counts + " materials=" + materials);
        }

        public static void CaptureIslandAudit()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
            var meshes = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            bool first = true; Bounds bounds = default;
            foreach (var mesh in meshes)
            {
                if (mesh.gameObject.scene != scene || !mesh.gameObject.activeInHierarchy) continue;
                if (mesh.name.Contains("Island"))
                {
                    string materials = string.Join(",", Array.ConvertAll(mesh.sharedMaterials,
                        material => material.name + "@" + AssetDatabase.GetAssetPath(material)));
                    Debug.Log("CATLIFE_ISLAND_OBJECT " + mesh.name + " bounds=" + mesh.bounds + " materials=" + materials);
                }
                if (first) { bounds = mesh.bounds; first = false; } else bounds.Encapsulate(mesh.bounds);
            }
            var cameraObject = new GameObject("Interview island audit camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.40f,.65f,.78f);
            var target = new RenderTexture(1200,900,24); camera.targetTexture = target; camera.aspect = 4f/3f;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/interview-demo-20261008/editor"));
            string[] args = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(args, "-auditPath");
            if (outputIndex >= 0) folder = Path.GetFullPath(args[outputIndex + 1]);
            Directory.CreateDirectory(folder);
            var previous = RenderTexture.active;
            for (int view = 0; view < 2; view++)
            {
                Vector3 direction = view == 0 ? new Vector3(0,1,-1.6f) : new Vector3(0,-.35f,-1);
                camera.transform.position = bounds.center + direction.normalized * bounds.size.magnitude * 2;
                camera.transform.LookAt(bounds.center);
                float size = 0;
                for (int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
                {
                    Vector3 corner = Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                    size = Mathf.Max(size,Mathf.Abs(Vector3.Dot(corner,camera.transform.up)),Mathf.Abs(Vector3.Dot(corner,camera.transform.right))/camera.aspect);
                }
                camera.orthographicSize = size * 1.1f; camera.farClipPlane = bounds.size.magnitude * 5;
                camera.Render(); RenderTexture.active = target;
                var image = new Texture2D(1200,900,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1200,900),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder,view==0?"island-overview.png":"island-bottom.png"),image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            RenderTexture.active = previous; camera.targetTexture = null; target.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
            Debug.Log("CATLIFE_ISLAND_AUDIT rendered overview/bottom without saving or changing the source scene.");
        }
        [Serializable] private sealed class Case
        {
            public string id, label, localTemplate, model, request;
            public bool synthetic = true;
            public bool behaviorStatsDisabled;
            public int seconds, touches, backgrounds, stability;
        }
        public static void ExportAiCases()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/interview-demo-20261008/ai-evaluation/cases"));
            string[] args = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(args, "-evaluationPath");
            if (outputIndex >= 0) folder = Path.Combine(Path.GetFullPath(args[outputIndex + 1]), "cases");
            Directory.CreateDirectory(folder);
            int[,] values = { {60,0,0,100}, {900,0,0,100}, {1500,0,0,100}, {2700,0,0,100},
                {1500,4,0,100}, {1500,10,1,70}, {1500,40,3,30}, {900,0,8,20},
                {10800,0,0,100}, {60,10,1,0}, {1500,0,0,100}, {1500,0,0,0} };
            string[] labels = { "评审短会话", "15分钟安静", "25分钟安静", "45分钟安静", "高触控但长稳定段", "触控和后台", "高触控低稳定", "多次后台", "180分钟上限", "短会话稳定度零", "统计关闭未采集合成", "已采集但最长稳定段为零" };
            string model = GenericCloudConfig.Load().model;
            for (int i = 0; i < labels.Length; i++)
            {
                int seconds = values[i,0], touches = values[i,1], backgrounds = values[i,2], stability = values[i,3];
                var record = new CatLifeSessionRecord { completed = true, actualSeconds = seconds, plannedSeconds = seconds,
                    behaviorStatsDisabled = i == 10,
                    touchCount = touches, backgroundCount = backgrounds, longestStableSeconds = seconds * stability / 100 };
                var context = new CatPromptContext { behaviorStatsDisabled = record.behaviorStatsDisabled, focusSessionActive = false, focusState = "Reward", focusScore01 = record.StabilityPercent / 100f,
                    distraction01 = Mathf.Clamp01(touches / 10f), userIntent = "PostSession", privacyModeEnabled = true,
                    safeLocalSummary = record.behaviorStatsDisabled ? $"时长={seconds}s;行为统计未采集" : $"时长={seconds}s;触控={touches};后台={backgrounds};稳定={record.StabilityPercent}%" };
                var item = new Case { id = $"A{i+1:D2}", label = labels[i], seconds = seconds, touches = touches,
                    backgrounds = backgrounds, stability = record.StabilityPercent, model = model,
                    behaviorStatsDisabled = record.behaviorStatsDisabled,
                    localTemplate = CatLifeInsightEngine.Create(record), request = MockCatLLMClient.BuildGenericCloudRequestJson(model, context) };
                File.WriteAllText(Path.Combine(folder, item.id + ".json"), JsonUtility.ToJson(item, true));
            }
            Debug.Log("CATLIFE_AI_EVAL exported 12 synthetic cases using current prompt and local insight; no requests sent.");
        }
    }
}
