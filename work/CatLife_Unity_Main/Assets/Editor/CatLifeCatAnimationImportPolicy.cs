using System;
using UnityEditor;

namespace CatLife.Editor
{
    public sealed class CatLifeCatAnimationImportPolicy : AssetPostprocessor
    {
        private const string RuntimeCatPath = "Assets/MobileRuntime/Art/Cat/Source/CL_CAT_Runtime.fbx";
        private const string StandIdleName = "CL_CAT_StandIdle_v01_loop_96f";

        public static void ReimportRuntimeCat()
        {
            AssetDatabase.ImportAsset(RuntimeCatPath, ImportAssetOptions.ForceUpdate);
        }

        private void OnPreprocessModel()
        {
            if (!string.Equals(assetPath, RuntimeCatPath, StringComparison.Ordinal)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importBlendShapes = true;
            importer.importBlendShapeNormals = ModelImporterNormals.Calculate;
        }

        private void OnPreprocessAnimation()
        {
            if (!string.Equals(assetPath, RuntimeCatPath, StringComparison.Ordinal)) return;

            ModelImporter importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string name = clip.takeName.Substring(clip.takeName.LastIndexOf('|') + 1);
                if (name == StandIdleName) clip.name = StandIdleName;
                clip.loopTime = IsLoopingTake(name);
            }
            importer.clipAnimations = clips;
        }

        private static bool IsLoopingTake(string name)
        {
            switch (name)
            {
                case StandIdleName:
                case "CL_CAT_CrouchIdleBreath_v01_loop_108f":
                case "CL_CAT_SitAlertLook_v01_loop_120f":
                case "CL_CAT_SitCuriousSniff_v01_loop_112f":
                case "CL_CAT_SitEarTwitchAlert_v01_loop_120f":
                case "CL_CAT_LieFocusRest_v01_loop_96f":
                case "CL_CAT_CrouchHeadShakeNo_v01_loop_108f":
                case "CL_CAT_CrouchHeadTiltListen_v01_loop_96f":
                case "CL_CAT_CrouchLookBack_v01_loop_112f":
                case "CL_CAT_CrouchPawWave_v01_loop_96f":
                case "CL_CAT_SitIdle_v01_loop_96f":
                case "CL_CAT_Walk_v01_loop_60f":
                case "CL_CAT_SitTailGreeting_v01_loop_96f":
                    return true;
                case "CL_CAT_StandStretchYawn_v01_144f":
                case "CL_CAT_LieFocusAttention_v01_48f":
                case "CL_CAT_SitToLie_v01_120f":
                case "CL_CAT_StandToSit_v01_72f":
                case "CL_CAT_LieToSit_v01_72f":
                    return false;
                default:
                    throw new InvalidOperationException("Unknown runtime cat animation take: " + name);
            }
        }
    }
}
