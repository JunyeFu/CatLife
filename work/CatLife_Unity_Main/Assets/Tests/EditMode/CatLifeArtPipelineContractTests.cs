using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CatLife.Mobile.Tests
{
    public sealed class CatLifeArtPipelineContractTests
    {
        private static string ManifestPath => Path.Combine(Application.dataPath, "MobileRuntime", "Art", "Town", "Catalog", "asset_manifest.csv");
        private static string TownFbxPath => Path.Combine(Application.dataPath, "MobileRuntime", "Art", "Town", "Source", "CL_TWN_Runtime.fbx");

        [Test]
        public void RuntimeManifestHasStableUniqueNamesAndCompleteIsland()
        {
            Assert.That(File.Exists(ManifestPath), Is.True, ManifestPath);
            string[] lines = File.ReadAllLines(ManifestPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            Assert.That(lines, Has.Length.EqualTo(168));
            Assert.That(lines[0].TrimStart('\uFEFF'), Is.EqualTo("asset_id,display_name_zh,runtime_name,category,source_file,source_object,source_version,mobile_policy,render_policy,triangle_budget,material_set,texture_set,landmark_id,status"));
            Assert.That(lines[0], Does.Not.Contain("hash").IgnoreCase);

            HashSet<string> assetIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> runtimeNames = new HashSet<string>(StringComparer.Ordinal);
            bool hasCompleteIsland = false;
            foreach (string line in lines.Skip(1))
            {
                string[] cells = line.Split(',');
                Assert.That(cells, Has.Length.EqualTo(14));
                Assert.That(assetIds.Add(cells[0]), Is.True, "Duplicate asset_id: " + cells[0]);
                Assert.That(runtimeNames.Add(cells[2]), Is.True, "Duplicate runtime_name: " + cells[2]);
                Assert.That(cells[2], Does.Not.StartWith("node_"));
                Assert.That(cells[2], Does.Not.StartWith("Mesh_"));
                if (cells[2] == "CL_ENV_IslandBase_01" && cells[12] == "ISLAND") hasCompleteIsland = true;
            }

            Assert.That(hasCompleteIsland, Is.True);
        }

        [Test]
        public void IslandSourceAndPrefabKeepTopSideAndBottomFaceAssignments()
        {
            foreach (string path in new[] {
                "Assets/MobileRuntime/Art/Town/Source/CL_TWN_Runtime.fbx",
                "Assets/MobileRuntime/Art/Town/PF_CL_TWN_Town.prefab" })
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderer = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Single(item => item.name == "CL_ENV_IslandBase_01");
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.subMeshCount, Is.EqualTo(3), path);
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(3), path);
                var expectedIndices = new Dictionary<string, uint> {
                    { "MAT_CL_SoilWarm", 576 }, { "MAT_CL_GrassSoftGreen", 1434 },
                    { "MAT_CL_IslandDarkBottom", 282 }
                };
                CollectionAssert.AreEquivalent(expectedIndices.Keys, renderer.sharedMaterials.Select(m => m.name));
                for (int i = 0; i < mesh.subMeshCount; i++)
                    Assert.That(mesh.GetIndexCount(i), Is.EqualTo(expectedIndices[renderer.sharedMaterials[i].name]), path);
            }
        }

        [Test]
        public void RuntimeTownUsesTheStandardizedFbxInterface()
        {
            Assert.That(File.Exists(TownFbxPath), Is.True, TownFbxPath);
            Assert.That(new FileInfo(TownFbxPath).Length, Is.LessThan(60L * 1024L * 1024L));
        }
    }
}
