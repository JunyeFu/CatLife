#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class CatLifeV12TerrainTests
{
    const string Key="CatLife.Mobile.Data.v1";
    const string Folder="../../Reports/interview-demo-20261008/v12-terrain-micro-contact/";
    bool hadData; string savedData;
    Camera detail; RenderTexture target; Mesh baked;
    [SetUp] public void SaveData(){hadData=PlayerPrefs.HasKey(Key);savedData=PlayerPrefs.GetString(Key);PlayerPrefs.DeleteKey(Key);}
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(detail!=null)detail.targetTexture=null;
        if(target!=null){target.Release();Object.Destroy(target);}if(baked!=null)Object.Destroy(baked);
        var scene=SceneManager.GetSceneByName("CatLifeMobile");
        SceneManager.SetActiveScene(SceneManager.CreateScene("TerrainReviewCleanup"));
        if(scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
        if(hadData)PlayerPrefs.SetString(Key,savedData);else PlayerPrefs.DeleteKey(Key);PlayerPrefs.Save();
    }
    [UnityTest] public IEnumerator LocateFootTerrainGapOnRealPath()
    {
        SceneManager.LoadScene("CatLifeMobile");yield return null;
        var routeDriver = GameObject.Find("CatLifeMobileCat").GetComponent("CatBehaviorDriver");
        var nextDecision = routeDriver.GetType().GetField("nextDecisionTime",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        nextDecision.SetValue(routeDriver,float.PositiveInfinity);
        yield return new WaitForSeconds(.5f);
        Directory.CreateDirectory(Folder);
        var cat=GameObject.Find("CatLifeMobileCat");var body=cat.GetComponent<Rigidbody>();
        var contact=(Behaviour)cat.GetComponent("CatFootTerrainContact");
        Assert.That(contact,Is.Not.Null);Assert.That(contact.enabled,Is.True);
        var probe=new GameObject("Terrain post-animation probe").AddComponent<CatTerrainLateProbe>();
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();var animator=cat.GetComponent<Animator>();
        var agent=cat.GetComponent<NavMeshAgent>();var driver=cat.GetComponent("CatBehaviorDriver");
        string[] feet={"frontleg2","R_frontleg2","backleg2","R_backleg2"};
        var subsets=new List<int>[4];var legacy=new List<int>[4];
        int[] bones=feet.Select(f=>Array.FindIndex(skin.bones,b=>b.name==f)).ToArray();
        for(int f=0;f<4;f++){subsets[f]=new List<int>();legacy[f]=new List<int>();}
        using(var counts=skin.sharedMesh.GetBonesPerVertex())using(var weights=skin.sharedMesh.GetAllBoneWeights())
        {
            int offset=0;var old=skin.sharedMesh.boneWeights;
            for(int v=0;v<counts.Length;v++)
            {
                for(int f=0;f<4;f++)
                {
                    float sum=0;for(int j=0;j<counts[v];j++)if(weights[offset+j].boneIndex==bones[f])sum+=weights[offset+j].weight;
                    if(sum>=.99f)subsets[f].Add(v);
                    var w=old[v];float four=(w.boneIndex0==bones[f]?w.weight0:0)+(w.boneIndex1==bones[f]?w.weight1:0)+(w.boneIndex2==bones[f]?w.weight2:0)+(w.boneIndex3==bones[f]?w.weight3:0);
                    if(four>=.99f)legacy[f].Add(v);
                }
                offset+=counts[v];
            }
        }
        File.WriteAllLines(Folder+"point-sets.txt",Enumerable.Range(0,4).Select(f=>$"{feet[f]} full={subsets[f].Count} legacy={legacy[f].Count} same={subsets[f].SequenceEqual(legacy[f])}"));
        detail=new GameObject("Terrain detail camera").AddComponent<Camera>();detail.orthographic=true;detail.orthographicSize=.95f;
        target=new RenderTexture(1000,700,24);detail.targetTexture=target;baked=new Mesh();
        var rows=new StringBuilder("sample,time,route,state,normalized,transition,foot,vertex,x,y,z,ground_y,gap,surface,normal_y,body_y,body_speed,sole_vy,sole_speed_xz,legacy_gap\n");
        var previous=new Vector3[4];float previousTime=Time.time;int sample=0;float nextCapture=Time.time;
        var wholeRows=new StringBuilder("sample,time,whole_min_gap,body_offset,corrected_feet\n");
        float lowestContact=float.PositiveInfinity;int eligibleSamples=0;
        // Use the same approved, whole-body-visible destinations as the runtime
        // route test, not the former full-island camera's out-of-frame route.
        foreach(var destination in new[]{GameObject.Find("Interest_Left_Garden").transform.position,GameObject.Find("Interest_Front_Path").transform.position})
        {
            Assert.That(NavMesh.SamplePosition(destination,out var hit,1f,NavMesh.AllAreas),Is.True);
            var path=new NavMeshPath();Assert.That(agent.CalculatePath(hit.position,path),Is.True);Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete));
            float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
            Assert.That((bool)driver.GetType().GetMethod("NotifyGroundTapped").Invoke(driver,new object[]{hit.position}),Is.True);
            // Explicit route audit: ambient random requests must not claim the
            // same destination before this route and its stop are sampled.
            nextDecision.SetValue(routeDriver,float.PositiveInfinity);
            File.AppendAllText(Folder+"destinations.txt",$"requested={hit.position:R} selected={agent.destination:R}\n");
            float started=Time.time;float deadline=started+length/agent.speed*2+3;float arrived=-1;
            while(Time.time<deadline)
            {
                bool sampled=false;
                probe.sample=()=>
                {
                skin.BakeMesh(baked,false);var vertices=baked.vertices;var state=animator.GetCurrentAnimatorStateInfo(0);
                string clip=animator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
                bool eligible=state.IsName("CL_CAT_Walk_v01_loop_60f")||state.IsName("CL_CAT_StandIdle_v01_loop_96f");
                if(animator.IsInTransition(0))
                {var next=animator.GetNextAnimatorStateInfo(0);eligible &= next.IsName("CL_CAT_Walk_v01_loop_60f")||next.IsName("CL_CAT_StandIdle_v01_loop_96f");}
                if(eligible)eligibleSamples++;
                for(int f=0;f<4;f++)
                {
                    int vertex=subsets[f].OrderBy(v=>vertices[v].y).First();Vector3 sole=World(skin,vertices[vertex]);
                    int oldVertex=legacy[f].OrderBy(v=>vertices[v].y).First();Vector3 oldSole=World(skin,vertices[oldVertex]);
                    var ground=Ground(cat,sole);var oldGround=Ground(cat,oldSole);
                    if(eligible)lowestContact=Mathf.Min(lowestContact,sole.y-ground.point.y);
                    var speed=sample==0?Vector3.zero:(sole-previous[f])/(Time.time-previousTime);previous[f]=sole;
                    rows.AppendLine(FormattableString.Invariant($"{sample},{Time.time-started:R},{destination.z},{clip},{state.normalizedTime:R},{animator.IsInTransition(0)},{feet[f]},{vertex},{sole.x:R},{sole.y:R},{sole.z:R},{ground.point.y:R},{sole.y-ground.point.y:R},{ground.collider.name},{ground.normal.y:R},{body.position.y:R},{agent.velocity.magnitude:R},{speed.y:R},{new Vector2(speed.x,speed.z).magnitude:R},{oldSole.y-oldGround.point.y:R}"));
                }
                var minimum=vertices[0];foreach(var v in vertices)if(v.y<minimum.y)minimum=v;
                var whole=World(skin,minimum);var wholeGround=Ground(cat,whole);
                wholeRows.AppendLine(FormattableString.Invariant($"{sample},{Time.time-started:R},{whole.y-wholeGround.point.y:R},{contact.GetType().GetProperty("BodyOffset").GetValue(contact)},{contact.GetType().GetProperty("CorrectedFeet").GetValue(contact)}"));
                if(Time.time>=nextCapture)
                {
                    detail.transform.position=cat.transform.position+cat.transform.right*2.6f+Vector3.up*.65f+cat.transform.forward*.4f;
                    detail.transform.LookAt(cat.transform.position+Vector3.up*.55f);Capture(sample);nextCapture=Time.time+.1f;
                }
                previousTime=Time.time;sample++;
                sampled=true;
                };
                while(!sampled)yield return null;
                probe.sample=null;
                if(Time.time-started>.3f&&!agent.pathPending&&agent.remainingDistance<=agent.stoppingDistance&&agent.velocity.sqrMagnitude<.01f)
                {if(arrived<0)arrived=Time.time;if(Time.time-arrived>.2f)break;}
            }
            File.WriteAllText(Folder+"contacts.csv",rows.ToString());
            File.AppendAllText(Folder+"destinations.txt",$"arrived={body.position:R} heading={body.rotation.eulerAngles:R}\n");
            Assert.That(new Vector2(body.position.x-agent.destination.x,body.position.z-agent.destination.z).magnitude,Is.LessThan(agent.stoppingDistance+.2f));
            Assert.That(new Vector2(agent.destination.x-hit.position.x,agent.destination.z-hit.position.z).magnitude,Is.LessThanOrEqualTo(.75f));
        }
        File.WriteAllText(Folder+"contacts.csv",rows.ToString());
        File.WriteAllText(Folder+"whole-contacts.csv",wholeRows.ToString());
        var type=contact.GetType();
        File.WriteAllText(Folder+"solver.txt",$"maxLengthError={type.GetProperty("MaxLengthError").GetValue(contact)}; bodyOffset={type.GetProperty("BodyOffset").GetValue(contact)}; correctedFeet={type.GetProperty("CorrectedFeet").GetValue(contact)}");
        Assert.That(eligibleSamples,Is.GreaterThan(20));
        Assert.That(lowestContact,Is.GreaterThanOrEqualTo(0),"Walk/Stand or its stop blend penetrates the sampled surface.");
        Debug.Log($"V12_TERRAIN_CONTACT samples={sample}; eligible={eligibleSamples}; minGap={lowestContact:R}");
    }
    static Vector3 World(SkinnedMeshRenderer skin,Vector3 v)=>skin.transform.position+skin.transform.rotation*v;
    static RaycastHit Ground(GameObject cat,Vector3 sole)=>Physics.RaycastAll(sole+Vector3.up*2,Vector3.down,5,~0,QueryTriggerInteraction.Ignore)
        .Where(h=>!h.transform.IsChildOf(cat.transform)).OrderBy(h=>h.distance).First();
    void Capture(int sample)
    {
        detail.Render();var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
        File.WriteAllBytes(Folder+$"F{sample:D4}.png",image.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(image);
    }
}
[DefaultExecutionOrder(10000)]
public sealed class CatTerrainLateProbe:MonoBehaviour
{
    public Action sample;
    void LateUpdate()=>sample?.Invoke();
}
#endif
