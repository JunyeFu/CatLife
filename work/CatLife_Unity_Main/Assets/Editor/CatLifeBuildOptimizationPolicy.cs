using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatLife.Editor
{
    public static class CatLifeBuildOptimizationPolicy
    {
        private const string PackageName = "com.catlife.mvp";
        private const string ProductName = "CatLife";
        private const string CompanyName = "CatLifeTeam";
        private const string VersionName = "0.3.0";
        private const int VersionCode = 3;
        private const string SplashTexturePath = "Assets/Resources/CatLifeSplash/CatLifeSplashLogo.png";

        [MenuItem("CatLife/Optimization/Stage 6/Audit Android Release Settings")]
        public static void AuditAndroidReleaseSettingsFromMenu()
        {
            string reportDirectory = CreateReportDirectory("stage6-android-release-audit");
            AuditAndroidReleaseSettings(reportDirectory);
            Debug.Log("[CatLifeOptimization] Stage 6 Android release settings audit exported: " + reportDirectory);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        [MenuItem("CatLife/Optimization/Stage 6/Apply Android Release Settings")]
        public static void ApplyAndroidReleaseSettingsFromMenu()
        {
            string reportDirectory = CreateReportDirectory("stage6-android-release-apply");
            ApplyReleaseBuildSettings();
            AuditAndroidReleaseSettings(reportDirectory);
            Debug.Log("[CatLifeOptimization] Stage 6 Android release settings applied: " + reportDirectory);
            EditorUtility.RevealInFinder(reportDirectory);
        }

        public static void AuditAndroidReleaseSettingsBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("stage6-android-release-audit");
            }

            try
            {
                AuditAndroidReleaseSettings(reportDirectory);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyAndroidReleaseSettingsBatch()
        {
            string reportDirectory = GetArg("-reportDir");
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = CreateReportDirectory("stage6-android-release-apply");
            }

            try
            {
                ApplyReleaseBuildSettings();
                AuditAndroidReleaseSettings(reportDirectory);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void ApplyReleaseBuildSettings()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
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
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.minifyRelease = true;
            PlayerSettings.Android.minifyDebug = false;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);
            ApplyCatLifeSplashScreenSettings();
            AssetDatabase.SaveAssets();
        }

        public static void ApplyCatLifeSplashScreenSettings()
        {
            Sprite splashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SplashTexturePath);
            if (splashSprite == null)
            {
                Debug.LogWarning("[CatLifeOptimization] Splash sprite missing: " + SplashTexturePath);
                return;
            }

            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = Color.white;
            PlayerSettings.SplashScreen.background = splashSprite;
            PlayerSettings.SplashScreen.backgroundPortrait = splashSprite;
            PlayerSettings.SplashScreen.blurBackgroundImage = false;
            PlayerSettings.SplashScreen.overlayOpacity = 1f;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.animationBackgroundZoom = 1f;
            PlayerSettings.SplashScreen.animationLogoZoom = 1f;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.logos = Array.Empty<PlayerSettings.SplashScreenLogo>();
            PlayerSettings.Android.splashScreenScale = AndroidSplashScreenScale.ScaleToFill;
        }

        public static void AuditAndroidReleaseSettings(string reportDirectory)
        {
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(Path.Combine(reportDirectory, "android_release_settings.md"), BuildSettingsMarkdown(), Encoding.UTF8);
        }

        public static string BuildSettingsMarkdown()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# CatLife Stage 6 Android Release Settings");
            sb.AppendLine();
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("| Setting | Value |");
            sb.AppendLine("|---|---|");
            AppendRow(sb, "Active build target", EditorUserBuildSettings.activeBuildTarget.ToString());
            AppendRow(sb, "Development build", EditorUserBuildSettings.development ? "true" : "false");
            AppendRow(sb, "Build app bundle", EditorUserBuildSettings.buildAppBundle ? "true" : "false");
            AppendRow(sb, "Android build system", EditorUserBuildSettings.androidBuildSystem.ToString());
            AppendRow(sb, "Android texture subtarget", EditorUserBuildSettings.androidBuildSubtarget.ToString());
            AppendRow(sb, "Application identifier", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            AppendRow(sb, "Product name", PlayerSettings.productName);
            AppendRow(sb, "Company name", PlayerSettings.companyName);
            AppendRow(sb, "Version", PlayerSettings.bundleVersion);
            AppendRow(sb, "Version code", PlayerSettings.Android.bundleVersionCode.ToString(CultureInfo.InvariantCulture));
            AppendRow(sb, "Min SDK", PlayerSettings.Android.minSdkVersion.ToString());
            AppendRow(sb, "Scripting backend", PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString());
            AppendRow(sb, "Target architectures", PlayerSettings.Android.targetArchitectures.ToString());
            AppendRow(sb, "Minify release", PlayerSettings.Android.minifyRelease ? "true" : "false");
            AppendRow(sb, "Minify debug", PlayerSettings.Android.minifyDebug ? "true" : "false");
            AppendRow(sb, "Strip engine code", PlayerSettings.stripEngineCode ? "true" : "false");
            AppendRow(sb, "Managed stripping Android", PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android).ToString());
            AppendRow(sb, "Splash screen texture", SplashTexturePath);
            AppendRow(sb, "Splash screen background", PlayerSettings.SplashScreen.background != null ? AssetDatabase.GetAssetPath(PlayerSettings.SplashScreen.background) : "missing");
            AppendRow(sb, "Splash screen portrait background", PlayerSettings.SplashScreen.backgroundPortrait != null ? AssetDatabase.GetAssetPath(PlayerSettings.SplashScreen.backgroundPortrait) : "missing");
            AppendRow(sb, "Splash screen background color", PlayerSettings.SplashScreen.backgroundColor.ToString());
            AppendRow(sb, "Splash screen Unity logo", PlayerSettings.SplashScreen.showUnityLogo ? "true" : "false");
            AppendRow(sb, "Android splash scale", PlayerSettings.Android.splashScreenScale.ToString());
            return sb.ToString();
        }

        private static void AppendRow(StringBuilder sb, string setting, string value)
        {
            sb.Append("| ");
            sb.Append(setting);
            sb.Append(" | ");
            sb.Append(value);
            sb.AppendLine(" |");
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
    }
}
