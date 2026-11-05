using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CatLife.Cat;

public static class CatLifeV12FootContactPreparation
{
    [Serializable] sealed class Data { public CatFootTerrainContact.SoleProxy[] soleProxies; }
    public static void Prepare()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MobileRuntime/Art/Cat/PF_CL_CAT_Runtime_v12.prefab");
        var skin = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh = skin.sharedMesh; var vertices = mesh.vertices; var binds = mesh.bindposes;
        string[] names = { "frontleg2", "R_frontleg2", "backleg2", "R_backleg2" };
        var indices = Array.ConvertAll(names, name => Array.FindIndex(skin.bones, b => b.name == name));
        var points = new List<Vector3>[4]; for (int f = 0; f < 4; f++) points[f] = new List<Vector3>();
        using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
        {
            int offset = 0;
            for (int v = 0; v < counts.Length; v++)
            {
                for (int f = 0; f < 4; f++)
                {
                    float sum = 0;
                    for (int j = 0; j < counts[v]; j++) if (weights[offset + j].boneIndex == indices[f]) sum += weights[offset + j].weight;
                    if (sum >= .99f) points[f].Add(binds[indices[f]].MultiplyPoint3x4(vertices[v]));
                }
                offset += counts[v];
            }
        }
        var data = new Data { soleProxies = Array.ConvertAll(points, p => new CatFootTerrainContact.SoleProxy { points = p.ToArray() }) };
        File.WriteAllText("Assets/Editor/CatProneReview/v12-foot-contact-proxies.json", JsonUtility.ToJson(data));
        AssetDatabase.Refresh();
        Debug.Log("V12_FOOT_PROXY_READY " + string.Join(",", Array.ConvertAll(points, p => p.Count)));
    }
    public static void Configure(GameObject cat)
    {
        var component = cat.GetComponent<CatFootTerrainContact>();
        if (component == null) component = cat.AddComponent<CatFootTerrainContact>();
        JsonUtility.FromJsonOverwrite(File.ReadAllText("Assets/Editor/CatProneReview/v12-foot-contact-proxies.json"), component);
        EditorUtility.SetDirty(component);
    }
    public static void Install()
    {
        const string scenePath = "Assets/Scenes/CatLifeMobile.unity";
        string backup = "../../Reports/cat-animation-v12/terrain-micro-contact-blends/before-install-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup); File.Copy(scenePath, Path.Combine(backup, "CatLifeMobile.unity"));
        var scene = EditorSceneManager.OpenScene(scenePath);
        Configure(GameObject.Find("CatLifeMobileCat"));
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("V12_FOOT_CONTACT_INSTALLED " + backup);
    }
}
