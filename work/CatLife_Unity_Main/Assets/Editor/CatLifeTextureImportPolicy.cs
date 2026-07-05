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
    public static class CatLifeTextureImportPolicy
    {
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string AndroidPlatformName = "Android";

        [MenuItem("CatLife/Optimization/Stage 3/Audit Android Texture Policy")]
        public static void AuditAndroidTexturePolicyFromMenu()
        {
            string reportDirectory = CreateReportDirectory("stage3-texture-policy-audit");
            AuditAndroidTexturePolicy(reportDirectory);
            Debug.Log("[CatLifeOptimization] Stage 3 texture policy audit exported: " + reportDirectory);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        [MenuItem("CatLife/Optimization/Stage 3/Apply Android Texture Policy")]
        public static void ApplyAndroidTexturePolicyFromMenu()
        {
            string reportDirectory = CreateReportDirectory("stage3-texture-policy-apply");
            ApplyAndroidTexturePolicy(reportDirectory);
            Debug.Log("[CatLifeOptimization] Stage 3 texture policy applied: " + reportDirectory);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        public static void AuditAndroidTexturePolicyBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("stage3-texture-policy-audit");
            }

            try
            {
                AuditAndroidTexturePolicy(reportDirectory);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyAndroidTexturePolicyBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("stage3-texture-policy-apply");
            }

            try
            {
                ApplyAndroidTexturePolicy(reportDirectory);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void AuditAndroidTexturePolicy(string reportDirectory)
        {
            Directory.CreateDirectory(reportDirectory);
            List<TexturePolicyRow> rows = CollectRows();
            WriteRowsCsv(Path.Combine(reportDirectory, "texture_policy_audit.csv"), rows);
            WriteSummary(Path.Combine(reportDirectory, "texture_policy_summary.md"), rows, changedCount: null);
        }

        public static void ApplyAndroidTexturePolicy(string reportDirectory)
        {
            Directory.CreateDirectory(reportDirectory);
            List<TexturePolicyRow> rows = CollectRows();
            int changedCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (TexturePolicyRow row in rows)
                {
                    if (row.Rule.KeepDefault)
                    {
                        continue;
                    }

                    TextureImporter importer = AssetImporter.GetAtPath(row.Path) as TextureImporter;
                    if (importer == null)
                    {
                        continue;
                    }

                    TextureImporterPlatformSettings current = importer.GetPlatformTextureSettings(AndroidPlatformName);
                    TextureImporterPlatformSettings next = new TextureImporterPlatformSettings
                    {
                        name = AndroidPlatformName,
                        overridden = true,
                        maxTextureSize = row.Rule.MaxTextureSize,
                        format = row.Rule.Format,
                        textureCompression = TextureImporterCompression.CompressedHQ,
                        compressionQuality = row.Rule.CompressionQuality,
                        allowsAlphaSplitting = false,
                        androidETC2FallbackOverride = AndroidETC2FallbackOverride.UseBuildSettings
                    };

                    if (PlatformSettingsEqual(current, next))
                    {
                        continue;
                    }

                    importer.SetPlatformTextureSettings(next);
                    AssetDatabase.WriteImportSettingsIfDirty(row.Path);
                    changedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            rows = CollectRows();
            WriteRowsCsv(Path.Combine(reportDirectory, "texture_policy_after_apply.csv"), rows);
            WriteSummary(Path.Combine(reportDirectory, "texture_policy_summary.md"), rows, changedCount);
        }

        private static List<TexturePolicyRow> CollectRows()
        {
            HashSet<string> mainSceneDependencies = new HashSet<string>(AssetDatabase.GetDependencies(MainScenePath, true));
            List<TexturePolicyRow> rows = new List<TexturePolicyRow>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (importer == null || texture == null)
                {
                    continue;
                }

                TexturePolicyRule rule = Classify(path);
                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings(AndroidPlatformName);
                long sourceBytes = GetSourceBytes(path);
                rows.Add(new TexturePolicyRow(
                    path,
                    sourceBytes,
                    texture.width,
                    texture.height,
                    importer.mipmapEnabled,
                    importer.sRGBTexture,
                    importer.textureType.ToString(),
                    mainSceneDependencies.Contains(path),
                    android.overridden,
                    android.maxTextureSize,
                    android.format,
                    android.compressionQuality,
                    rule));
            }

            rows.Sort((left, right) => right.SourceBytes.CompareTo(left.SourceBytes));
            return rows;
        }

        private static TexturePolicyRule Classify(string path)
        {
            if (path.StartsWith("Assets/UI/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Resources/", StringComparison.Ordinal))
            {
                return TexturePolicyRule.Keep("KeepDefault", "UI and startup resources stay on default importer rules.");
            }

            if (path.StartsWith("Assets/Art/Cat/Textures/", StringComparison.Ordinal))
            {
                return TexturePolicyRule.Override("CatHighQuality", TextureImporterFormat.ASTC_4x4, 2048, 100, "Cat textures stay full resolution with high quality ASTC.");
            }

            if (path.StartsWith("Assets/Art/Town/Textures/GeneratedMasks/", StringComparison.Ordinal))
            {
                return TexturePolicyRule.Override("TownMaskCompact", TextureImporterFormat.ASTC_8x8, 1024, 80, "Metallic/roughness masks are not directly inspected by users, so they use lower resolution and denser ASTC.");
            }

            if (path.StartsWith("Assets/Art/Town/Textures/Extracted/", StringComparison.Ordinal))
            {
                return TexturePolicyRule.Override("TownColorBalanced", TextureImporterFormat.ASTC_4x4, 1024, 90, "Town color textures use a conservative 1024 cap with high quality ASTC to reduce size without a large visual drop.");
            }

            if (path.StartsWith("Assets/Art/Town/", StringComparison.Ordinal))
            {
                return TexturePolicyRule.Override("TownOtherBalanced", TextureImporterFormat.ASTC_6x6, 1024, 90, "Other town textures use a conservative 1024 cap and balanced ASTC.");
            }

            return TexturePolicyRule.Keep("KeepDefault", "Non-art texture kept unchanged for safety.");
        }

        private static bool PlatformSettingsEqual(TextureImporterPlatformSettings current, TextureImporterPlatformSettings next)
        {
            return current.overridden == next.overridden &&
                   current.maxTextureSize == next.maxTextureSize &&
                   current.format == next.format &&
                   current.textureCompression == next.textureCompression &&
                   current.compressionQuality == next.compressionQuality &&
                   current.allowsAlphaSplitting == next.allowsAlphaSplitting &&
                   current.androidETC2FallbackOverride == next.androidETC2FallbackOverride;
        }

        private static void WriteRowsCsv(string path, List<TexturePolicyRow> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("path,source_bytes,source_mib,width,height,mipmap,srgb,texture_type,main_scene_dependency,android_overridden,android_max_size,android_format,android_quality,policy,policy_format,policy_max_size,policy_quality,policy_reason");
            foreach (TexturePolicyRow row in rows)
            {
                sb.AppendCsv(row.Path);
                sb.Append(',');
                sb.Append(row.SourceBytes.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append((row.SourceBytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.Width.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.Height.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.MipmapEnabled ? "true" : "false");
                sb.Append(',');
                sb.Append(row.Srgb ? "true" : "false");
                sb.Append(',');
                sb.AppendCsv(row.TextureType);
                sb.Append(',');
                sb.Append(row.MainSceneDependency ? "true" : "false");
                sb.Append(',');
                sb.Append(row.AndroidOverridden ? "true" : "false");
                sb.Append(',');
                sb.Append(row.AndroidMaxTextureSize.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.AppendCsv(row.AndroidFormat.ToString());
                sb.Append(',');
                sb.Append(row.AndroidCompressionQuality.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.AppendCsv(row.Rule.Name);
                sb.Append(',');
                sb.AppendCsv(row.Rule.KeepDefault ? "default" : row.Rule.Format.ToString());
                sb.Append(',');
                sb.Append(row.Rule.KeepDefault ? string.Empty : row.Rule.MaxTextureSize.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(row.Rule.KeepDefault ? string.Empty : row.Rule.CompressionQuality.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.AppendCsv(row.Rule.Reason);
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static void WriteSummary(string path, List<TexturePolicyRow> rows, int? changedCount)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# CatLife Stage 3 Android Texture Policy");
            sb.AppendLine();
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            if (changedCount.HasValue)
            {
                sb.AppendLine("Changed importers: " + changedCount.Value.ToString(CultureInfo.InvariantCulture));
            }
            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("| Policy | Count | MainScene deps | Source MiB | Android override count |");
            sb.AppendLine("|---|---:|---:|---:|---:|");
            foreach (IGrouping<string, TexturePolicyRow> group in rows.GroupBy(row => row.Rule.Name).OrderBy(group => group.Key))
            {
                long sourceBytes = group.Sum(row => row.SourceBytes);
                int dependencyCount = group.Count(row => row.MainSceneDependency);
                int overrideCount = group.Count(row => row.AndroidOverridden);
                sb.Append("| ");
                sb.Append(group.Key);
                sb.Append(" | ");
                sb.Append(group.Count().ToString(CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append(dependencyCount.ToString(CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append((sourceBytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture));
                sb.Append(" | ");
                sb.Append(overrideCount.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine(" |");
            }

            sb.AppendLine();
            sb.AppendLine("## Rules");
            sb.AppendLine();
            foreach (TexturePolicyRule rule in rows.Select(row => row.Rule).GroupBy(rule => rule.Name).Select(group => group.First()).OrderBy(rule => rule.Name))
            {
                sb.Append("- `");
                sb.Append(rule.Name);
                sb.Append("`: ");
                if (rule.KeepDefault)
                {
                    sb.Append("keep default importer settings");
                }
                else
                {
                    sb.Append(rule.Format);
                    sb.Append(", max ");
                    sb.Append(rule.MaxTextureSize.ToString(CultureInfo.InvariantCulture));
                    sb.Append(", quality ");
                    sb.Append(rule.CompressionQuality.ToString(CultureInfo.InvariantCulture));
                }
                sb.Append(". ");
                sb.AppendLine(rule.Reason);
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static long GetSourceBytes(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return 0;
            }

            return new FileInfo(fullPath).Length;
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

        private readonly struct TexturePolicyRow
        {
            public TexturePolicyRow(
                string path,
                long sourceBytes,
                int width,
                int height,
                bool mipmapEnabled,
                bool srgb,
                string textureType,
                bool mainSceneDependency,
                bool androidOverridden,
                int androidMaxTextureSize,
                TextureImporterFormat androidFormat,
                int androidCompressionQuality,
                TexturePolicyRule rule)
            {
                Path = path;
                SourceBytes = sourceBytes;
                Width = width;
                Height = height;
                MipmapEnabled = mipmapEnabled;
                Srgb = srgb;
                TextureType = textureType;
                MainSceneDependency = mainSceneDependency;
                AndroidOverridden = androidOverridden;
                AndroidMaxTextureSize = androidMaxTextureSize;
                AndroidFormat = androidFormat;
                AndroidCompressionQuality = androidCompressionQuality;
                Rule = rule;
            }

            public string Path { get; }

            public long SourceBytes { get; }

            public int Width { get; }

            public int Height { get; }

            public bool MipmapEnabled { get; }

            public bool Srgb { get; }

            public string TextureType { get; }

            public bool MainSceneDependency { get; }

            public bool AndroidOverridden { get; }

            public int AndroidMaxTextureSize { get; }

            public TextureImporterFormat AndroidFormat { get; }

            public int AndroidCompressionQuality { get; }

            public TexturePolicyRule Rule { get; }
        }

        private readonly struct TexturePolicyRule
        {
            private TexturePolicyRule(string name, bool keepDefault, TextureImporterFormat format, int maxTextureSize, int compressionQuality, string reason)
            {
                Name = name;
                KeepDefault = keepDefault;
                Format = format;
                MaxTextureSize = maxTextureSize;
                CompressionQuality = compressionQuality;
                Reason = reason;
            }

            public string Name { get; }

            public bool KeepDefault { get; }

            public TextureImporterFormat Format { get; }

            public int MaxTextureSize { get; }

            public int CompressionQuality { get; }

            public string Reason { get; }

            public static TexturePolicyRule Keep(string name, string reason)
            {
                return new TexturePolicyRule(name, true, TextureImporterFormat.Automatic, 0, 0, reason);
            }

            public static TexturePolicyRule Override(string name, TextureImporterFormat format, int maxTextureSize, int compressionQuality, string reason)
            {
                return new TexturePolicyRule(name, false, format, maxTextureSize, compressionQuality, reason);
            }
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
