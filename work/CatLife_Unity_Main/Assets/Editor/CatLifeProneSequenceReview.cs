using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

public static class CatLifeProneSequenceReview
{
    public static void Render()
    {
        Capture(false);
    }
    public static void Compare()
    {
        Capture(true);
    }
    public static void Focus()
    {
        Capture(false,true);
    }
    public static void CompareAmbient()
    {
        Capture(true,false,true);
    }
    static void Capture(bool compare,bool focus=false,bool ambient=false)
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        string folder="Reports/ProneImportReview/"+(ambient?"comparison-ambient":compare?"comparison":focus?"sequence-focus":"sequence-neutral");
        Directory.CreateDirectory(folder+"/frames");
        var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Editor/CatProneReview/PF_CL_CAT_IntegrationCandidate.prefab"));
        var animator=cat.GetComponent<Animator>();
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        skin.enabled=false;
        var surface=new GameObject("EvaluatedPose");
        var filter=surface.AddComponent<MeshFilter>();
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor",new Color(.65f,.65f,.65f));
        material.SetFloat("_Smoothness",0);
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        surface.AddComponent<MeshRenderer>().sharedMaterial=material;
        var light=new GameObject("Light").AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=2;
        light.transform.rotation=Quaternion.Euler(40,-30,0);
        RenderSettings.ambientLight=Color.gray;
        var camera=new GameObject("Camera").AddComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=.8f;
        camera.nearClipPlane=.01f;camera.farClipPlane=20;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.16f,.19f,.2f);
        camera.transform.position=new Vector3(1.9f,1.15f,1.9f);
        camera.transform.LookAt(new Vector3(0,.43f,0));
        var rt=new RenderTexture(800,700,24);
        camera.targetTexture=rt;
        var baked=new Mesh();
        if(compare)
        {
            const string path="Assets/Editor/CatProneReview/CL_CAT_LegacyReview_v09.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.importNormals=ModelImporterNormals.Import;
            importer.importBlendShapeNormals=ModelImporterNormals.None;
            importer.SaveAndReimport();
            var original=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var originalSkin=original.GetComponentInChildren<SkinnedMeshRenderer>();
            originalSkin.enabled=false;
            animator.enabled=false;
            var report=new List<string>();
            foreach(string action in ambient
                ? new[]{"CL_CAT_SitTailGreeting_v01_loop_96f","CL_CAT_CrouchPawWave_v01_loop_96f","CL_CAT_StandStretchYawn_v01_144f"}
                : new[]{"CL_CAT_Walk_v01_loop_60f","CL_CAT_StandIdle_v01_loop_96f"})
            {
                var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__") && c.name.EndsWith(action));
                foreach(bool useOriginal in new[]{true,false})
                {
                    foreach(float sampleTime in ambient?new[]{0f,.5f,1f,1.5f,2f}:new[]{.5f})
                    {
                    var target=useOriginal?original:cat;
                    var renderer=useOriginal?originalSkin:skin;
                    for(int i=0;i<renderer.sharedMesh.blendShapeCount;i++) renderer.SetBlendShapeWeight(i,0);
                    clip.SampleAnimation(target,sampleTime);
                    renderer.BakeMesh(baked,false);
                    filter.sharedMesh=baked;
                    string name=(useOriginal?"original_":"candidate_")+action+(ambient?"_"+Mathf.RoundToInt(sampleTime*1000):"");
                    report.Add(name+" bounds="+baked.bounds);
                    Save(camera,rt,folder+"/"+name+".png");
                    }
                }
            }
            File.WriteAllLines(folder+"/comparison.txt",report);
            rt.Release();
            Debug.Log("PRONE_SAME_ACTION_COMPARISON_DONE");
            return;
        }
        var records=new List<string>{"frame,stateHash,transition,maxVertexStep,minimumY,proneCorrective"};
        Vector3[] previous=null;
        animator.Rebind();animator.Update(0);
        animator.Play("CL_CAT_Walk_v01_loop_60f",0,0);
        for(int frame=0;frame<(focus?840:540);frame++)
        {
            if(frame==45) animator.CrossFadeInFixedTime("CL_CAT_StandIdle_v01_loop_96f",.12f);
            if(frame==90) animator.SetTrigger("BeginFocus");
            if(focus && frame==540) animator.SetTrigger("Nudge");
            if(frame==(focus?630:300)) animator.SetTrigger("EndFocus");
            animator.Update(1f/60f);
            skin.BakeMesh(baked,false);
            var vertices=baked.vertices;
            float maximum=0,min=float.PositiveInfinity;
            for(int i=0;i<vertices.Length;i++)
            {
                min=Mathf.Min(min,vertices[i].y);
                if(previous!=null) maximum=Mathf.Max(maximum,Vector3.Distance(previous[i],vertices[i]));
            }
            previous=vertices;
            float correction=skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("CL_CAT_ProneJointCorrective_v11"));
            records.Add($"{frame},{animator.GetCurrentAnimatorStateInfo(0).shortNameHash},{animator.IsInTransition(0)},{maximum:R},{min:R},{correction:R}");
            if((frame==80 || frame==(focus?812:452)) && Mathf.Abs(correction)>.001f)
                throw new System.Exception("Prone correction leaked into standing at frame "+frame);
            if(frame==280 && correction<99f)
                throw new System.Exception("Prone correction missing in rest state");
            if(frame%4!=0) continue;
            filter.sharedMesh=baked;
            camera.Render();
            RenderTexture.active=rt;
            var image=new Texture2D(800,700,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,800,700),0,0);image.Apply();
            File.WriteAllBytes($"{folder}/frames/{frame:0000}.png",image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        File.WriteAllLines(folder+"/sequence.csv",records);
        RenderTexture.active=null;camera.targetTexture=null;rt.Release();
        Object.DestroyImmediate(baked);
        Debug.Log("PRONE_SEQUENCE_CAPTURED frames="+(focus?840:540));
    }
    static void Save(Camera camera,RenderTexture rt,string path)
    {
        camera.Render(); RenderTexture.active=rt;
        var image=new Texture2D(800,700,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,800,700),0,0);image.Apply();
        File.WriteAllBytes(path,image.EncodeToPNG());
        Object.DestroyImmediate(image);RenderTexture.active=null;
    }
}
