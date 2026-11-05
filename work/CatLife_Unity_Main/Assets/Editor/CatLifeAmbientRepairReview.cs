using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CatLifeAmbientRepairReview
{
    [Serializable] internal class SupportPoint { public string foot; public float x,y,z; }
    [Serializable] internal class SupportSet { public SupportPoint[] points; }
    internal static string SupportKey(string foot, Vector3 p) => $"{foot}:{Mathf.RoundToInt(p.x*1e7f)},{Mathf.RoundToInt(p.y*1e7f)},{Mathf.RoundToInt(p.z*1e7f)}";
    public static void Tail() => Review(false);
    public static void Paw() => Review(true);
    public static void Stretch() => Review(false,true);
    static void Review(bool paw,bool stretch=false)
    {
        const string folder="Assets/Editor/CatProneReview";
        string model=stretch?"CL_CAT_AmbientStretch_v12.fbx":paw?"CL_CAT_AmbientPaw_v12.fbx":"CL_CAT_AmbientTail_v12.fbx";
        string asset=folder+"/"+model;
        File.Copy("../../Reports/cat-animation-v12/"+(stretch?"stretch-staged-refined/":paw?"ambient-paw/":"ambient-tail-neutral/")+model,asset,true);
        AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(asset);
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.importNormals=ModelImporterNormals.Import;
        importer.importBlendShapeNormals=ModelImporterNormals.None;
        if(stretch)
        {
            importer.skinWeights=ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex=4;
            importer.minBoneWeight=.001f;
        }
        var sourceTake=importer.defaultClipAnimations[0];
        var take=sourceTake.takeName;
        Debug.Log($"AMBIENT_TAKE name={take} first={sourceTake.firstFrame} last={sourceTake.lastFrame}");
        int endFrame=stretch?288:144;
        float firstFrame=sourceTake.firstFrame;
        importer.clipAnimations=new[]{new ModelImporterClipAnimation {takeName=take,name=stretch?"CL_CAT_StandStretch_v12_288f":paw?"CL_CAT_StandPawWave_v12_144f":"CL_CAT_StandTailGreeting_v12_loop_144f",firstFrame=firstFrame,lastFrame=firstFrame+endFrame,loopTime=!paw&&!stretch}};
        importer.SaveAndReimport();
        var clip=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
        EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
        var cat=GameObject.Find("CatLifeMobileCat");
        cat.GetComponent<Animator>().enabled=false;
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        if(stretch)
        {
            var sourceSkin=AssetDatabase.LoadAssetAtPath<GameObject>(asset).GetComponentInChildren<SkinnedMeshRenderer>();
            var bones=skin.bones.ToDictionary(b=>b.name);
            skin.sharedMesh=sourceSkin.sharedMesh;
            skin.bones=sourceSkin.bones.Select(b=>bones[b.name]).ToArray();
            if(skin.sharedMesh.GetBlendShapeIndex("CL_CAT_StretchShoulderCorrective_v12")<0)
                throw new Exception("Stretch corrective was not imported.");
        }
        for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,0);
        var weights=skin.sharedMesh.boneWeights;
        int[] paws=Enumerable.Range(0,weights.Length).Where(i=>weights[i].weight0>.99f && skin.bones[weights[i].boneIndex0].name.EndsWith("leg2") && (!paw || skin.bones[weights[i].boneIndex0].name!="frontleg2") && (!stretch || skin.bones[weights[i].boneIndex0].name.Contains("backleg"))).ToArray();
        var importedPaws=paws;
        string supportDescription="imported dominant foot weight > .99";
        if(stretch)
        {
            var support=JsonUtility.FromJson<SupportSet>(File.ReadAllText("../../Reports/cat-animation-v12/stretch-staged-refined/"+model+".support.json"));
            var expected=support.points.Select(p=>SupportKey(p.foot,new Vector3(p.x,p.y,p.z))).ToHashSet();
            var vertices=skin.sharedMesh.vertices;
            var keys=Enumerable.Range(0,vertices.Length).Select(i=>SupportKey(skin.bones[weights[i].boneIndex0].name,vertices[i])).ToArray();
            paws=Enumerable.Range(0,vertices.Length).Where(i=>expected.Contains(keys[i])).ToArray();
            int matched=paws.Select(i=>keys[i]).Distinct().Count();
            if(matched!=expected.Count)throw new Exception($"Source support coverage mismatch: {matched}/{expected.Count}");
            supportDescription=$"source support unique positions matched {matched}/{expected.Count}; source vertices={support.points.Length}; imported vertices={paws.Length}";
        }
        var baked=new Mesh();
        var surface=new GameObject("IsolatedTailSample");
        surface.AddComponent<MeshFilter>().sharedMesh=baked;
        surface.AddComponent<MeshRenderer>().sharedMaterial=skin.sharedMaterial;
        skin.enabled=false;
        var camera=Camera.main;
        var target=new RenderTexture(540,1200,24);
        camera.targetTexture=target;
        string output="Reports/ProneImportReview/ambient-"+(stretch?"stretch":paw?"paw":"tail")+"-v12";
        Directory.CreateDirectory(output);
        Vector3[] initial=null;
        float maxPawDrift=0,loopError=0,importedSetDrift=0;
        string driftDetails="";
        var rows=new System.Text.StringBuilder("frame,min_paw_y,max_paw_drift\n");
        for(int frame=0;frame<=endFrame;frame++)
        {
            clip.SampleAnimation(cat,frame/60f);
            skin.BakeMesh(baked,false);
            var points=baked.vertices;
            if(initial==null)initial=points;
            float drift=paws.Max(i=>Vector3.Distance(points[i],initial[i]));
            importedSetDrift=Mathf.Max(importedSetDrift,importedPaws.Max(i=>Vector3.Distance(points[i],initial[i])));
            if(drift>maxPawDrift)
            {
                int vertex=paws.OrderByDescending(i=>Vector3.Distance(points[i],initial[i])).First();
                var w=weights[vertex];
                driftDetails=$"frame={frame}; vertex={vertex}; rest={skin.sharedMesh.vertices[vertex].ToString("G9")}; delta={(points[vertex]-initial[vertex]).ToString("G9")}; weights={skin.bones[w.boneIndex0].name}:{w.weight0:R},{skin.bones[w.boneIndex1].name}:{w.weight1:R},{skin.bones[w.boneIndex2].name}:{w.weight2:R},{skin.bones[w.boneIndex3].name}:{w.weight3:R}; skinQuality={skin.quality}; globalWeights={QualitySettings.skinWeights}; importerMinWeight={importer.minBoneWeight:R}; importerMaxBones={importer.maxBonesPerVertex}";
            }
            maxPawDrift=Mathf.Max(maxPawDrift,drift);
            rows.AppendLine(FormattableString.Invariant($"{frame},{paws.Min(i=>(skin.transform.position+skin.transform.rotation*points[i]).y)},{drift}"));
            if(frame==endFrame)loopError=points.Select((p,i)=>Vector3.Distance(p,initial[i])).Max();
            if(frame%12!=0)continue;
            surface.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
            camera.Render();RenderTexture.active=target;
            var image=new Texture2D(540,1200,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,540,1200),0,0);image.Apply();
            File.WriteAllBytes($"{output}/{frame:0000}.png",image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        File.WriteAllText(output+"/contact.csv",rows.ToString());
        File.WriteAllText(output+"/drift-details.txt",driftDetails);
        Debug.Log("AMBIENT_DRIFT "+driftDetails);
        File.WriteAllText(output+"/summary.txt",$"Imported seconds={clip.length}; frames={endFrame+1}; pawCount={paws.Length}; maxPawDrift={maxPawDrift}; loopError={loopError}; importedWeightSetDrift={importedSetDrift}; {supportDescription}. CPU skin sample on scene rig; stretch uses candidate mesh including corrective shapes; scene not saved.");
        if(paws.Length==0 || maxPawDrift>.001f || loopError>.001f)throw new Exception("Ambient import contact/loop mismatch.");
        RenderTexture.active=null;camera.targetTexture=null;target.Release();
        Debug.Log("AMBIENT_"+(stretch?"STRETCH":paw?"PAW":"TAIL")+"_IMPORT_PASS");
    }
}
