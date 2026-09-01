using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CatLifeMobilePreviewRenderer
{
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

    public static void RenderBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
        Camera camera = Camera.main;
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
