#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CatLifeV12CandidateTests
{
    const string Folder="../../Reports/interview-demo-20261008/v12-fullweights-playmode/";
    Scene scene;
    SkinWeights previousWeights;
    float previousCaptureDelta;
    GameObject cat;
    Animator animator;
    SkinnedMeshRenderer skin;
    Camera camera;
    RenderTexture target;
    Mesh baked;
    readonly List<string> rows=new List<string>();
    float minimum;
    int sequenceFrame;

    [UnitySetUp] public IEnumerator Setup()
    {
        previousWeights=QualitySettings.skinWeights;
        previousCaptureDelta=Time.captureDeltaTime;
        QualitySettings.skinWeights=SkinWeights.Unlimited;
        Time.captureDeltaTime=1f/60;
        scene=SceneManager.CreateScene("V12FullWeightsIsolated");
        SceneManager.SetActiveScene(scene);
        var mobile = SceneManager.GetSceneByName("CatLifeMobile");
        if (mobile.isLoaded) yield return SceneManager.UnloadSceneAsync(mobile);
        cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Editor/CatProneReview/V12FullWeightsController/PF_CL_CAT_V12Candidate.prefab"));
        animator=cat.GetComponent<Animator>();
        skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();
        skin.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/MobileRuntime/Art/Materials/MAT_CL_Cat.mat");
        skin.updateWhenOffscreen=true;
        Assert.That(skin.quality,Is.EqualTo(SkinQuality.Auto));
        using(var counts=skin.sharedMesh.GetBonesPerVertex())Assert.That(counts.ToArray().Max(),Is.GreaterThan(4));
        var light=new GameObject("Candidate light").AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(40,-30,0);
        camera=new GameObject("Candidate camera").AddComponent<Camera>();
        camera.orthographic=true;camera.orthographicSize=1.15f;
        camera.transform.position=new Vector3(2,1.3f,3);camera.transform.LookAt(new Vector3(0,.9f,0));
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.19f,.2f);
        target=new RenderTexture(1000,800,24);camera.targetTexture=target;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.localScale=Vector3.one*.5f;
        baked=new Mesh();minimum=float.PositiveInfinity;sequenceFrame=0;
        rows.Clear();rows.Add("frame,requested_state,current_state,normalized_time,transition,mesh_min");
        yield return null;
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        QualitySettings.skinWeights=previousWeights;Time.captureDeltaTime=previousCaptureDelta;
        camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(baked);
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest] public IEnumerator WalkProneRiseThreeCycles()
    {
        string output=Folder+"walk-prone-rise";Directory.CreateDirectory(output);
        for(int cycle=0;cycle<3;cycle++)
        {
            yield return Play("CL_CAT_Walk_v01_loop_60f",60,output);
            yield return Play("CL_CAT_StandIdle_v01_loop_96f",30,output);
            yield return Play("CL_CAT_StandToLie_v11_150f",159,output);
            yield return Play("CL_CAT_ProneBreath_v11_loop_240f",60,output);
            yield return Play("CL_CAT_ProneAttention_v11_48f",57,output);
            yield return Play("CL_CAT_ProneBreath_v11_loop_240f",60,output);
            yield return Play("CL_CAT_LieToStand_v11_120f",129,output);
            yield return Play("CL_CAT_StandIdle_v01_loop_96f",30,output);
        }
        Assert.That(minimum,Is.GreaterThanOrEqualTo(-.0001f),"Continuous blend penetrates isolated plane; inspect frames.csv.");
    }

    [UnityTest] public IEnumerator SitProneSitContinuity()
    {
        string output=Folder+"sit-prone-sit";Directory.CreateDirectory(output);
        yield return Play("CL_CAT_StandToSit_v01_72f",81,output);
        yield return Play("CL_CAT_SitIdle_v01_loop_96f",60,output);
        yield return Play("CL_CAT_SitToLie_v01_120f",129,output);
        yield return Play("CL_CAT_ProneBreath_v11_loop_240f",60,output);
        yield return Play("CL_CAT_LieToSit_v01_72f",105,output);
        yield return Play("CL_CAT_SitIdle_v01_loop_96f",60,output);
        Assert.That(minimum,Is.GreaterThanOrEqualTo(-.0001f),"Continuous blend penetrates isolated plane; inspect frames.csv.");
    }

    IEnumerator Play(string state,int frames,string output)
    {
        animator.CrossFadeInFixedTime(state,.15f,0,0);
        for(int frame=0;frame<frames;frame++)
        {
            yield return null;
            var info=animator.GetCurrentAnimatorStateInfo(0);
            bool blending=animator.IsInTransition(0);
            if(frame>=12)Assert.That(info.IsName(state),Is.True,"Wrong actual state: "+state);
            skin.BakeMesh(baked,false);
            float low=baked.vertices.Min(v=>(skin.transform.position+skin.transform.rotation*v).y);
            minimum=Mathf.Min(minimum,low);
            rows.Add(System.FormattableString.Invariant($"{sequenceFrame},{state},{info.shortNameHash},{info.normalizedTime:R},{blending},{low:R}"));
            if(sequenceFrame%15==0 || frame==frames-1)
            {
                camera.Render();RenderTexture.active=target;
                var image=new Texture2D(1000,800,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();
                File.WriteAllBytes(output+"/F"+sequenceFrame.ToString("0000")+".png",image.EncodeToPNG());
                RenderTexture.active=null;Object.Destroy(image);
            }
            sequenceFrame++;
        }
        File.WriteAllLines(output+"/frames.csv",rows);
    }
}
#endif
