using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CatLifeGroundingExportReview
{
    public static void ProneWeightProbeUnlimited()
    {
        var previous=QualitySettings.skinWeights;
        try { QualitySettings.skinWeights=SkinWeights.Unlimited; ProneWeightProbe(); }
        finally { QualitySettings.skinWeights=previous; }
    }
    public static void ProneWeightProbe()
    {
        const string source="Assets/Editor/CatProneReview/CL_CAT_CommonSitProne_v12.fbx";
        const string probe="Assets/Editor/CatProneReview/CL_CAT_CommonWeightProbe_v12.fbx";
        bool unlimited=QualitySettings.skinWeights==SkinWeights.Unlimited;
        if(!unlimited)
        {
        File.Copy(source,probe,true);
        AssetDatabase.ImportAsset(probe,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(probe);
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.importNormals=ModelImporterNormals.Import;
        importer.importBlendShapeNormals=ModelImporterNormals.None;
        importer.skinWeights=ModelImporterSkinWeights.Custom;
        importer.maxBonesPerVertex=32;
        importer.minBoneWeight=0;
        importer.SaveAndReimport();
        }
        var report=new System.Text.StringBuilder("Isolated import influence comparison; no production change.\n");
        foreach(string asset in new[]{source,probe})
        {
            var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset));
            cat.transform.localScale=Vector3.one*.045f;
            var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
            var clip=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
            var baked=new Mesh();
            using(var counts=skin.sharedMesh.GetBonesPerVertex())
            using(var allWeights=skin.sharedMesh.GetAllBoneWeights())
            {
                report.AppendLine(asset+" maximumStoredInfluences="+counts.ToArray().Max());
                foreach(var quality in new[]{SkinQuality.Bone4,SkinQuality.Auto})
                {
                    skin.quality=quality;
                    foreach(int frame in new[]{0,30,90,150,240,330,480,600})
                    {
                        for(int s=0;s<skin.sharedMesh.blendShapeCount;s++)skin.SetBlendShapeWeight(s,0);
                        clip.SampleAnimation(cat,frame/60f);skin.BakeMesh(baked,false);
                        var points=baked.vertices.Select(v=>skin.transform.position+skin.transform.rotation*v).ToArray();
                        int low=Enumerable.Range(0,points.Length).OrderBy(i=>points[i].y).First();
                        int offset=0;for(int i=0;i<low;i++)offset+=counts[i];
                        var influences=Enumerable.Range(0,counts[low]).Select(i=>allWeights[offset+i])
                            .Select(w=>skin.bones[w.boneIndex].name+":"+w.weight.ToString("R"));
                        report.AppendLine(System.FormattableString.Invariant($"quality={quality} global={QualitySettings.skinWeights} frame={frame} minimum={points[low].y:R} vertex={low} rest={skin.sharedMesh.vertices[low]:F8} weights={string.Join(";",influences)}"));
                    }
                }
            }
            Object.DestroyImmediate(baked);Object.DestroyImmediate(cat);
        }
        File.WriteAllText("../../Reports/cat-animation-v12/prone-weight-probe"+(unlimited?"-unlimited":"")+".txt",report.ToString());
        Debug.Log("PRONE_WEIGHT_PROBE_FINISHED\n"+report);
    }
    [System.Serializable] class ContactSample { public int frame; public Vector3 position, bonePosition; public float[] boneMatrix; }
    [System.Serializable] class ContactPoint { public int vertex; public string bone; public float weight; public Vector3 local; public ContactSample[] samples; }
    [System.Serializable] class ContactReport { public ContactPoint[] points; }
    public static void StretchContactPoints()
    {
        const string asset="Assets/Editor/CatProneReview/CL_CAT_Stretch_unified_v12.fbx";
        var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset));
        cat.transform.localScale=Vector3.one*.045f;
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var clip=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        var mesh=new Mesh();var weights=skin.sharedMesh.boneWeights;
        var report=new ContactReport{points=new[]{68602,27644}.Select(i=>new ContactPoint{vertex=i,local=skin.sharedMesh.vertices[i],bone=skin.bones[weights[i].boneIndex0].name,weight=weights[i].weight0,samples=new ContactSample[3]}).ToArray()};
        int[] frames={0,189,190};
        for(int sample=0;sample<frames.Length;sample++)
        {
            clip.SampleAnimation(cat,frames[sample]/60f);skin.BakeMesh(mesh,false);var vertices=mesh.vertices;
            foreach(var point in report.points)
            {
                var bone=skin.bones[weights[point.vertex].boneIndex0];
                point.samples[sample]=new ContactSample{frame=frames[sample],position=skin.transform.position+skin.transform.rotation*vertices[point.vertex],bonePosition=bone.position,boneMatrix=Enumerable.Range(0,16).Select(i=>bone.localToWorldMatrix[i]).ToArray()};
            }
        }
        File.WriteAllText("../../Reports/cat-animation-v12/stretch-contact-points.json",JsonUtility.ToJson(report,true));
        Object.DestroyImmediate(mesh);Object.DestroyImmediate(cat);
        Debug.Log("STRETCH_CONTACT_POINTS_SAMPLED");
    }
    public static void Locomotion()
    {
        AuditMotions(new[]{"CL_CAT_StandIdle_contact_v12","CL_CAT_Walk_contact_v12"}, "locomotion-unity-summary");
    }
    public static void AmbientUnified()
    {
        AuditMotions(new[]{"CL_CAT_Tail_unified_v12","CL_CAT_Paw_unified_v12"}, "ambient-unified-summary");
    }
    public static void StretchUnified()
    {
        AuditMotions(new[]{"CL_CAT_Stretch_unified_v12"}, "stretch-unified-summary");
    }
    public static void StretchFlat()
    {
        AuditMotions(new[]{"CL_CAT_Stretch_flat_probe_v12"}, "stretch-flat-summary");
    }
    public static void CommonRuntime()
    {
        AuditCommonRuntime(false);
    }
    public static void CommonSitting()
    {
        AuditCommonRuntime(true);
        RenderCommonSurface(true);
    }
    public static void CommonSittingFeedback()
    {
        AuditCommonRuntime(true,true);
        RenderCommonSurface(true,true);
    }
    public static void CommonCrouching()
    {
        AuditCommonRuntime(true,true,true);
        RenderCommonSurface(true,true,true);
    }
    public static void CommonFullWeights()
    {
        var previous=QualitySettings.skinWeights;
        try
        {
            QualitySettings.skinWeights=SkinWeights.Unlimited;
            if(System.Environment.GetCommandLineArgs().Contains("-correctNormals"))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Editor/CatProneReview/CL_CAT_CommonWeightProbe_v12.fbx");
                importer.importNormals=ModelImporterNormals.Import;
                importer.importBlendShapeNormals=ModelImporterNormals.None;
                importer.SaveAndReimport();
            }
            AuditCommonRuntime(true,true,true);
            RenderCommonSurface(true,true,true);
            if(System.Environment.GetCommandLineArgs().Contains("-buildController"))CatLifeV12ControllerCandidate.BuildFullWeights();
        }
        finally { QualitySettings.skinWeights=previous; }
    }
    static void AuditCommonRuntime(bool sitting, bool feedback=false, bool crouching=false)
    {
        bool fullWeights=System.Environment.GetCommandLineArgs().Contains("-fullWeights");
        bool sitProne=crouching && (fullWeights || System.Environment.GetCommandLineArgs().Contains("-sitProne"));
        bool lookback=sitProne || crouching && System.Environment.GetCommandLineArgs().Contains("-lookback");
        bool semantic=crouching && !lookback && System.Environment.GetCommandLineArgs().Contains("-crouchSemantic");
        string[] names={"CL_CAT_Common_v12","CL_CAT_StandIdle_runtime_v12","CL_CAT_Walk_runtime_v12","CL_CAT_Tail_runtime_v12","CL_CAT_Paw_runtime_v12","CL_CAT_Stretch_runtime_v12"};
        if(sitting)
        {
            names[0]="CL_CAT_CommonSit_v12";
            names=names.Concat(new[]{"CL_CAT_StandToSit_runtime_v12","CL_CAT_SitIdle_runtime_v12"}).ToArray();
        }
        string reportName=sitting?"common-sitting":"common-runtime";
        if(feedback)
        {
            names=names.Concat(new[]{"CL_CAT_SitAlertLook_runtime_v12","CL_CAT_SitCuriousSniff_runtime_v12","CL_CAT_SitEarTwitchAlert_runtime_v12"}).ToArray();
            reportName="common-sitting-feedback";
        }
        if(crouching)
        {
            names[0]="CL_CAT_CommonCrouch_v12";
            names=names.Concat(new[]{"CL_CAT_CrouchIdle_runtime_v12","CL_CAT_CrouchHeadShakeNo_runtime_v12","CL_CAT_CrouchHeadTiltListen_runtime_v12"}).ToArray();
            reportName="common-crouching";
        }
        var clips=new System.Collections.Generic.Dictionary<string,AnimationClip>();
        if(semantic || lookback)
        {
            names[12]="CL_CAT_CrouchHeadShakeNo_semantic_v12";
            names[13]="CL_CAT_CrouchHeadTiltListen_semantic_v12";
            reportName="common-crouching-semantic";
        }
        if(lookback)
        {
            names[0]="CL_CAT_CommonLookBack_v12";
            names=names.Concat(new[]{"CL_CAT_CrouchLookBack_runtime_v12"}).ToArray();
            reportName="common-lookback";
        }
        if(sitProne)
        {
            names[0]="CL_CAT_CommonSitProne_v12";
            names=names.Concat(new[]{"CL_CAT_SitToLie_runtime_v12","CL_CAT_LieToSit_runtime_v12"}).ToArray();
            reportName="common-sit-prone";
        }
        if(fullWeights){names[0]="CL_CAT_CommonWeightProbe_v12";reportName="common-fullweights";}
        if(fullWeights && System.Environment.GetCommandLineArgs().Contains("-correctNormals"))reportName+="-normals";
        foreach(string name in names)
        {
            string asset="Assets/Editor/CatProneReview/"+name+".fbx";
            if(!fullWeights && (sitProne?(name.Contains("CommonSitProne") || name.Contains("SitToLie") || name.Contains("LieToSit")):lookback?name.Contains("LookBack"):!semantic || name.Contains("_semantic_")))
            {
            File.Copy("../../Reports/cat-animation-v12/grounding-export-check/"+name+".fbx",asset,true);
            AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(asset);
            importer.animationType=ModelImporterAnimationType.Generic;importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importNormals=ModelImporterNormals.Import;
            importer.importBlendShapeNormals=ModelImporterNormals.None;importer.SaveAndReimport();
            }
            clips[name]=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/CatProneReview/"+names[0]+".fbx"));
        cat.transform.localScale=Vector3.one*.045f;
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
        if(fullWeights)skin.quality=SkinQuality.Auto;
        var weights=skin.sharedMesh.boneWeights;
        var feet=new[]{"frontleg2","R_frontleg2","backleg2","R_backleg2"};
        var groups=feet.Select(foot=>Enumerable.Range(0,weights.Length).Where(i=>weights[i].weight0>.99f && skin.bones[weights[i].boneIndex0].name==foot).ToArray()).ToArray();
        var summary=new System.Text.StringBuilder($"One shared mesh; {(semantic?3:names.Length)} sampled takes, CPU sampling only. Not a controller or terrain pass.\n");
        var rows=new System.Text.StringBuilder("clip,frame,mesh_min,front_left,front_right,rear_left,rear_right\n");
        Vector3[] Sample(AnimationClip clip,float time)
        {
            clip.SampleAnimation(cat,time);skin.BakeMesh(baked,false);
            return baked.vertices.Select(v=>skin.transform.position+skin.transform.rotation*v).ToArray();
        }
        void ClearShapes(){for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,0);}
        ClearShapes();var standing=Sample(clips[names[1]],0);
        foreach(string name in semantic?names.Skip(11):names)
        {
            var clip=clips[name];
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if(binding.path!="" && cat.transform.Find(binding.path)==null)throw new System.Exception("Unresolved runtime path: "+binding.path);
                if(binding.type==typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.") && skin.sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(11))<0)
                    throw new System.Exception("Missing common corrective: "+binding.propertyName);
            }
            ClearShapes();Vector3[] first=null,last=null;
            float minimum=float.PositiveInfinity,maxStep=0;int worst=0;
            int end=Mathf.RoundToInt(clip.length*60);
            for(int frame=0;frame<=end;frame++)
            {
                var points=Sample(clip,frame/60f);if(first==null)first=points;
                float low=points.Min(p=>p.y);if(low<minimum){minimum=low;worst=frame;}
                if(last!=null)maxStep=Mathf.Max(maxStep,points.Select((p,i)=>Vector3.Distance(p,last[i])).Max());
                last=points;var soles=groups.Select(ids=>ids.Min(i=>points[i].y)).ToArray();
                rows.AppendLine(System.FormattableString.Invariant($"{name},{frame},{low:R},{soles[0]:R},{soles[1]:R},{soles[2]:R},{soles[3]:R}"));
            }
            summary.AppendLine($"{name} frames={end+1}; minimum={minimum:R}; worst={worst}; endpointSpan={first.Select((p,i)=>Vector3.Distance(p,last[i])).Max():R}; largestFrameStep={maxStep:R}");
            if(name==names[0])
            {
                var rise=Sample(clip,150/60f);
                summary.AppendLine($"Rise -> real Stand max={rise.Select((p,i)=>Vector3.Distance(p,standing[i])).Max():R}");
            }
            else if(name!=names[2])summary.AppendLine($"Stand -> {name} first max={first.Select((p,i)=>Vector3.Distance(p,standing[i])).Max():R}");
        }
        if(sitting && !semantic)
        {
            var down=clips["CL_CAT_StandToSit_runtime_v12"];
            ClearShapes();var endSit=Sample(down,down.length);
            ClearShapes();var idleSit=Sample(clips["CL_CAT_SitIdle_runtime_v12"],0);
            summary.AppendLine($"StandToSit -> SitIdle max={endSit.Select((p,i)=>Vector3.Distance(p,idleSit[i])).Max():R}");
            if(feedback)
                foreach(string name in names.Skip(8).Take(3))
                {
                    ClearShapes();var begin=Sample(clips[name],0);
                    summary.AppendLine($"SitIdle -> {name} first max={begin.Select((p,i)=>Vector3.Distance(p,idleSit[i])).Max():R}");
                }
        }
        if(crouching)
        {
            ClearShapes();var idleCrouch=Sample(clips["CL_CAT_CrouchIdle_runtime_v12"],0);
            foreach(string name in names.Skip(12).Take(lookback?3:2))
            {
                ClearShapes();var begin=Sample(clips[name],0);
                summary.AppendLine($"CrouchIdle -> {name} first max={begin.Select((p,i)=>Vector3.Distance(p,idleCrouch[i])).Max():R}");
            }
        }
        if(sitProne)
        {
            ClearShapes();var idleSit=Sample(clips["CL_CAT_SitIdle_runtime_v12"],0);
            ClearShapes();var idleProne=Sample(clips[names[0]],0);
            foreach(string name in names.Skip(15))
            {
                bool down=name.Contains("SitToLie");
                var expectedStart=down?idleSit:idleProne;
                var expectedEnd=down?idleProne:idleSit;
                ClearShapes();var first=Sample(clips[name],0);
                var last=Sample(clips[name],clips[name].length);
                summary.AppendLine($"{name} boundary start={first.Select((p,i)=>Vector3.Distance(p,expectedStart[i])).Max():R}; end={last.Select((p,i)=>Vector3.Distance(p,expectedEnd[i])).Max():R}");
            }
        }
        ClearShapes();Sample(clips[names[1]],0);
        PrefabUtility.SaveAsPrefabAsset(cat,"Assets/Editor/CatProneReview/"+(fullWeights?"PF_CL_CAT_CommonFullWeights_v12":sitProne?"PF_CL_CAT_CommonSitProne_v12":crouching?"PF_CL_CAT_CommonCrouching_v12":sitting?"PF_CL_CAT_CommonSitting_v12":"PF_CL_CAT_CommonRuntime_v12")+".prefab");
        File.WriteAllText("../../Reports/cat-animation-v12/"+reportName+"-summary.txt",summary.ToString());
        File.WriteAllText("../../Reports/cat-animation-v12/"+reportName+"-frames.csv",rows.ToString());
        Object.DestroyImmediate(baked);Object.DestroyImmediate(cat);
        Debug.Log("COMMON_RUNTIME_CPU_FINISHED\n"+summary);
    }
    public static void CommonRuntimeSurface()
    {
        RenderCommonSurface(false);
    }
    static void RenderCommonSurface(bool sitting, bool feedback=false, bool crouching=false)
    {
        bool fullWeights=System.Environment.GetCommandLineArgs().Contains("-fullWeights");
        bool sitProne=crouching && (fullWeights || System.Environment.GetCommandLineArgs().Contains("-sitProne"));
        bool lookback=crouching && System.Environment.GetCommandLineArgs().Contains("-lookback");
        bool semantic=crouching && System.Environment.GetCommandLineArgs().Contains("-crouchSemantic");
        EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
        var material=GameObject.Find("CatLifeMobileCat").GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/CatProneReview/"+(fullWeights?"PF_CL_CAT_CommonFullWeights_v12":sitProne?"PF_CL_CAT_CommonSitProne_v12":crouching?"PF_CL_CAT_CommonCrouching_v12":sitting?"PF_CL_CAT_CommonSitting_v12":"PF_CL_CAT_CommonRuntime_v12")+".prefab"));
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();skin.enabled=false;
        var surface=new GameObject("Common candidate CPU skin");
        var filter=surface.AddComponent<MeshFilter>();surface.AddComponent<MeshRenderer>().sharedMaterial=material;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.localScale=Vector3.one*.5f;
        var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMaterial.SetColor("_BaseColor",new Color(.32f,.36f,.32f));ground.GetComponent<MeshRenderer>().sharedMaterial=floorMaterial;
        var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(40,-30,0);
        RenderSettings.ambientLight=Color.gray;
        var camera=new GameObject("Review camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.05f;
        if(sitting)camera.orthographicSize=1.15f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.19f,.2f);camera.nearClipPlane=.01f;camera.farClipPlane=100;
        var target=new RenderTexture(1000,800,24);camera.targetTexture=target;
        string output="../../Reports/cat-animation-v12/"+(sitting?"common-sitting-surface":"common-runtime-surface");Directory.CreateDirectory(output);
        if(feedback){output="../../Reports/cat-animation-v12/common-sitting-feedback-surface";Directory.CreateDirectory(output);}
        if(crouching){output="../../Reports/cat-animation-v12/common-crouching-surface";Directory.CreateDirectory(output);}
        if(semantic){output="../../Reports/cat-animation-v12/common-crouching-semantic-surface";Directory.CreateDirectory(output);}
        if(lookback){output="../../Reports/cat-animation-v12/common-lookback-surface";Directory.CreateDirectory(output);}
        if(sitProne){output="../../Reports/cat-animation-v12/common-sit-prone-surface";Directory.CreateDirectory(output);}
        if(fullWeights){output="../../Reports/cat-animation-v12/common-fullweights-surface";Directory.CreateDirectory(output);}
        if(fullWeights && System.Environment.GetCommandLineArgs().Contains("-correctNormals")){output="../../Reports/cat-animation-v12/common-fullweights-normals-surface";Directory.CreateDirectory(output);}
        var samples=new[]{("CL_CAT_Common_v12",0),("CL_CAT_Common_v12",90),("CL_CAT_Common_v12",150),("CL_CAT_Stretch_runtime_v12",109),("CL_CAT_Stretch_runtime_v12",190),("CL_CAT_Walk_runtime_v12",20)};
        if(sitting)samples=new[]{("CL_CAT_StandToSit_runtime_v12",0),("CL_CAT_StandToSit_runtime_v12",20),("CL_CAT_StandToSit_runtime_v12",36),("CL_CAT_StandToSit_runtime_v12",60),("CL_CAT_SitIdle_runtime_v12",0),("CL_CAT_SitIdle_runtime_v12",48)};
        if(feedback)samples=new[]{("CL_CAT_SitAlertLook_runtime_v12",33),("CL_CAT_SitAlertLook_runtime_v12",75),("CL_CAT_SitCuriousSniff_runtime_v12",40),("CL_CAT_SitCuriousSniff_runtime_v12",54),("CL_CAT_SitEarTwitchAlert_runtime_v12",23),("CL_CAT_SitEarTwitchAlert_runtime_v12",47)};
        if(crouching)samples=new[]{("CL_CAT_CrouchIdle_runtime_v12",0),("CL_CAT_CrouchIdle_runtime_v12",54),("CL_CAT_CrouchHeadShakeNo_runtime_v12",27),("CL_CAT_CrouchHeadShakeNo_runtime_v12",54),("CL_CAT_CrouchHeadTiltListen_runtime_v12",32),("CL_CAT_CrouchHeadTiltListen_runtime_v12",58)};
        if(semantic)samples=new[]{("CL_CAT_CrouchIdle_runtime_v12",0),("CL_CAT_CrouchIdle_runtime_v12",54),("CL_CAT_CrouchHeadShakeNo_semantic_v12",23),("CL_CAT_CrouchHeadShakeNo_semantic_v12",43),("CL_CAT_CrouchHeadTiltListen_semantic_v12",31),("CL_CAT_CrouchHeadTiltListen_semantic_v12",57)};
        if(lookback)samples=new[]{("CL_CAT_CrouchLookBack_runtime_v12",0),("CL_CAT_CrouchLookBack_runtime_v12",21),("CL_CAT_CrouchLookBack_runtime_v12",31),("CL_CAT_CrouchLookBack_runtime_v12",41),("CL_CAT_CrouchLookBack_runtime_v12",79),("CL_CAT_CrouchLookBack_runtime_v12",99)};
        if(sitProne)samples=Enumerable.Range(0,7).Select(i=>("CL_CAT_SitToLie_runtime_v12",i*20)).Concat(Enumerable.Range(0,7).Select(i=>("CL_CAT_LieToSit_runtime_v12",i*16))).ToArray();
        var detail=new System.Text.StringBuilder("Actual scene material; CPU mesh on isolated plane. Not live controller or town terrain.\n");
        var weights=skin.sharedMesh.boneWeights;
        foreach(var sample in samples)
        {
            for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,0);
            var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/Editor/CatProneReview/"+sample.Item1+".fbx").OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
            clip.SampleAnimation(cat,sample.Item2/60f);var mesh=new Mesh();skin.BakeMesh(mesh,false);filter.sharedMesh=mesh;
            surface.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
            var points=mesh.vertices.Select(v=>skin.transform.position+skin.transform.rotation*v).ToArray();
            int lowest=Enumerable.Range(0,points.Length).OrderBy(i=>points[i].y).First();
            detail.AppendLine($"{sample.Item1} frame={sample.Item2}; minimum={points[lowest].y:R}; vertex={lowest}; bone={skin.bones[weights[lowest].boneIndex0].name}; weight={weights[lowest].weight0:R}");
            for(int view=0;view<2;view++)
            {
                var center=new Vector3(0,sitting?.9f:.7f,0);
                camera.transform.position=center+(view==0?new Vector3(2,.4f,3):new Vector3(3,.1f,0));camera.transform.LookAt(center);
                camera.Render();RenderTexture.active=target;
                var texture=new Texture2D(1000,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1000,800),0,0);texture.Apply();
                File.WriteAllBytes($"{output}/{sample.Item1}-F{sample.Item2:000}-view{view}.png",texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            Object.DestroyImmediate(mesh);
        }
        File.WriteAllText(output+"/samples.txt",detail.ToString());
        RenderTexture.active=null;camera.targetTexture=null;target.Release();
        Debug.Log("COMMON_RUNTIME_SURFACE_FINISHED\n"+detail);
    }
    static void AuditMotions(string[] names, string reportName)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var summary=new System.Text.StringBuilder("Isolated CPU samples; not controller, navigation or terrain acceptance.\n");
        foreach(string name in names)
        {
            string asset="Assets/Editor/CatProneReview/"+name+".fbx";
            File.Copy("../../Reports/cat-animation-v12/grounding-export-check/"+name+".fbx",asset,true);
            AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(asset);
            importer.animationType=ModelImporterAnimationType.Generic;importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importNormals=ModelImporterNormals.Import;
            importer.importBlendShapeNormals=ModelImporterNormals.None;importer.SaveAndReimport();
            var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset));cat.transform.localScale=Vector3.one*.045f;
            var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
            var clip=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            var mesh=new Mesh();var weights=skin.sharedMesh.boneWeights;
            var groups=new[]{"frontleg2","R_frontleg2","backleg2","R_backleg2"}.Select(foot=>Enumerable.Range(0,weights.Length).Where(i=>weights[i].weight0>.99f && skin.bones[weights[i].boneIndex0].name==foot).ToArray()).ToArray();
            int[] sourceSupport=System.Array.Empty<int>();float sourceSupportDrift=0;
            if(name.Contains("Stretch"))
            {
                var support=JsonUtility.FromJson<CatLifeAmbientRepairReview.SupportSet>(File.ReadAllText("../../Reports/cat-animation-v12/grounding-export-check/"+name+".fbx.support.json"));
                var expected=support.points.Select(p=>CatLifeAmbientRepairReview.SupportKey(p.foot,new Vector3(p.x,p.y,p.z))).ToHashSet();
                var rest=skin.sharedMesh.vertices;
                var keys=Enumerable.Range(0,rest.Length).Select(i=>CatLifeAmbientRepairReview.SupportKey(skin.bones[weights[i].boneIndex0].name,rest[i])).ToArray();
                sourceSupport=Enumerable.Range(0,rest.Length).Where(i=>expected.Contains(keys[i])).ToArray();
                int matched=sourceSupport.Select(i=>keys[i]).Distinct().Count();
                if(matched!=expected.Count)throw new System.Exception($"Source support coverage mismatch: {matched}/{expected.Count}");
                summary.AppendLine($"source support matched={matched}/{expected.Count}; imported sample vertices={sourceSupport.Length}");
            }
            Vector3[] first=null,last=null;float minimum=float.PositiveInfinity, motion=0;int worst=0;
            float supportDrift=0; int supportFrame=0, supportVertex=0, minimumVertex=0;
            var rows=new System.Text.StringBuilder("frame,mesh_min,front_left,front_right,rear_left,rear_right\n");
            int end=Mathf.RoundToInt(clip.length*60);
            for(int frame=0;frame<=end;frame++)
            {
                clip.SampleAnimation(cat,frame/60f);skin.BakeMesh(mesh,false);last=mesh.vertices;
                for(int i=0;i<last.Length;i++)last[i]=skin.transform.position+skin.transform.rotation*last[i];
                if(first==null)first=(Vector3[])last.Clone();
                float low=last.Min(v=>v.y);if(low<minimum){minimum=low;worst=frame;minimumVertex=System.Array.FindIndex(last,v=>v.y==low);}
                for(int i=0;i<last.Length;i++)motion=Mathf.Max(motion,Vector3.Distance(first[i],last[i]));
                if(name.Contains("unified") || name.Contains("Stretch"))
                    foreach(var ids in groups.Skip(name.Contains("Stretch") ? 2 : name.Contains("Paw") ? 1 : 0))
                        foreach(int i in ids)
                        {
                            float drift=Vector3.Distance(first[i],last[i]);
                            if(drift>supportDrift){supportDrift=drift;supportFrame=frame;supportVertex=i;}
                        }
                foreach(int i in sourceSupport)sourceSupportDrift=Mathf.Max(sourceSupportDrift,Vector3.Distance(first[i],last[i]));
                var soles=groups.Select(ids=>ids.Min(i=>last[i].y)).ToArray();
                rows.AppendLine(System.FormattableString.Invariant($"{frame},{low:R},{soles[0]:R},{soles[1]:R},{soles[2]:R},{soles[3]:R}"));
            }
            float loop=first.Select((p,i)=>Vector3.Distance(p,last[i])).Max();
            summary.AppendLine($"{name} frames={end+1}; minimum={minimum:R}; worst={worst}; loop={loop:R}; motion={motion:R}");
            summary.AppendLine($"minimum vertex={minimumVertex}; dominantBone={skin.bones[weights[minimumVertex].boneIndex0].name}; weight={weights[minimumVertex].weight0:R}; supportFrame={supportFrame}; supportVertex={supportVertex}; supportBone={skin.bones[weights[supportVertex].boneIndex0].name}; supportWeight={weights[supportVertex].weight0:R}");
            if(name.Contains("Stretch"))summary.AppendLine($"imported support drift={supportDrift:R}; matched source support drift={sourceSupportDrift:R}");
            File.WriteAllText("../../Reports/cat-animation-v12/"+name+"-unity.csv",rows.ToString());
            if(name.Contains("unified"))
            {
                var stand=AssetDatabase.LoadAllAssetsAtPath("Assets/Editor/CatProneReview/CL_CAT_StandIdle_contact_v12.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                stand.SampleAnimation(cat,0);skin.BakeMesh(mesh,false);var endpoint=mesh.vertices;
                float difference=first.Select((p,i)=>Vector3.Distance(p,skin.transform.position+skin.transform.rotation*endpoint[i])).Max();
                summary.AppendLine($"StandIdle -> ambient first frame max={difference:R}; supportDrift={supportDrift:R}");
            }
            if(name.Contains("StandIdle"))
            {
                var rise=AssetDatabase.LoadAllAssetsAtPath("Assets/Editor/CatProneReview/CL_CAT_Prone_unified_v12.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                rise.SampleAnimation(cat,150/60f);skin.BakeMesh(mesh,false);var endpoint=mesh.vertices;
                float difference=first.Select((p,i)=>Vector3.Distance(p,skin.transform.position+skin.transform.rotation*endpoint[i])).Max();
                summary.AppendLine($"Rise to real StandIdle first frame max={difference:R}");
            }
            Object.DestroyImmediate(mesh);Object.DestroyImmediate(cat);
        }
        File.WriteAllText("../../Reports/cat-animation-v12/"+reportName+".txt",summary.ToString());
        Debug.Log("LOCOMOTION_CPU_AUDIT_FINISHED\n"+summary);
    }
    public static void Surface()
    {
        Surface("Assets/Editor/CatProneReview/CL_CAT_Prone_unified_v12.fbx", "../../Reports/cat-animation-v12/unity-surface-comparison");
    }
    public static void Chest()
    {
        Clearance("CL_CAT_Prone_chest_v12", "unity-chest", "Assets/Editor/CatProneReview/CL_CAT_StandIdle_contact_v12.fbx", false);
        Surface("Assets/Editor/CatProneReview/CL_CAT_Prone_chest_v12.fbx", "../../Reports/cat-animation-v12/unity-chest-surface");
    }
    static void Surface(string asset, string output)
    {
        Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset));
        cat.transform.localScale=Vector3.one*.045f;
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var clip=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        var display=new GameObject("CPU same geometry");
        var filter=display.AddComponent<MeshFilter>();var renderer=display.AddComponent<MeshRenderer>();
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor",new Color(.65f,.65f,.65f));material.SetFloat("_Smoothness",0);
        renderer.sharedMaterial=material;skin.enabled=false;
        var light=new GameObject("Side light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;
        light.transform.rotation=Quaternion.Euler(40,-30,0);RenderSettings.ambientLight=Color.gray;
        var camera=new GameObject("Surface camera").AddComponent<Camera>();camera.orthographic=true;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.19f,.2f);
        camera.orthographicSize=.85f;camera.nearClipPlane=.01f;camera.farClipPlane=100;
        var rt=new RenderTexture(1100,1000,24);camera.targetTexture=rt;
        foreach(int frame in new[]{61,91,105,121})
        {
            clip.SampleAnimation(cat,(frame-1)/60f);
            var mesh=new Mesh();skin.BakeMesh(mesh,false);filter.sharedMesh=mesh;
            display.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);
            var center=renderer.bounds.center;
            for(int view=0;view<2;view++)
            {
                camera.transform.position=center+(view==0?new Vector3(2,.3f,3):new Vector3(0,-.3f,3));
                camera.transform.LookAt(center);
                for(int mode=0;mode<2;mode++)
                {
                    skin.BakeMesh(mesh,false);
                    if(mode==1)mesh.RecalculateNormals();
                    camera.Render();RenderTexture.active=rt;
                    var texture=new Texture2D(1100,1000,TextureFormat.RGB24,false);
                    texture.ReadPixels(new Rect(0,0,1100,1000),0,0);texture.Apply();
                    File.WriteAllBytes($"{output}/F{frame:000}-view{view}-{(mode==0?"import":"recalculated")}.png",texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                }
            }
            Object.DestroyImmediate(mesh);
        }
        RenderTexture.active=null;camera.targetTexture=null;rt.Release();
        Object.DestroyImmediate(cat);Object.DestroyImmediate(display);Object.DestroyImmediate(material);
        Debug.Log("SURFACE_COMPARISON_FINISHED 4 frames x 2 views x 2 normal modes; isolated CPU samples only");
    }
    public static void Endpoints()
    {
        const string asset="Assets/Editor/CatProneReview/CL_CAT_Prone_clearance_v12.fbx";
        const string legacy="Assets/MobileRuntime/Art/Cat/Source/CL_CAT_Legacy_v11.fbx";
        string output="../../Reports/cat-animation-v12/endpoint-comparison";
        Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset));
        cat.transform.localScale=Vector3.one*.045f;
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var transforms=cat.GetComponentsInChildren<Transform>();
        var positions=transforms.Select(t=>t.localPosition).ToArray();
        var rotations=transforms.Select(t=>t.localRotation).ToArray();
        var scales=transforms.Select(t=>t.localScale).ToArray();
        var rise=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        var stand=AssetDatabase.LoadAllAssetsAtPath(legacy).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__") && c.name.EndsWith("CL_CAT_StandIdle_v01_loop_96f"));
        var meshes=new Mesh[3];
        for(int sample=0;sample<3;sample++)
        {
            if(sample!=1)
                for(int i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];transforms[i].localScale=scales[i];}
            for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,0);
            (sample==0?rise:stand).SampleAnimation(cat,sample==0?150/60f:0);
            meshes[sample]=new Mesh();skin.BakeMesh(meshes[sample],false);
            var vertices=meshes[sample].vertices;
            for(int i=0;i<vertices.Length;i++)vertices[i]=skin.transform.position+skin.transform.rotation*vertices[i];
            meshes[sample].vertices=vertices;meshes[sample].RecalculateNormals();meshes[sample].RecalculateBounds();
        }
        var report=new System.Text.StringBuilder("CPU same-mesh samples: rise endpoint, legacy after rise, legacy from imported defaults. No scene/controller changes.\n");
        for(int a=0;a<2;a++)
        {
            var first=meshes[a].vertices;var second=meshes[2].vertices;
            var distances=first.Select((p,i)=>Vector3.Distance(p,second[i])).ToArray();
            report.AppendLine($"sample{a} -> clean legacy: max={distances.Max():R}; rms={System.Math.Sqrt(distances.Average(d=>(double)d*d)):R}");
        }
        File.WriteAllText(output+"/comparison.txt",report.ToString());
        skin.enabled=false;
        var display=new GameObject("CPU sampled geometry");var filter=display.AddComponent<MeshFilter>();
        var renderer=display.AddComponent<MeshRenderer>();
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor",new Color(.7f,.73f,.76f));material.SetFloat("_Smoothness",0);
        renderer.sharedMaterial=material;
        var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;
        light.transform.rotation=Quaternion.Euler(40,-30,0);RenderSettings.ambientLight=Color.gray;
        var camera=new GameObject("Same camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.16f,.19f,.2f);camera.orthographic=true;
        var bounds=meshes[0].bounds;bounds.Encapsulate(meshes[2].bounds);
        var target=bounds.center;float size=bounds.size.magnitude;
        camera.orthographicSize=size*.48f;camera.nearClipPlane=.01f;camera.farClipPlane=100;
        var rt=new RenderTexture(1000,800,24);camera.targetTexture=rt;
        var names=new[]{"rise","legacy-after-rise","legacy-clean"};
        var views=new[]{new Vector3(1,.22f,0),new Vector3(1,.3f,1)};
        for(int view=0;view<views.Length;view++)
        {
            camera.transform.position=target+views[view].normalized*size*3;camera.transform.LookAt(target);
            for(int sample=0;sample<3;sample++)
            {
                filter.sharedMesh=meshes[sample];camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(1000,800,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,1000,800),0,0);texture.Apply();
                File.WriteAllBytes(output+"/"+names[sample]+"-"+view+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
        }
        RenderTexture.active=null;camera.targetTexture=null;rt.Release();
        foreach(var mesh in meshes)Object.DestroyImmediate(mesh);
        Object.DestroyImmediate(cat);Object.DestroyImmediate(display);Object.DestroyImmediate(material);
        Debug.Log("ENDPOINT_COMPARISON_FINISHED\n"+report);
    }
    public static void Clearance()
    {
        Clearance("CL_CAT_Prone_clearance_v12","unity-clearance","Assets/MobileRuntime/Art/Cat/Source/CL_CAT_Legacy_v11.fbx",true);
    }
    public static void Unified()
    {
        const string standName="CL_CAT_Stand_unified_v12";
        string standAsset="Assets/Editor/CatProneReview/"+standName+".fbx";
        File.Copy("../../Reports/cat-animation-v12/grounding-export-check/"+standName+".fbx",standAsset,true);
        AssetDatabase.ImportAsset(standAsset,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(standAsset);
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.importNormals=ModelImporterNormals.Import;
        importer.importBlendShapeNormals=ModelImporterNormals.None;
        importer.SaveAndReimport();
        Clearance("CL_CAT_Prone_unified_v12","unity-unified",standAsset,false);
    }
    static void Clearance(string name,string reportName,string standAsset,bool legacy)
    {
        string asset="Assets/Editor/CatProneReview/"+name+".fbx";
        File.Copy("../../Reports/cat-animation-v12/grounding-export-check/"+name+".fbx",asset,true);
        AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(asset);
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.importNormals=ModelImporterNormals.Import;
        importer.importBlendShapeNormals=ModelImporterNormals.None;
        importer.SaveAndReimport();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset));
        cat.transform.localScale=Vector3.one*.045f;
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var clip=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        var mesh=new Mesh();var weights=skin.sharedMesh.boneWeights;
        var feet=new[]{"frontleg2","R_frontleg2","backleg2","R_backleg2"};
        var groups=feet.Select(foot=>Enumerable.Range(0,weights.Length).Where(i=>weights[i].weight0>.99f && skin.bones[weights[i].boneIndex0].name==foot).ToArray()).ToArray();
        var rows=new System.Text.StringBuilder("frame,mesh_min,min_vertex,min_bone,front_left,front_right,rear_left,rear_right\n");
        float minimum=float.PositiveInfinity;int worst=0;
        for(int frame=0;frame<=648;frame++)
        {
            clip.SampleAnimation(cat,frame/60f);skin.BakeMesh(mesh,false);
            var points=mesh.vertices;float low=float.PositiveInfinity;int vertex=0;
            for(int i=0;i<points.Length;i++)
            {
                points[i]=skin.transform.position+skin.transform.rotation*points[i];
                if(points[i].y<low){low=points[i].y;vertex=i;}
            }
            if(low<minimum){minimum=low;worst=frame;}
            var soles=groups.Select(ids=>ids.Min(i=>points[i].y)).ToArray();
            rows.AppendLine(System.FormattableString.Invariant($"{frame},{low:R},{vertex},{skin.bones[weights[vertex].boneIndex0].name},{soles[0]:R},{soles[1]:R},{soles[2]:R},{soles[3]:R}"));
        }
        File.WriteAllText("../../Reports/cat-animation-v12/"+reportName+"-all-frames.csv",rows.ToString());
        var summary=new System.Text.StringBuilder($"Frames=649; seconds={clip.length}; minimum={minimum:R}; worstFrame={worst}; CPU mesh on isolated candidate, no terrain or controller integration.\n");
        clip.SampleAnimation(cat,150/60f);skin.BakeMesh(mesh,false);var riseEnd=mesh.vertices;
        var stand=AssetDatabase.LoadAllAssetsAtPath(standAsset).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__") && (!legacy || c.name.EndsWith("CL_CAT_StandIdle_v01_loop_96f")));
        for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,0);
        stand.SampleAnimation(cat,0);skin.BakeMesh(mesh,false);var legacyStand=mesh.vertices;
        float maxEndpoint=0;double sum=0;
        for(int i=0;i<riseEnd.Length;i++){float d=Vector3.Distance(riseEnd[i],legacyStand[i]);maxEndpoint=Mathf.Max(maxEndpoint,d);sum+=d*d;}
        summary.AppendLine($"Rise endpoint -> comparison stand ({standAsset}): max={maxEndpoint:R}; rms={System.Math.Sqrt(sum/riseEnd.Length):R}");
        for(int foot=0;foot<feet.Length;foot++)summary.AppendLine($"{feet[foot]} riseY={groups[foot].Min(i=>(skin.transform.rotation*riseEnd[i]).y):R}; legacyStandY={groups[foot].Min(i=>(skin.transform.rotation*legacyStand[i]).y):R}");
        File.WriteAllText("../../Reports/cat-animation-v12/"+reportName+"-summary.txt",summary.ToString());
        Object.DestroyImmediate(mesh);Object.DestroyImmediate(cat);
        Debug.Log($"CLEARANCE_AUDIT_FINISHED minimum={minimum:R} frame={worst}");
    }
    public static void Compare()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var report=new System.Text.StringBuilder();
        foreach(string name in new[]{"CL_CAT_Prone_grounding_check","full-transform-inherit-probe","CL_CAT_Prone_inheritance_baked_v12"})
        {
            string asset="Assets/Editor/CatProneReview/"+name+".fbx";
            File.Copy("../../Reports/cat-animation-v12/grounding-export-check/"+name+".fbx",asset,true);
            AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(asset);
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.importNormals=ModelImporterNormals.Import;
            importer.importBlendShapeNormals=ModelImporterNormals.None;
            importer.SaveAndReimport();
            var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(asset));
            cat.transform.localScale=Vector3.one*.045f;
            var clip=AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            clip.SampleAnimation(cat,0);
            var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked=new Mesh();skin.BakeMesh(baked,false);
            var vertices=baked.vertices;var weights=skin.sharedMesh.boneWeights;
            report.AppendLine($"{name} clip={clip.name} seconds={clip.length} scale={skin.transform.lossyScale}");
            foreach(string foot in new[]{"frontleg2","R_frontleg2","backleg2","R_backleg2"})
            {
                int bone=System.Array.FindIndex(skin.bones,b=>b.name==foot);
                float sole=float.PositiveInfinity;
                for(int i=0;i<vertices.Length;i++)
                    if(weights[i].boneIndex0==bone && weights[i].weight0>.99f)
                        sole=Mathf.Min(sole,(skin.transform.position+skin.transform.rotation*vertices[i]).y);
                report.AppendLine($"{foot} sole={sole:R} head={skin.bones[bone].position.ToString("G9")}");
            }
            if(name=="CL_CAT_Prone_inheritance_baked_v12")
            {
                foreach(int frame in new[]{0,30,90,150,180,240,330,360,480,600,648})
                {
                    clip.SampleAnimation(cat,frame/60f);skin.BakeMesh(baked,false);vertices=baked.vertices;
                    int low=Enumerable.Range(0,vertices.Length).OrderBy(i=>(skin.transform.position+skin.transform.rotation*vertices[i]).y).First();
                    report.AppendLine($"sampleFrame={frame} meshBottom={(skin.transform.position+skin.transform.rotation*vertices[low]).y:R} minBone={skin.bones[weights[low].boneIndex0].name}");
                    foreach(string foot in new[]{"frontleg2","R_frontleg2","backleg2","R_backleg2"})
                    {
                        int bone=System.Array.FindIndex(skin.bones,b=>b.name==foot);
                        float sole=Enumerable.Range(0,vertices.Length).Where(i=>weights[i].boneIndex0==bone && weights[i].weight0>.99f).Min(i=>(skin.transform.position+skin.transform.rotation*vertices[i]).y);
                        report.AppendLine($"  {foot} sole={sole:R}");
                    }
                }
            }
            Object.DestroyImmediate(baked);Object.DestroyImmediate(cat);
        }
        File.WriteAllText("../../Reports/cat-animation-v12/unity-grounding-export-comparison.txt",report.ToString());
        Debug.Log(report.ToString());
    }
}
