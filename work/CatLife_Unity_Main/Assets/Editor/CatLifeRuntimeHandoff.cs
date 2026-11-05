using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class CatLifeRuntimeHandoff
{
    [Serializable] public sealed class Entry { public string path; public long bytes; public string kind; }
    [Serializable] public sealed class Inventory { public string unityVersion; public string[] scenes; public Entry[] files; public string publicRights = "unconfirmed-local-only"; }
    public static void ExportInventory()
    {
        string[] scenes = { "Assets/Scenes/CatLifeMobile.unity",
            "Assets/Editor/CatProneReview/NavigationTurnCandidate/CatLifeNavigationTurnReview.unity" };
        var paths = new HashSet<string>(AssetDatabase.GetDependencies(scenes, true).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)));
        // Render pipelines, renderers and global volume settings are not scene dependencies.
        var globalAssets = new HashSet<string>();
        foreach (string file in Directory.GetFiles("ProjectSettings", "*.asset"))
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"guid: ([0-9a-f]{32})"))
            {
                string path = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
                if (path.StartsWith("Assets/", StringComparison.Ordinal)) globalAssets.Add(path);
            }
        paths.UnionWith(AssetDatabase.GetDependencies(globalAssets.ToArray(), true).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)));
        foreach (string p in Directory.GetFiles("Assets", "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')))
        {
            if (p.Contains("CatLifePrivate") || p.Contains("generic_cloud_credentials")) continue;
            if (p.EndsWith(".cs") || p.EndsWith(".asmdef") || p.StartsWith("Assets/Plugins/Android/") || p.StartsWith("Assets/Resources/")) paths.Add(p);
        }
        paths.RemoveWhere(p => p.Contains("CatLifePrivate") || p.Contains("generic_cloud_credentials"));
        foreach (string p in paths.ToArray())
        {
            if (File.Exists(p + ".meta")) paths.Add(p + ".meta");
            string parent = Path.GetDirectoryName(p)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(parent) && parent != "Assets")
            {
                if (File.Exists(parent + ".meta")) paths.Add(parent + ".meta");
                parent = Path.GetDirectoryName(parent)?.Replace('\\', '/');
            }
        }
        foreach (string folder in new[] { "ProjectSettings", "Packages" })
            foreach (string p in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) paths.Add(p.Replace('\\', '/'));
        var report = new Inventory { unityVersion = Application.unityVersion, scenes = scenes,
            files = paths.Where(File.Exists).OrderBy(p => p).Select(p => new Entry { path = p, bytes = new FileInfo(p).Length,
                kind = IsSource(p) ? "source" : "runtime-asset" }).ToArray() };
        string output = Path.GetFullPath("../../Reports/repair-20261105/runtime-handoff.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Debug.Log("CATLIFE_HANDOFF files=" + report.files.Length + " bytes=" + report.files.Sum(e => e.bytes) + " rights=local-only");
    }
    public static bool IsSource(string p)
    {
        string value = p.EndsWith(".meta") ? p.Substring(0, p.Length - 5) : p;
        return value.EndsWith(".cs") || value.EndsWith(".asmdef") || value.StartsWith("ProjectSettings/") || value.StartsWith("Packages/") || value.StartsWith("Assets/Plugins/");
    }
}
