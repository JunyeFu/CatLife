using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CatLife.Editor
{
    public static class CatLifeModelImportPolicy
    {
        private const string TownFbxPath = "Assets/MobileRuntime/Art/Town/Source/CL_TWN_Runtime.fbx";
        private const string CatAnimationFbxPath = "Assets/MobileRuntime/Art/Cat/Source/CL_CAT_Runtime.fbx";

        [MenuItem("CatLife/Optimization/Stage 5/Audit Model Import Policy")]
        public static void AuditModelImportPolicyFromMenu()
        {
            string reportDirectory = CreateReportDirectory("stage5-model-import-audit");
            AuditModelImportPolicy(reportDirectory);
            Debug.Log("[CatLifeOptimization] Stage 5 model import audit exported: " + reportDirectory);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        [MenuItem("CatLife/Optimization/Stage 5/Apply Model Import Policy")]
        public static void ApplyModelImportPolicyFromMenu()
        {
            string reportDirectory = CreateReportDirectory("stage5-model-import-apply");
            ApplyModelImportPolicy(reportDirectory);
            Debug.Log("[CatLifeOptimization] Stage 5 model import policy applied: " + reportDirectory);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        public static void AuditModelImportPolicyBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("stage5-model-import-audit");
            }

            try
            {
                AuditModelImportPolicy(reportDirectory);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyModelImportPolicyBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("stage5-model-import-apply");
            }

            try
            {
                ApplyModelImportPolicy(reportDirectory);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void AuditModelImportPolicy(string reportDirectory)
        {
            Directory.CreateDirectory(reportDirectory);
            WriteReports(reportDirectory, CollectRows(), changedCount: null);
        }

        public static void ApplyModelImportPolicy(string reportDirectory)
        {
            Directory.CreateDirectory(reportDirectory);
            int changedCount = 0;
            foreach (ModelPolicyRow row in CollectRows())
            {
                ModelImporter importer = AssetImporter.GetAtPath(row.Path) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                if (!row.ApplyStaticTownPolicy)
                {
                    bool catChanged = false;
                    if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
                    {
                        importer.materialImportMode = ModelImporterMaterialImportMode.None;
                        catChanged = true;
                    }
                    if (importer.animationType != ModelImporterAnimationType.Generic) { importer.animationType = ModelImporterAnimationType.Generic; catChanged = true; }
                    if (!importer.importAnimation) { importer.importAnimation = true; catChanged = true; }
                    if (importer.importCameras) { importer.importCameras = false; catChanged = true; }
                    if (importer.importLights) { importer.importLights = false; catChanged = true; }
                    if (importer.importBlendShapes) { importer.importBlendShapes = false; catChanged = true; }
                    if (importer.importVisibility) { importer.importVisibility = false; catChanged = true; }
                    if (importer.isReadable) { importer.isReadable = false; catChanged = true; }
                    if (catChanged) { AssetDatabase.WriteImportSettingsIfDirty(row.Path); importer.SaveAndReimport(); changedCount++; }
                    continue;
                }

                bool changed = false;
                if (importer.meshCompression != ModelImporterMeshCompression.Low)
                {
                    importer.meshCompression = ModelImporterMeshCompression.Low;
                    changed = true;
                }

                if (importer.importBlendShapes)
                {
                    importer.importBlendShapes = false;
                    changed = true;
                }

                if (importer.importAnimation)
                {
                    importer.importAnimation = false;
                    changed = true;
                }

                if (importer.importCameras)
                {
                    importer.importCameras = false;
                    changed = true;
                }

                if (importer.importLights)
                {
                    importer.importLights = false;
                    changed = true;
                }

                if (importer.importVisibility)
                {
                    importer.importVisibility = false;
                    changed = true;
                }

                if (importer.isReadable)
                {
                    importer.isReadable = false;
                    changed = true;
                }

                if (!importer.optimizeMeshPolygons)
                {
                    importer.optimizeMeshPolygons = true;
                    changed = true;
                }

                if (!importer.optimizeMeshVertices)
                {
                    importer.optimizeMeshVertices = true;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                AssetDatabase.WriteImportSettingsIfDirty(row.Path);
                importer.SaveAndReimport();
                changedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteReports(reportDirectory, CollectRows(), changedCount);
        }

        private static List<ModelPolicyRow> CollectRows()
        {
            List<ModelPolicyRow> rows = new List<ModelPolicyRow>();
            rows.Add(BuildRow(TownFbxPath, applyStaticTownPolicy: true));
            rows.Add(BuildRow(CatAnimationFbxPath, applyStaticTownPolicy: false));
            return rows;
        }

        private static ModelPolicyRow BuildRow(string path, bool applyStaticTownPolicy)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            ModelStats stats = CollectModelStats(path);
            long sourceBytes = GetSourceBytes(path);
            if (importer == null)
            {
                return new ModelPolicyRow(path, sourceBytes, stats, applyStaticTownPolicy);
            }

            return new ModelPolicyRow(
                path,
                sourceBytes,
                stats,
                applyStaticTownPolicy,
                importer.isReadable,
                importer.meshCompression,
                importer.optimizeMeshPolygons,
                importer.optimizeMeshVertices,
                importer.importAnimation,
                importer.animationCompression,
                importer.importCameras,
                importer.importLights,
                importer.importBlendShapes,
                importer.importVisibility);
        }

        private static ModelStats CollectModelStats(string path)
        {
            int meshCount = 0;
            int blendShapeMeshCount = 0;
            int animationClipCount = 0;
            long vertexCount = 0;
            long triangleCount = 0;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                Mesh mesh = asset as Mesh;
                if (mesh != null)
                {
                    meshCount++;
                    vertexCount += mesh.vertexCount;
                    triangleCount += mesh.triangles == null ? 0 : mesh.triangles.Length / 3;
                    if (mesh.blendShapeCount > 0)
                    {
                        blendShapeMeshCount++;
                    }
                }

                if (asset is AnimationClip)
                {
                    animationClipCount++;
                }
            }

            return new ModelStats(meshCount, vertexCount, triangleCount, blendShapeMeshCount, animationClipCount);
        }

        private static void WriteReports(string reportDirectory, List<ModelPolicyRow> rows, int? changedCount)
        {
            WriteSummary(Path.Combine(reportDirectory, "model_import_summary.md"), rows, changedCount);
            WriteCsv(Path.Combine(reportDirectory, "model_import_rows.csv"), rows);
        }

        private static void WriteSummary(string path, List<ModelPolicyRow> rows, int? changedCount)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# CatLife Stage 5 Model Import Policy");
            sb.AppendLine();
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            if (changedCount.HasValue)
            {
                sb.AppendLine("Changed importers: " + changedCount.Value.ToString(CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
            sb.AppendLine("| Model | Source MiB | Meshes | Vertices | Triangles | BlendShape meshes | Anim clips | Mesh compression | Import blendshapes | Policy |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|---:|---|");
            foreach (ModelPolicyRow row in rows)
            {
                sb.Append("| `");
                sb.Append(row.Path);
                sb.Append("` | ");
                sb.Append((row.SourceBytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append(row.Stats.MeshCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append(row.Stats.VertexCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append(row.Stats.TriangleCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append(row.Stats.BlendShapeMeshCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append(row.Stats.AnimationClipCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append(row.MeshCompression);
                sb.Append(" | ");
                sb.Append(row.ImportBlendShapes ? "on" : "off");
                sb.Append(" | ");
                sb.Append(row.ApplyStaticTownPolicy ? "Static town: Low compression, optimized meshes, no animation/camera/light/blendshape/read-write" : "Protected cat animation/model importer");
                sb.AppendLine(" |");
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteCsv(string path, List<ModelPolicyRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("path,source_bytes,source_mib,mesh_count,vertices,triangles,blendshape_meshes,animation_clips,is_readable,mesh_compression,optimize_polygons,optimize_vertices,import_animation,animation_compression,import_cameras,import_lights,import_blendshapes,import_visibility,policy");
            foreach (ModelPolicyRow row in rows)
            {
                sb.AppendCsv(row.Path);
                sb.Append(',');
                sb.Append(row.SourceBytes.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append((row.SourceBytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.Stats.MeshCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.Stats.VertexCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.Stats.TriangleCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.Stats.BlendShapeMeshCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.Stats.AnimationClipCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.IsReadable ? "true" : "false");
                sb.Append(',');
                sb.AppendCsv(row.MeshCompression.ToString());
                sb.Append(',');
                sb.Append(row.OptimizeMeshPolygons ? "true" : "false");
                sb.Append(',');
                sb.Append(row.OptimizeMeshVertices ? "true" : "false");
                sb.Append(',');
                sb.Append(row.ImportAnimation ? "true" : "false");
                sb.Append(',');
                sb.AppendCsv(row.AnimationCompression.ToString());
                sb.Append(',');
                sb.Append(row.ImportCameras ? "true" : "false");
                sb.Append(',');
                sb.Append(row.ImportLights ? "true" : "false");
                sb.Append(',');
                sb.Append(row.ImportBlendShapes ? "true" : "false");
                sb.Append(',');
                sb.Append(row.ImportVisibility ? "true" : "false");
                sb.Append(',');
                sb.AppendCsv(row.ApplyStaticTownPolicy ? "static_town" : "protected_cat");
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static long GetSourceBytes(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
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

        private sealed class ModelPolicyRow
        {
            public ModelPolicyRow(string path, long sourceBytes, ModelStats stats, bool applyStaticTownPolicy)
                : this(path, sourceBytes, stats, applyStaticTownPolicy, false, ModelImporterMeshCompression.Off, false, false, false, ModelImporterAnimationCompression.Off, false, false, false, false)
            {
            }

            public ModelPolicyRow(
                string path,
                long sourceBytes,
                ModelStats stats,
                bool applyStaticTownPolicy,
                bool isReadable,
                ModelImporterMeshCompression meshCompression,
                bool optimizeMeshPolygons,
                bool optimizeMeshVertices,
                bool importAnimation,
                ModelImporterAnimationCompression animationCompression,
                bool importCameras,
                bool importLights,
                bool importBlendShapes,
                bool importVisibility)
            {
                Path = path;
                SourceBytes = sourceBytes;
                Stats = stats;
                ApplyStaticTownPolicy = applyStaticTownPolicy;
                IsReadable = isReadable;
                MeshCompression = meshCompression;
                OptimizeMeshPolygons = optimizeMeshPolygons;
                OptimizeMeshVertices = optimizeMeshVertices;
                ImportAnimation = importAnimation;
                AnimationCompression = animationCompression;
                ImportCameras = importCameras;
                ImportLights = importLights;
                ImportBlendShapes = importBlendShapes;
                ImportVisibility = importVisibility;
            }

            public string Path { get; private set; }

            public long SourceBytes { get; private set; }

            public ModelStats Stats { get; private set; }

            public bool ApplyStaticTownPolicy { get; private set; }

            public bool IsReadable { get; private set; }

            public ModelImporterMeshCompression MeshCompression { get; private set; }

            public bool OptimizeMeshPolygons { get; private set; }

            public bool OptimizeMeshVertices { get; private set; }

            public bool ImportAnimation { get; private set; }

            public ModelImporterAnimationCompression AnimationCompression { get; private set; }

            public bool ImportCameras { get; private set; }

            public bool ImportLights { get; private set; }

            public bool ImportBlendShapes { get; private set; }

            public bool ImportVisibility { get; private set; }
        }

        private sealed class ModelStats
        {
            public ModelStats(int meshCount, long vertexCount, long triangleCount, int blendShapeMeshCount, int animationClipCount)
            {
                MeshCount = meshCount;
                VertexCount = vertexCount;
                TriangleCount = triangleCount;
                BlendShapeMeshCount = blendShapeMeshCount;
                AnimationClipCount = animationClipCount;
            }

            public int MeshCount { get; private set; }

            public long VertexCount { get; private set; }

            public long TriangleCount { get; private set; }

            public int BlendShapeMeshCount { get; private set; }

            public int AnimationClipCount { get; private set; }
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
