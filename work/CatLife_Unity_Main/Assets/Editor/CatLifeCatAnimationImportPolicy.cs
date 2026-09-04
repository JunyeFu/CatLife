using System;
using System.Linq;
using UnityEditor;

namespace CatLife.Editor
{
    public sealed class CatLifeCatAnimationImportPolicy : AssetPostprocessor
    {
        private const string RuntimeCatPath = "Assets/MobileRuntime/Art/Cat/Source/CL_CAT_Runtime.fbx";
        private const string IdleBreathName = "CL_CAT_IdleBreath_v06_headsync_loop_108f";

        private void OnPreprocessAnimation()
        {
            if (!string.Equals(assetPath, RuntimeCatPath, StringComparison.Ordinal)) return;

            ModelImporter importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            ModelImporterClipAnimation idleBreath = clips.FirstOrDefault(clip => clip.takeName.Contains("IdleBreath_v06"));
            if (idleBreath == null) return;

            idleBreath.name = IdleBreathName;
            importer.clipAnimations = clips;
        }
    }
}
