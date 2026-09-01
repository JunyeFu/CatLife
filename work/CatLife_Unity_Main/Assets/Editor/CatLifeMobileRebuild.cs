using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatLife.Editor
{
    public static class CatLifeMobileRebuild
    {
        private const string TownSourcePath = "Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260630.fbx";
        private const string TownGlbSourcePath = "Assets/Art/Town/Source/catlife_v2_island_grass_style_no_skybox_20260702_1.glb";
        private const string CatSourcePath = "Assets/Art/Cat/Animations/CatLife_cat_10_actions_final_state.fbx";

        public static void ProbeSourceAssetsBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reports", "MobileRebuild", "source-probe"));
            }

            Directory.CreateDirectory(reportDirectory);
            WriteTownReport(TownSourcePath, Path.Combine(reportDirectory, "town_meshes.csv"));
            WriteTownReport(TownGlbSourcePath, Path.Combine(reportDirectory, "town_glb_meshes.csv"));
            WriteCatReport(Path.Combine(reportDirectory, "cat_source.txt"));
            Debug.Log("[CatLifeMobileRebuild] Source probe exported: " + reportDirectory);
            EditorApplication.Exit(0);
        }

        private static void WriteTownReport(string modelPath, string outputPath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
            {
                throw new FileNotFoundException("Town source model was not imported.", modelPath);
            }

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("path,mesh,vertices,triangles,submeshes,materials,material_paths,texture_paths,position_x,position_y,position_z");
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                string materials = renderer == null
                    ? string.Empty
                    : string.Join("|", renderer.sharedMaterials.Where(material => material != null).Select(material => material.name));
                string materialPaths = renderer == null
                    ? string.Empty
                    : string.Join("|", renderer.sharedMaterials
                        .Where(material => material != null)
                        .Select(AssetDatabase.GetAssetPath));
                string texturePaths = renderer == null
                    ? string.Empty
                    : string.Join("|", renderer.sharedMaterials
                        .Where(material => material != null)
                        .SelectMany(material => material.GetTexturePropertyNames()
                            .Select(material.GetTexture)
                            .Where(texture => texture != null)
                            .Select(AssetDatabase.GetAssetPath))
                        .Distinct());
                Vector3 position = filter.transform.position;
                csv.AppendLine(string.Join(",",
                    Csv(GetPath(filter.transform, source.transform)),
                    Csv(mesh.name),
                    mesh.vertexCount.ToString(CultureInfo.InvariantCulture),
                    (mesh.triangles.Length / 3).ToString(CultureInfo.InvariantCulture),
                    mesh.subMeshCount.ToString(CultureInfo.InvariantCulture),
                    Csv(materials),
                    Csv(materialPaths),
                    Csv(texturePaths),
                    position.x.ToString("0.###", CultureInfo.InvariantCulture),
                    position.y.ToString("0.###", CultureInfo.InvariantCulture),
                    position.z.ToString("0.###", CultureInfo.InvariantCulture)));
            }

            File.WriteAllText(outputPath, csv.ToString(), Encoding.UTF8);
        }

        private static void WriteCatReport(string outputPath)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CatSourcePath);
            if (source == null)
            {
                throw new FileNotFoundException("Cat source model was not imported.", CatSourcePath);
            }

            int skinnedMeshes = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            int vertices = source.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMesh != null)
                .Sum(renderer => renderer.sharedMesh.vertexCount);
            int triangles = source.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.sharedMesh != null)
                .Sum(renderer => renderer.sharedMesh.triangles.Length / 3);
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(CatSourcePath).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            string[] lines =
            {
                "Source: " + CatSourcePath,
                "Skinned meshes: " + skinnedMeshes,
                "Vertices: " + vertices,
                "Triangles: " + triangles,
                "Animation clips: " + clips.Length,
                "Clip names: " + string.Join(" | ", clips.Select(clip => clip.name))
            };
            File.WriteAllLines(outputPath, lines, Encoding.UTF8);
        }

        private static string GetPath(Transform current, Transform root)
        {
            List<string> names = new List<string>();
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (args[index] == name)
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
