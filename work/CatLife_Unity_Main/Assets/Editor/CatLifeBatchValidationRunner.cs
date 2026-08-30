using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatLife.EditorTools
{
    public static class CatLifeBatchValidationRunner
    {
        private const string MenuPath = "CatLife/Runtime/Run Edit Mode Validation";

        [MenuItem(MenuPath)]
        public static void RunEditModeValidationFromMenu()
        {
            string report = RunEditModeValidationReport();
            if (report.StartsWith("PASS"))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }

        public static string RunEditModeValidationReport()
        {
            return CatLifeRuntimeAssemblyValidator.ValidateCurrentSceneReport();
        }

        public static bool RunEditModeValidation()
        {
            string report = RunEditModeValidationReport();
            bool passed = report.StartsWith("PASS");
            if (passed)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }

            return passed;
        }

        public static void RunEditModeValidationAndExit()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
            bool passed = RunEditModeValidation();
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
