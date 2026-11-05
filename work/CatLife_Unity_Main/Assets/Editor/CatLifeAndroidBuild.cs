using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string VersionName = "0.3.0";
        private const int VersionCode = 3;
        private const string MainScenePath = "Assets/Scenes/CatLifeMobile.unity";
        private const string PrivateCredentialAssetPath = "Assets/Resources/CatLifePrivate/generic_cloud_credentials.json";
        private const string ExitReviewScene = "Assets/Editor/CatProneReview/ExitCandidate/CatLifeExitCandidate.unity";
        private const string CredentialStash = "Assets/Editor/CatProneReview/ExitCandidate/generic_cloud_credentials_build_stash.json";

        [MenuItem("CatLife/Build/Build Android APK")]
        public static void BuildApkFromMenu()
        {
            string outputPath = GetDefaultOutputPath();
            BuildApkInternal(outputPath, developmentBuild: true, exitOnComplete: false);
        }

        [MenuItem("CatLife/Build/Build Android Release APK")]
        public static void BuildReleaseApkFromMenu()
        {
            string outputPath = GetDefaultOutputPath();
            BuildApkInternal(outputPath, developmentBuild: false, exitOnComplete: false);
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

        public static void BuildExitReviewApk()
        {
            BuildExitReviewApk(ExitReviewScene);
        }

        public static void BuildTownAtlasReviewApk()
        {
            CatLifeTownAtlasReview.PrepareScene();
            BuildExitReviewApk(CatLifeTownAtlasReview.ScenePath);
        }

        public static void BuildSitSniffReviewApk()
        {
            BuildExitReviewApk(CatLifeSitSniffCandidate.ScenePath);
        }

        public static void BuildFigmaUiReviewApk()
        {
            BuildExitReviewApk(CatLifeFigmaUiInstaller.ScenePath, "com.catlife.review.ui", "CatLife UI Review");
        }

        public static void BuildNavigationTurnReviewApk()
        {
            BuildExitReviewApk("Assets/Editor/CatProneReview/NavigationTurnCandidate/CatLifeNavigationTurnReview.unity",
                "com.catlife.review.ui", "CatLife UI Review");
        }

        private static void BuildExitReviewApk(string scenePath, string reviewPackage = "com.catlife.review.wp04", string reviewProduct = "CatLife Exit Review")
        {
            string originalPackage = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string originalProduct = PlayerSettings.productName;
            bool success = false;
            bool hasPrivateConfig = File.Exists(PrivateCredentialAssetPath);
            bool privateLocalReview = HasArg("-privateLocalReview");
            if (privateLocalReview && !hasPrivateConfig) throw new InvalidOperationException("Private local review requires the local credential asset; no request fallback is used for this gate.");
            bool stashPrivateConfig = hasPrivateConfig && !privateLocalReview;
            if (stashPrivateConfig)
            {
                string moveError = AssetDatabase.MoveAsset(PrivateCredentialAssetPath, CredentialStash);
                if (moveError.Length != 0) throw new InvalidOperationException(moveError);
            }
            try
            {
                success = BuildApkInternal(GetArg("-outputPath"), developmentBuild: false, exitOnComplete: false, exitReview: true, reviewScenePath: scenePath, reviewPackage: reviewPackage, reviewProduct: reviewProduct, privateLocalReview: privateLocalReview);
            }
            finally
            {
                string restoreError = stashPrivateConfig ? AssetDatabase.MoveAsset(CredentialStash, PrivateCredentialAssetPath) : "";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalPackage);
                PlayerSettings.productName = originalProduct;
                AssetDatabase.SaveAssets();
                if (restoreError.Length != 0) throw new InvalidOperationException(restoreError);
                Debug.Log("EXIT_REVIEW_RESTORED original package/product and private credential asset; no credential value logged");
            }
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static bool BuildApkInternal(string outputPath, bool developmentBuild, bool exitOnComplete, bool exitReview = false, string reviewScenePath = ExitReviewScene, string reviewPackage = "com.catlife.review.wp04", string reviewProduct = "CatLife Exit Review", bool privateLocalReview = false)
        {
            outputPath = Path.GetFullPath(outputPath);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException("Android APK output directory could not be resolved.");
            }

            Directory.CreateDirectory(outputDirectory);
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            string evidenceRoot = ResolveEvidenceRoot(outputDirectory);
            Directory.CreateDirectory(evidenceRoot);
            WritePrivateCredentialEvidence(evidenceRoot);
            WriteText(Path.Combine(evidenceRoot, "distribution-policy.txt"), privateLocalReview ? "PRIVATE LOCAL ACCEPTANCE ONLY. Contains local credentials. Do not upload or share this APK." : "Credential-free review build. Asset redistribution rights remain separate.");

            try
            {
                string validation = exitReview ? CatLifeMobileBuildValidator.ValidateSceneReport(reviewScenePath) : CatLifeMobileBuildValidator.ValidateReport();
                WriteText(Path.Combine(evidenceRoot, "runtime-validator-before-build.txt"), validation);
                if (!validation.StartsWith("PASS", StringComparison.Ordinal))
                {
                    Debug.LogError(validation);
                    Complete(exitOnComplete, 1);
                    return false;
                }

                if (exitReview)
                {
                    var view = GameObject.Find("CatLifeMobileView");
                    if (view.GetComponent<CatLifeMobileTouchLayout>() == null || view.GetComponentInChildren<CatLifePawContactView>(true) == null)
                        throw new InvalidOperationException("Exit review requires its serialized touch layout and paw contact view.");
                    var animator = GameObject.Find("CatLifeMobileCat").GetComponent<Animator>();
                    foreach (string state in new[] { "CL_CAT_ProneExitReach_v01_24f", "CL_CAT_ProneExitHold_v01_loop_48f", "CL_CAT_ProneExitRelease_v01_24f" })
                        if (!animator.HasState(0, Animator.StringToHash("Base Layer." + state))) throw new InvalidOperationException("Exit review animation missing: " + state);
                }

                ConfigureAndroidPlayer(developmentBuild);
                if (exitReview)
                {
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, reviewPackage);
                    PlayerSettings.productName = reviewProduct;
                }

                string[] scenes = exitReview ? new[] { reviewScenePath } : ResolveBuildScenes();
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
                    if (exitReview)
                    {
                        var packed = report.packedAssets.SelectMany(asset => asset.contents).ToArray();
                        WriteText(Path.Combine(evidenceRoot, "packed-assets.tsv"), "bytes\tasset\n" + string.Join("\n", packed.OrderByDescending(asset => asset.packedSize).Select(asset => asset.packedSize + "\t" + asset.sourceAssetPath)));
                        if (packed.Any(asset => asset.sourceAssetPath == CredentialStash || (!privateLocalReview && asset.sourceAssetPath == PrivateCredentialAssetPath)))
                            throw new InvalidOperationException("Private credential asset must not enter the exit review APK.");
                        if (new FileInfo(outputPath).Length > 120L * 1024 * 1024) throw new InvalidOperationException("Exit review APK exceeds 120 MiB.");
                    }
                    WriteApkIdentity(evidenceRoot, outputPath);
                    Complete(exitOnComplete, 0);
                    return true;
                }

                Complete(exitOnComplete, 1);
                return false;
            }
            catch (Exception ex)
            {
                string error = ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace;
                Debug.LogError(error);
                WriteText(Path.Combine(evidenceRoot, "build-exception.txt"), error);
                Complete(exitOnComplete, 1);
                return false;
            }
        }

        private static void ConfigureAndroidPlayer(bool developmentBuild)
        {
            CatLifeBuildOptimizationPolicy.ApplyReleaseBuildSettings();
            EditorUserBuildSettings.development = developmentBuild;
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
                "Package name: " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                "Product name: " + PlayerSettings.productName,
                "Version: " + VersionName,
                "Version code: " + VersionCode,
                "Development build: " + developmentBuild,
                "Scripting backend: IL2CPP",
                "Target architecture: ARM64",
                "Texture compression: ASTC",
                "Min SDK: 28",
                "Build app bundle: false",
                "Minify release: " + PlayerSettings.Android.minifyRelease,
                "Managed stripping: " + PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android),
                "Strip engine code: " + PlayerSettings.stripEngineCode,
                "Private credential asset path: " + PrivateCredentialAssetPath,
                "Private credential value: REDACTED",
                "Scenes in build:",
                string.Join(Environment.NewLine, scenes)
            };

            WriteText(Path.Combine(evidenceRoot, "unity-build-settings.txt"), string.Join(Environment.NewLine, lines));
        }

        private static void WritePrivateCredentialEvidence(string evidenceRoot)
        {
            string configPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Resources/CatLifePrivate/generic_cloud_credentials.json"));
            bool exists = File.Exists(configPath);
            TextAsset resourcesConfig = Resources.Load<TextAsset>("CatLifePrivate/generic_cloud_credentials");
            bool resourcesLoadable = resourcesConfig != null;
            string[] lines =
            {
                "Private config path: " + PrivateCredentialAssetPath,
                "Exists: " + exists,
                "Unity Resources loadable: " + resourcesLoadable,
                "Unity Resources bytes: " + (resourcesLoadable ? resourcesConfig.bytes.Length.ToString() : "missing"),
                "Provider: mimo",
                "API key present: " + exists,
                "API key value: REDACTED"
            };

            WriteText(Path.Combine(evidenceRoot, "private_config_presence_redacted.txt"), string.Join(Environment.NewLine, lines));
        }

        private static void WriteApkIdentity(string evidenceRoot, string apkPath)
        {
            if (!File.Exists(apkPath))
            {
                WriteText(Path.Combine(evidenceRoot, "apk-identity.txt"), "APK file: " + apkPath + Environment.NewLine + "Size bytes: missing");
                return;
            }

            FileInfo file = new FileInfo(apkPath);
            string[] lines =
            {
                "APK file: " + apkPath,
                "Size bytes: " + file.Length,
                "Package name: " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                "Version: " + VersionName,
                "Version code: " + VersionCode
            };
            WriteText(Path.Combine(evidenceRoot, "apk-identity.txt"), string.Join(Environment.NewLine, lines));
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
            return Path.GetFullPath(Path.Combine(GetProjectRoot(), "build", "CatLife_Mobile_Android_v0.3.0.apk"));
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
