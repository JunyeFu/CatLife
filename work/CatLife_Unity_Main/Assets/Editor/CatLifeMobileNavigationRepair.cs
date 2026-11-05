using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class CatLifeMobileNavigationRepair
{
    const string ScenePath="Assets/Scenes/CatLifeMobile.unity";
    const string DataPath="Assets/MobileRuntime/Navigation/CL_NAV_Mobile.asset";
    static readonly HashSet<string> Ground=new HashSet<string>{
        "CL_ENV_IslandBase_01","CL_ENV_IslandGrass_01","CL_ROAD_CentralGardenRing_01",
        "CL_ROAD_PlazaPath_01","CL_ROAD_PlazaRing_01","CL_PROP_CenterStone_01"};

    public static void Install()
    {
        EditorSceneManager.OpenScene(ScenePath);
        string backup="Reports/ProneImportReview/nav-backup-"+System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath,backup+"/CatLifeMobile.unity");
        File.Copy(DataPath,backup+"/CL_NAV_Mobile.asset");
        var surface=Object.FindFirstObjectByType<NavMeshSurface>();
        var cat=GameObject.Find("CatLifeMobileCat");
        surface.minRegionArea=0;
        surface.BuildNavMesh();
        bool retained=NavMesh.SamplePosition(cat.transform.position,out var hit,1f,NavMesh.AllAreas);
        Debug.Log($"NAV_REGION_DIAG minRegionArea=0 foregroundRetained={retained} nearest={hit.position}");
        Bake(surface,GameObject.Find("CatLifeMobileTown"));
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        bool covered=NavMesh.SamplePosition(cat.transform.position,out hit,1f,NavMesh.AllAreas);
        Debug.Log($"NAV_REPAIR foregroundCovered={covered} nearest={hit.position} vertices={NavMesh.CalculateTriangulation().vertices.Length}");
        if(!covered) throw new System.InvalidOperationException("New navigation does not cover the cat anchor.");
    }

    public static void Bake(NavMeshSurface surface,GameObject town)
    {
        var sources=new List<NavMeshBuildSource>();
        foreach(var filter in town.GetComponentsInChildren<MeshFilter>())
        {
            if(filter.sharedMesh==null) continue;
            sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,
                sourceObject=filter.sharedMesh,transform=filter.transform.localToWorldMatrix,
                area=Ground.Contains(filter.name)?0:1});
        }
        var settings=NavMesh.GetSettingsByID(surface.agentTypeID);
        settings.agentRadius=.18f; settings.agentHeight=.55f; settings.agentClimb=.2f;
        settings.overrideVoxelSize=true; settings.voxelSize=.06f; settings.minRegionArea=0;
        var bounds=new Bounds(town.transform.position,Vector3.one*100f);
        var baked=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
        if(baked==null) throw new System.InvalidOperationException("Town navigation bake failed.");
        surface.RemoveData();
        var data=AssetDatabase.LoadAssetAtPath<NavMeshData>(DataPath);
        if(data==null) { AssetDatabase.CreateAsset(baked,DataPath); data=baked; }
        else { EditorUtility.CopySerialized(baked,data); Object.DestroyImmediate(baked); EditorUtility.SetDirty(data); }
        surface.navMeshData=data;
        surface.AddData();
        foreach(var box in surface.GetComponentsInChildren<BoxCollider>())
            if(box.name.StartsWith("Walk_")) box.enabled=false;
        EditorUtility.SetDirty(surface);
    }
}
