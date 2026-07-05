using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CatLife.Editor
{
    public static class CatLifeBuildSizeReporter
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string FinalSubmissionApk = "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk";

        [MenuItem("CatLife/Optimization/Stage 0/Export Project Size Inventory")]
        public static void ExportProjectSizeInventoryFromMenu()
        {
            string reportDirectory = CreateReportDirectory("inventory");
            ExportProjectSizeInventory(reportDirectory, null);
            Debug.Log("[CatLifeOptimization] Stage 0 inventory exported: " + reportDirectory);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        [MenuItem("CatLife/Optimization/Stage 0/Build Android Detailed Size Report")]
        public static void BuildAndroidDetailedSizeReportFromMenu()
        {
            string reportDirectory = CreateReportDirectory("android-detailed-build");
            string outputPath = Path.Combine(reportDirectory, "CatLife_SizeAudit.apk");
            BuildAndroidDetailedSizeReport(reportDirectory, outputPath, exitOnComplete: false);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        public static void ExportProjectSizeInventoryBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("inventory");
            }

            try
            {
                ExportProjectSizeInventory(reportDirectory, null);
                Debug.Log("[CatLifeOptimization] Stage 0 inventory exported: " + reportDirectory);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildAndroidDetailedSizeReportBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("android-detailed-build");
            }

            string outputPath = GetArg("-outputPath");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(reportDirectory, "CatLife_SizeAudit.apk");
            }

            BuildAndroidDetailedSizeReport(reportDirectory, outputPath, exitOnComplete: true);
        }

        private static void BuildAndroidDetailedSizeReport(string reportDirectory, string outputPath, bool exitOnComplete)
        {
            Directory.CreateDirectory(reportDirectory);
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            try
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = ResolveBuildScenes(),
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.DetailedBuildReport
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                ExportProjectSizeInventory(reportDirectory, report);
                WriteBuildReportFiles(reportDirectory, report);
                WriteApkEntries(reportDirectory, outputPath);

                if (report.summary.result != BuildResult.Succeeded)
                {
                    Complete(exitOnComplete, 1);
                    return;
                }

                Complete(exitOnComplete, 0);
            }
            catch (Exception ex)
            {
                WriteText(Path.Combine(reportDirectory, "build_exception.txt"), ex.ToString());
                Debug.LogError(ex);
                Complete(exitOnComplete, 1);
            }
        }

        private static void ExportProjectSizeInventory(string reportDirectory, BuildReport buildReport)
        {
            Directory.CreateDirectory(reportDirectory);
            AssetDatabase.Refresh();

            List<AssetSizeRow> allAssets = GetAssetsUnderAssetsDirectory();
            List<AssetSizeRow> sceneDependencies = GetSceneDependencies();

            WriteAssetCsv(Path.Combine(reportDirectory, "project_assets_top.csv"), allAssets.OrderByDescending(a => a.SizeBytes).Take(500));
            WriteAssetCsv(Path.Combine(reportDirectory, "main_scene_dependencies.csv"), sceneDependencies.OrderByDescending(a => a.SizeBytes));
            WriteAssetTypeSummary(Path.Combine(reportDirectory, "asset_type_summary.csv"), allAssets);
            WriteApkEntries(reportDirectory, ResolveExistingApkPath());
            WriteSummary(reportDirectory, allAssets, sceneDependencies, buildReport);
        }

        private static void WriteBuildReportFiles(string reportDirectory, BuildReport report)
        {
            WriteReflectiveRows(Path.Combine(reportDirectory, "build_files.csv"), InvokeEnumerable(report, "GetFiles"));
            WriteReflectiveRows(Path.Combine(reportDirectory, "packed_assets.csv"), GetMemberEnumerable(report, "packedAssets"));
            WriteReflectiveRows(Path.Combine(reportDirectory, "scenes_using_assets.csv"), GetMemberEnumerable(report, "scenesUsingAssets"));
        }

        private static List<AssetSizeRow> GetAssetsUnderAssetsDirectory()
        {
            List<AssetSizeRow> rows = new List<AssetSizeRow>();
            foreach (string assetPath in AssetDatabase.GetAllAssetPaths())
            {
                if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                rows.Add(CreateAssetRow(assetPath));
            }

            return rows;
        }

        private static List<AssetSizeRow> GetSceneDependencies()
        {
            List<AssetSizeRow> rows = new List<AssetSizeRow>();
            if (!File.Exists(ToFullPath(MainScenePath)))
            {
                return rows;
            }

            foreach (string assetPath in AssetDatabase.GetDependencies(MainScenePath, true).Distinct())
            {
                rows.Add(CreateAssetRow(assetPath));
            }

            return rows;
        }

        private static AssetSizeRow CreateAssetRow(string assetPath)
        {
            string fullPath = ToFullPath(assetPath);
            FileInfo file = File.Exists(fullPath) ? new FileInfo(fullPath) : null;
            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            string importerType = "missing";
            string assetType = "missing";

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer != null)
            {
                importerType = importer.GetType().Name;
            }

            Type type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (type != null)
            {
                assetType = type.Name;
            }

            return new AssetSizeRow
            {
                AssetPath = assetPath,
                Extension = extension,
                SizeBytes = file != null ? file.Length : 0L,
                AssetType = assetType,
                ImporterType = importerType
            };
        }

        private static void WriteSummary(string reportDirectory, List<AssetSizeRow> allAssets, List<AssetSizeRow> sceneDependencies, BuildReport buildReport)
        {
            long totalAssetsBytes = allAssets.Sum(a => a.SizeBytes);
            long sceneDependencyBytes = sceneDependencies.Sum(a => a.SizeBytes);
            string apkPath = ResolveExistingApkPath();
            FileInfo apkFile = !string.IsNullOrWhiteSpace(apkPath) && File.Exists(apkPath) ? new FileInfo(apkPath) : null;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# CatLife Android Size Optimization Stage 0 Report");
            sb.AppendLine();
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("Unity project: " + GetProjectRoot());
            sb.AppendLine("Main scene: " + MainScenePath);
            sb.AppendLine();
            sb.AppendLine("## Inventory");
            sb.AppendLine();
            sb.AppendLine("- Assets/ file count: " + allAssets.Count);
            sb.AppendLine("- Assets/ source bytes: " + totalAssetsBytes + " (" + ToMiB(totalAssetsBytes) + " MiB)");
            sb.AppendLine("- MainScene dependency count: " + sceneDependencies.Count);
            sb.AppendLine("- MainScene dependency source bytes: " + sceneDependencyBytes + " (" + ToMiB(sceneDependencyBytes) + " MiB)");
            sb.AppendLine("- Existing APK: " + (apkFile != null ? apkFile.FullName : "not found"));
            sb.AppendLine("- Existing APK bytes: " + (apkFile != null ? apkFile.Length.ToString(CultureInfo.InvariantCulture) : "missing"));
            sb.AppendLine();

            if (buildReport != null)
            {
                sb.AppendLine("## Detailed Build");
                sb.AppendLine();
                sb.AppendLine("- Result: " + buildReport.summary.result);
                sb.AppendLine("- Total size bytes: " + buildReport.summary.totalSize);
                sb.AppendLine("- Total errors: " + buildReport.summary.totalErrors);
                sb.AppendLine("- Total warnings: " + buildReport.summary.totalWarnings);
                sb.AppendLine("- Output path: " + buildReport.summary.outputPath);
                sb.AppendLine();
            }

            sb.AppendLine("## Files");
            sb.AppendLine();
            sb.AppendLine("- `project_assets_top.csv`: largest source files under Assets.");
            sb.AppendLine("- `asset_type_summary.csv`: total source bytes grouped by extension and Unity asset type.");
            sb.AppendLine("- `main_scene_dependencies.csv`: current MainScene dependency graph from AssetDatabase.GetDependencies.");
            sb.AppendLine("- `apk_entries.csv`: top APK zip entries when an APK exists.");
            sb.AppendLine("- `build_files.csv`, `packed_assets.csv`, `scenes_using_assets.csv`: emitted only by detailed build mode.");
            sb.AppendLine();
            sb.AppendLine("## Stage 0 Rule");
            sb.AppendLine();
            sb.AppendLine("This report is attribution only. Do not change import settings, move source assets, or compress textures until the largest contributors are reviewed.");

            WriteText(Path.Combine(reportDirectory, "build_summary.md"), sb.ToString());
        }

        private static void WriteAssetCsv(string path, IEnumerable<AssetSizeRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("asset_path,extension,size_bytes,size_mib,asset_type,importer_type");
            foreach (AssetSizeRow row in rows)
            {
                sb.Append(Csv(row.AssetPath)).Append(',');
                sb.Append(Csv(row.Extension)).Append(',');
                sb.Append(row.SizeBytes.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(ToMiB(row.SizeBytes)).Append(',');
                sb.Append(Csv(row.AssetType)).Append(',');
                sb.Append(Csv(row.ImporterType)).AppendLine();
            }

            WriteText(path, sb.ToString());
        }

        private static void WriteAssetTypeSummary(string path, IEnumerable<AssetSizeRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("extension,asset_type,count,total_bytes,total_mib");
            foreach (var group in rows.GroupBy(r => new { r.Extension, r.AssetType }).OrderByDescending(g => g.Sum(r => r.SizeBytes)))
            {
                long total = group.Sum(r => r.SizeBytes);
                sb.Append(Csv(group.Key.Extension)).Append(',');
                sb.Append(Csv(group.Key.AssetType)).Append(',');
                sb.Append(group.Count().ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(total.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(ToMiB(total)).AppendLine();
            }

            WriteText(path, sb.ToString());
        }

        private static void WriteApkEntries(string reportDirectory, string apkPath)
        {
            string output = Path.Combine(reportDirectory, "apk_entries.csv");
            string groupOutput = Path.Combine(reportDirectory, "apk_entry_groups.csv");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("entry,compressed_bytes,uncompressed_bytes,compressed_mib,uncompressed_mib");
            List<ApkEntryRow> rows = new List<ApkEntryRow>();

            if (string.IsNullOrWhiteSpace(apkPath) || !File.Exists(apkPath))
            {
                WriteText(output, sb.ToString());
                WriteText(groupOutput, "group,count,compressed_bytes,uncompressed_bytes,compressed_mib,uncompressed_mib" + Environment.NewLine);
                return;
            }

            using (ZipArchive archive = ZipFile.OpenRead(apkPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    rows.Add(new ApkEntryRow
                    {
                        Entry = entry.FullName,
                        Group = ClassifyApkEntry(entry.FullName),
                        CompressedBytes = entry.CompressedLength,
                        UncompressedBytes = entry.Length
                    });
                }

                foreach (ApkEntryRow entry in rows.OrderByDescending(e => e.CompressedBytes).Take(500))
                {
                    sb.Append(Csv(entry.Entry)).Append(',');
                    sb.Append(entry.CompressedBytes.ToString(CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(entry.UncompressedBytes.ToString(CultureInfo.InvariantCulture)).Append(',');
                    sb.Append(ToMiB(entry.CompressedBytes)).Append(',');
                    sb.Append(ToMiB(entry.UncompressedBytes)).AppendLine();
                }
            }

            WriteText(output, sb.ToString());
            WriteApkEntryGroups(groupOutput, rows);
        }

        private static void WriteApkEntryGroups(string path, List<ApkEntryRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("group,count,compressed_bytes,uncompressed_bytes,compressed_mib,uncompressed_mib");
            foreach (var group in rows.GroupBy(r => r.Group).OrderByDescending(g => g.Sum(r => r.CompressedBytes)))
            {
                long compressed = group.Sum(r => r.CompressedBytes);
                long uncompressed = group.Sum(r => r.UncompressedBytes);
                sb.Append(Csv(group.Key)).Append(',');
                sb.Append(group.Count().ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(compressed.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(uncompressed.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(ToMiB(compressed)).Append(',');
                sb.Append(ToMiB(uncompressed)).AppendLine();
            }

            WriteText(path, sb.ToString());
        }

        private static string ClassifyApkEntry(string entry)
        {
            string normalized = entry.Replace('\\', '/');
            if (normalized.StartsWith("assets/bin/Data/sharedassets", StringComparison.OrdinalIgnoreCase))
            {
                string file = Path.GetFileName(normalized);
                int splitIndex = file.IndexOf(".split", StringComparison.OrdinalIgnoreCase);
                return splitIndex > 0 ? "assets/bin/Data/" + file.Substring(0, splitIndex) : "assets/bin/Data/" + file;
            }

            if (normalized.StartsWith("assets/bin/Data/Managed/", StringComparison.OrdinalIgnoreCase))
            {
                return "assets/bin/Data/Managed";
            }

            if (normalized.StartsWith("assets/bin/Data/Resources/", StringComparison.OrdinalIgnoreCase))
            {
                return "assets/bin/Data/Resources";
            }

            if (normalized.StartsWith("assets/bin/Data/", StringComparison.OrdinalIgnoreCase))
            {
                string file = Path.GetFileName(normalized);
                return string.IsNullOrEmpty(file) ? "assets/bin/Data" : "assets/bin/Data/" + file;
            }

            if (normalized.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = normalized.Split('/');
                return parts.Length >= 2 ? "lib/" + parts[1] : "lib";
            }

            int slash = normalized.IndexOf('/');
            return slash > 0 ? normalized.Substring(0, slash) : normalized;
        }

        private static void WriteReflectiveRows(string path, IEnumerable rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("row_type,name,path,role,size_bytes,packed_size_bytes,raw");

            if (rows == null)
            {
                WriteText(path, sb.ToString());
                return;
            }

            foreach (object row in rows)
            {
                string name = StringMember(row, "name");
                string pathValue = FirstNonEmpty(StringMember(row, "path"), StringMember(row, "shortPath"), StringMember(row, "assetPath"), StringMember(row, "scenePath"));
                string role = StringMember(row, "role");
                string size = FirstNonEmpty(StringMember(row, "size"), StringMember(row, "fileSize"));
                string packedSize = FirstNonEmpty(StringMember(row, "packedSize"), StringMember(row, "overhead"));

                sb.Append(Csv(row.GetType().Name)).Append(',');
                sb.Append(Csv(name)).Append(',');
                sb.Append(Csv(pathValue)).Append(',');
                sb.Append(Csv(role)).Append(',');
                sb.Append(Csv(size)).Append(',');
                sb.Append(Csv(packedSize)).Append(',');
                sb.Append(Csv(row.ToString())).AppendLine();

                IEnumerable contents = GetMemberEnumerable(row, "contents");
                if (contents == null)
                {
                    continue;
                }

                foreach (object content in contents)
                {
                    string contentPath = FirstNonEmpty(StringMember(content, "sourceAssetPath"), StringMember(content, "assetPath"), StringMember(content, "path"));
                    string contentSize = FirstNonEmpty(StringMember(content, "packedSize"), StringMember(content, "size"));
                    sb.Append(Csv(content.GetType().Name)).Append(',');
                    sb.Append(Csv(StringMember(content, "name"))).Append(',');
                    sb.Append(Csv(contentPath)).Append(',');
                    sb.Append(Csv(StringMember(content, "role"))).Append(',');
                    sb.Append(Csv(string.Empty)).Append(',');
                    sb.Append(Csv(contentSize)).Append(',');
                    sb.Append(Csv(content.ToString())).AppendLine();
                }
            }

            WriteText(path, sb.ToString());
        }

        private static IEnumerable InvokeEnumerable(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object result = method != null ? method.Invoke(target, null) : null;
            return result as IEnumerable;
        }

        private static IEnumerable GetMemberEnumerable(object target, string memberName)
        {
            object value = GetMemberValue(target, memberName);
            return value as IEnumerable;
        }

        private static string StringMember(object target, string memberName)
        {
            object value = GetMemberValue(target, memberName);
            return value != null ? value.ToString() : string.Empty;
        }

        private static object GetMemberValue(object target, string memberName)
        {
            if (target == null)
            {
                return null;
            }

            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return property.GetValue(target, null);
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? field.GetValue(target) : null;
        }

        private static string[] ResolveBuildScenes()
        {
            List<string> paths = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && File.Exists(scene.path))
                {
                    paths.Add(scene.path);
                }
            }

            if (paths.Count == 0 && File.Exists(ToFullPath(MainScenePath)))
            {
                paths.Add(MainScenePath);
            }

            if (paths.Count == 0)
            {
                throw new InvalidOperationException("No enabled build scenes found. Expected " + MainScenePath);
            }

            return paths.ToArray();
        }

        private static string ResolveExistingApkPath()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(GetProjectRoot(), "..", ".."));
            string finalApk = Path.Combine(repoRoot, FinalSubmissionApk.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(finalApk))
            {
                return finalApk;
            }

            string buildsRoot = Path.Combine(GetProjectRoot(), "Builds");
            if (!Directory.Exists(buildsRoot))
            {
                return null;
            }

            return Directory.GetFiles(buildsRoot, "*.apk", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static string CreateReportDirectory(string label)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return Path.GetFullPath(Path.Combine(GetProjectRoot(), "Reports", "BuildSize", stamp + "-" + label));
        }

        private static string ToFullPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(GetProjectRoot(), assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void Complete(bool exitOnComplete, int exitCode)
        {
            if (exitOnComplete)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void WriteText(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static string ToMiB(long bytes)
        {
            return (bytes / 1024d / 1024d).ToString("F2", CultureInfo.InvariantCulture);
        }

        private sealed class AssetSizeRow
        {
            public string AssetPath;
            public string Extension;
            public long SizeBytes;
            public string AssetType;
            public string ImporterType;
        }

        private sealed class ApkEntryRow
        {
            public string Entry;
            public string Group;
            public long CompressedBytes;
            public long UncompressedBytes;
        }
    }
}
