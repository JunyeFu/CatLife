using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatLife.Editor
{
    public static class CatLifeMaterialDeduplicationPolicy
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";

        [MenuItem("CatLife/Optimization/Stage 4/Audit Material Deduplication")]
        public static void AuditMaterialDeduplicationFromMenu()
        {
            string reportDirectory = CreateReportDirectory("stage4-material-dedup-audit");
            AuditMaterialDeduplication(reportDirectory);
            Debug.Log("[CatLifeOptimization] Stage 4 material dedup audit exported: " + reportDirectory);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        [MenuItem("CatLife/Optimization/Stage 4/Apply Material Deduplication")]
        public static void ApplyMaterialDeduplicationFromMenu()
        {
            string reportDirectory = CreateReportDirectory("stage4-material-dedup-apply");
            ApplyMaterialDeduplication(reportDirectory);
            Debug.Log("[CatLifeOptimization] Stage 4 material dedup applied: " + reportDirectory);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        public static void AuditMaterialDeduplicationBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("stage4-material-dedup-audit");
            }

            try
            {
                AuditMaterialDeduplication(reportDirectory);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyMaterialDeduplicationBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("stage4-material-dedup-apply");
            }

            try
            {
                ApplyMaterialDeduplication(reportDirectory);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void AuditMaterialDeduplication(string reportDirectory)
        {
            Directory.CreateDirectory(reportDirectory);
            MaterialDedupReport report = BuildReport();
            WriteReports(reportDirectory, report, applyResult: null);
        }

        public static void ApplyMaterialDeduplication(string reportDirectory)
        {
            Directory.CreateDirectory(reportDirectory);
            MaterialDedupReport before = BuildReport();
            Dictionary<Material, Material> replacementMap = BuildReplacementMap(before.Groups);
            int rendererCount = 0;
            int slotCount = 0;

            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Material[] sharedMaterials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material material = sharedMaterials[i];
                    if (material == null)
                    {
                        continue;
                    }

                    Material replacement;
                    if (!replacementMap.TryGetValue(material, out replacement))
                    {
                        continue;
                    }

                    sharedMaterials[i] = replacement;
                    changed = true;
                    slotCount++;
                }

                if (!changed)
                {
                    continue;
                }

                Undo.RecordObject(renderer, "CatLife material deduplication");
                renderer.sharedMaterials = sharedMaterials;
                EditorUtility.SetDirty(renderer);
                rendererCount++;
            }

            if (slotCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            }

            MaterialDedupReport after = BuildReport();
            WriteReports(reportDirectory, after, new ApplyResult(rendererCount, slotCount, before.Groups.Count, before.DuplicateMaterialCount));
        }

        private static MaterialDedupReport BuildReport()
        {
            HashSet<string> sceneDependencies = new HashSet<string>(AssetDatabase.GetDependencies(MainScenePath, true));
            Dictionary<string, int> rendererUse = CountRendererMaterialUse();
            List<Material> materials = new List<Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!sceneDependencies.Contains(path) || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null)
                {
                    materials.Add(material);
                }
            }

            Dictionary<string, List<Material>> signatureGroups = new Dictionary<string, List<Material>>();
            foreach (Material material in materials)
            {
                string signature = GetMaterialSignature(material);
                List<Material> group;
                if (!signatureGroups.TryGetValue(signature, out group))
                {
                    group = new List<Material>();
                    signatureGroups.Add(signature, group);
                }

                group.Add(material);
            }

            List<MaterialGroup> groups = new List<MaterialGroup>();
            foreach (List<Material> group in signatureGroups.Values)
            {
                if (group.Count <= 1)
                {
                    continue;
                }

                group.Sort((left, right) => string.CompareOrdinal(AssetDatabase.GetAssetPath(left), AssetDatabase.GetAssetPath(right)));
                groups.Add(new MaterialGroup(group, rendererUse));
            }

            groups.Sort((left, right) => right.Materials.Count.CompareTo(left.Materials.Count));

            return new MaterialDedupReport(materials.Count, rendererUse, groups);
        }

        private static Dictionary<Material, Material> BuildReplacementMap(List<MaterialGroup> groups)
        {
            Dictionary<Material, Material> replacementMap = new Dictionary<Material, Material>();
            foreach (MaterialGroup group in groups)
            {
                Material keeper = group.Keeper;
                foreach (Material material in group.Materials)
                {
                    if (material == keeper)
                    {
                        continue;
                    }

                    replacementMap[material] = keeper;
                }
            }

            return replacementMap;
        }

        private static Dictionary<string, int> CountRendererMaterialUse()
        {
            Dictionary<string, int> rendererUse = new Dictionary<string, int>();
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(material);
                    if (string.IsNullOrEmpty(path))
                    {
                        path = "<embedded>:" + material.name;
                    }

                    int count;
                    rendererUse.TryGetValue(path, out count);
                    rendererUse[path] = count + 1;
                }
            }

            return rendererUse;
        }

        private static string GetMaterialSignature(Material material)
        {
            string json = EditorJsonUtility.ToJson(material, false);
            json = Regex.Replace(json, "\"m_Name\":\"[^\"]*\",?", string.Empty);
            string shaderPath = material.shader == null ? "<no-shader>" : AssetDatabase.GetAssetPath(material.shader) + "#" + material.shader.name;
            return shaderPath + "\n" + json;
        }

        private static void WriteReports(string reportDirectory, MaterialDedupReport report, ApplyResult applyResult)
        {
            WriteSummary(Path.Combine(reportDirectory, "material_dedup_summary.md"), report, applyResult);
            WriteGroupsCsv(Path.Combine(reportDirectory, "material_duplicate_groups.csv"), report.Groups);
        }

        private static void WriteSummary(string path, MaterialDedupReport report, ApplyResult applyResult)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# CatLife Stage 4 Material Deduplication");
            sb.AppendLine();
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            if (applyResult != null)
            {
                sb.AppendLine("## Apply Result");
                sb.AppendLine();
                sb.AppendLine("- Renderers changed: " + applyResult.RenderersChanged.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("- Material slots changed: " + applyResult.SlotsChanged.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("- Duplicate groups before apply: " + applyResult.GroupsBefore.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("- Duplicate materials before apply: " + applyResult.DuplicatesBefore.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine();
            }

            int rendererSlots = report.RendererUse.Values.Sum();
            sb.AppendLine("## Current State");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|---|---:|");
            sb.AppendLine("| MainScene material dependencies | " + report.MaterialDependencyCount.ToString(CultureInfo.InvariantCulture) + " |");
            sb.AppendLine("| Renderer material slots | " + rendererSlots.ToString(CultureInfo.InvariantCulture) + " |");
            sb.AppendLine("| Renderer unique material paths | " + report.RendererUse.Count.ToString(CultureInfo.InvariantCulture) + " |");
            sb.AppendLine("| Exact duplicate groups | " + report.Groups.Count.ToString(CultureInfo.InvariantCulture) + " |");
            sb.AppendLine("| Duplicate materials | " + report.DuplicateMaterialCount.ToString(CultureInfo.InvariantCulture) + " |");
            sb.AppendLine();
            sb.AppendLine("## Duplicate Groups");
            sb.AppendLine();
            sb.AppendLine("| Keeper | Count | Renderer slots in group |");
            sb.AppendLine("|---|---:|---:|");
            foreach (MaterialGroup group in report.Groups)
            {
                sb.Append("| `");
                sb.Append(group.KeeperPath);
                sb.Append("` | ");
                sb.Append(group.Materials.Count.ToString(CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append(group.RendererSlotCount.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine(" |");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteGroupsCsv(string path, List<MaterialGroup> groups)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("group_index,keeper,material_path,renderer_slot_count");
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                MaterialGroup group = groups[groupIndex];
                foreach (Material material in group.Materials)
                {
                    string materialPath = AssetDatabase.GetAssetPath(material);
                    sb.Append(groupIndex.ToString(CultureInfo.InvariantCulture));
                    sb.Append(',');
                    sb.AppendCsv(group.KeeperPath);
                    sb.Append(',');
                    sb.AppendCsv(materialPath);
                    sb.Append(',');
                    sb.Append(group.GetRendererSlotCount(materialPath).ToString(CultureInfo.InvariantCulture));
                    sb.AppendLine();
                }
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static string CreateReportDirectory(string suffix)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string reportRoot = Path.Combine(projectRoot, "Reports", "BuildSize");
            string reportDirectory = Path.Combine(reportRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + suffix);
            Directory.CreateDirectory(reportDirectory);
            return reportDirectory;
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private sealed class MaterialDedupReport
        {
            public MaterialDedupReport(int materialDependencyCount, Dictionary<string, int> rendererUse, List<MaterialGroup> groups)
            {
                MaterialDependencyCount = materialDependencyCount;
                RendererUse = rendererUse;
                Groups = groups;
            }

            public int MaterialDependencyCount { get; private set; }

            public Dictionary<string, int> RendererUse { get; private set; }

            public List<MaterialGroup> Groups { get; private set; }

            public int DuplicateMaterialCount
            {
                get { return Groups.Sum(group => group.Materials.Count - 1); }
            }
        }

        private sealed class MaterialGroup
        {
            private readonly Dictionary<string, int> rendererUse;

            public MaterialGroup(List<Material> materials, Dictionary<string, int> rendererUse)
            {
                Materials = materials;
                this.rendererUse = rendererUse;
                Keeper = materials[0];
                KeeperPath = AssetDatabase.GetAssetPath(Keeper);
                RendererSlotCount = materials.Sum(material => GetRendererSlotCount(AssetDatabase.GetAssetPath(material)));
            }

            public List<Material> Materials { get; private set; }

            public Material Keeper { get; private set; }

            public string KeeperPath { get; private set; }

            public int RendererSlotCount { get; private set; }

            public int GetRendererSlotCount(string materialPath)
            {
                int count;
                return rendererUse.TryGetValue(materialPath, out count) ? count : 0;
            }
        }

        private sealed class ApplyResult
        {
            public ApplyResult(int renderersChanged, int slotsChanged, int groupsBefore, int duplicatesBefore)
            {
                RenderersChanged = renderersChanged;
                SlotsChanged = slotsChanged;
                GroupsBefore = groupsBefore;
                DuplicatesBefore = duplicatesBefore;
            }

            public int RenderersChanged { get; private set; }

            public int SlotsChanged { get; private set; }

            public int GroupsBefore { get; private set; }

            public int DuplicatesBefore { get; private set; }
        }

        private static void AppendCsv(this StringBuilder sb, string value)
        {
            if (value == null)
            {
                return;
            }

            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!needsQuotes)
            {
                sb.Append(value);
                return;
            }

            sb.Append('"');
            sb.Append(value.Replace("\"", "\"\""));
            sb.Append('"');
        }
    }
}
