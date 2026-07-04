using System;
using System.Collections.Generic;
using System.IO;
using CatLife.EditorTools;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CatLife.Editor
{
    public static class CatLifeAndroidBuild
    {
        private const string PackageName = "com.catlife.mvp";
        private const string ProductName = "CatLife";
        private const string CompanyName = "CatLifeTeam";
        private const string VersionName = "0.1.0";
        private const int VersionCode = 1;
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string PrivateCredentialAssetPath = "Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json";

        [MenuItem("CatLife/Build/Build Android APK")]
        public static void BuildApkFromMenu()
        {
            string outputPath = GetDefaultOutputPath();
            BuildApkInternal(outputPath, developmentBuild: true, exitOnComplete: false);
        }

        public static void BuildApk()
        {
            string outputPath = GetArg("-outputPath");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = GetDefaultOutputPath();
            }

            bool developmentBuild = !HasArg("-release");
            if (HasArg("-development"))
            {
                developmentBuild = true;
            }

            BuildApkInternal(outputPath, developmentBuild, exitOnComplete: true);
        }

        private static void BuildApkInternal(string outputPath, bool developmentBuild, bool exitOnComplete)
        {
            outputPath = Path.GetFullPath(outputPath);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("Android APK output directory could not be resolved.");
            }

            Directory.CreateDirectory(outputDirectory);
            string evidenceRoot = ResolveEvidenceRoot(outputDirectory);
            Directory.CreateDirectory(evidenceRoot);
            WritePrivateCredentialEvidence(evidenceRoot);

            try
            {
                string validation = CatLifeRuntimeAssemblyValidator.ValidateCurrentSceneReport();
                WriteText(Path.Combine(evidenceRoot, "runtime-validator-before-build.txt"), validation);
                if (!validation.StartsWith("PASS", StringComparison.Ordinal))
                {
                    Debug.LogError(validation);
                    Complete(exitOnComplete, 1);
                    return;
                }

                ConfigureAndroidPlayer(developmentBuild);

                string[] scenes = ResolveBuildScenes();
                WriteBuildSettings(evidenceRoot, outputPath, developmentBuild, scenes);

                BuildPlayerOptions options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = developmentBuild ? BuildOptions.Development : BuildOptions.None
                };

                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                string summaryLine = $"CATLIFE_ANDROID_BUILD result={summary.result} output={outputPath} size={summary.totalSize} errors={summary.totalErrors} warnings={summary.totalWarnings}";
                Debug.Log(summaryLine);
                WriteText(Path.Combine(evidenceRoot, "build-summary.txt"), summaryLine);

                if (summary.result == BuildResult.Succeeded)
                {
                    WriteApkHash(evidenceRoot, outputPath);
                    Complete(exitOnComplete, 0);
                    return;
                }

                Complete(exitOnComplete, 1);
            }
            catch (Exception ex)
            {
                string error = ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace;
                Debug.LogError(error);
                WriteText(Path.Combine(evidenceRoot, "build-exception.txt"), error);
                Complete(exitOnComplete, 1);
            }
        }

        private static void ConfigureAndroidPlayer(bool developmentBuild)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = developmentBuild;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = VersionName;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
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

            if (paths.Count == 0 && File.Exists(MainScenePath))
            {
                paths.Add(MainScenePath);
            }

            if (paths.Count == 0)
            {
                throw new InvalidOperationException("No enabled build scenes found. Expected Assets/Scenes/MainScene.unity.");
            }

            return paths.ToArray();
        }

        private static void WriteBuildSettings(string evidenceRoot, string outputPath, bool developmentBuild, string[] scenes)
        {
            string[] lines =
            {
                "Unity version: " + Application.unityVersion,
                "Build date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "Project path: " + Directory.GetCurrentDirectory(),
                "Output APK: " + outputPath,
                "Package name: " + PackageName,
                "Product name: " + ProductName,
                "Version: " + VersionName,
                "Version code: " + VersionCode,
                "Development build: " + developmentBuild,
                "Scripting backend: IL2CPP",
                "Target architecture: ARM64",
                "Texture compression: ASTC",
                "Min SDK: 28",
                "Build app bundle: false",
                "Private credential asset path: " + PrivateCredentialAssetPath,
                "Private credential value: REDACTED",
                "Scenes in build:",
                string.Join(Environment.NewLine, scenes)
            };

            WriteText(Path.Combine(evidenceRoot, "unity-build-settings.txt"), string.Join(Environment.NewLine, lines));
        }

        private static void WritePrivateCredentialEvidence(string evidenceRoot)
        {
            string configPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/CatLifePrivate/vivo_cloud_credentials.json"));
            bool exists = File.Exists(configPath);
            TextAsset resourcesConfig = Resources.Load<TextAsset>("CatLifePrivate/vivo_cloud_credentials");
            bool resourcesLoadable = resourcesConfig != null;
            string[] lines =
            {
                "Private config path: " + PrivateCredentialAssetPath,
                "Exists: " + exists,
                "Unity Resources loadable: " + resourcesLoadable,
                "Unity Resources bytes: " + (resourcesLoadable ? resourcesConfig.bytes.Length.ToString() : "missing"),
                "AppID: 2026414599",
                "AppKEY present: " + exists,
                "AppKEY value: REDACTED"
            };

            WriteText(Path.Combine(evidenceRoot, "private_config_presence_redacted.txt"), string.Join(Environment.NewLine, lines));
        }

        private static void WriteApkHash(string evidenceRoot, string apkPath)
        {
            if (!File.Exists(apkPath))
            {
                WriteText(Path.Combine(evidenceRoot, "apk-sha256.txt"), "APK file: " + apkPath + Environment.NewLine + "Size bytes: missing" + Environment.NewLine + "SHA256: missing");
                return;
            }

            FileInfo file = new FileInfo(apkPath);
            string hash = GetSha256(apkPath);
            string[] lines =
            {
                "APK file: " + apkPath,
                "Size bytes: " + file.Length,
                "SHA256: " + hash
            };
            WriteText(Path.Combine(evidenceRoot, "apk-sha256.txt"), string.Join(Environment.NewLine, lines));
        }

        private static string GetSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static string ResolveEvidenceRoot(string outputDirectory)
        {
            string finalSubmission = Path.GetFullPath(Path.Combine(GetProjectRoot(), "..", "..", "06-deliverables", "final-submission"));
            string defaultEvidenceRoot = Path.Combine(finalSubmission, "evidence", "android", "00-build");
            if (outputDirectory.StartsWith(finalSubmission, StringComparison.OrdinalIgnoreCase))
            {
                return defaultEvidenceRoot;
            }

            return Path.Combine(outputDirectory, "evidence");
        }

        private static string GetDefaultOutputPath()
        {
            return Path.GetFullPath(Path.Combine(GetProjectRoot(), "..", "..", "06-deliverables", "final-submission", "CatLife_MVP_Android_v0.1.0.apk"));
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

        private static bool HasArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args)
            {
                if (arg == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteText(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }

        private static void Complete(bool exitOnComplete, int exitCode)
        {
            if (exitOnComplete)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
