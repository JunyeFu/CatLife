using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class CatLifeTurnRootStudyTests
{
    [Serializable] private sealed class RootSample { public float yaw_degrees; public float[] root_translation; }
    [Serializable] private sealed class RootSchedule { public RootSample[] samples; }
    [UnityTest] public IEnumerator LocalTurnAndRigidRootRecomposeOriginalMesh()
    { yield return ReviewRoot(false); }
    [UnityTest] public IEnumerator ExportedScheduleDrivesPhysicalRootWithoutDoubleTurn()
    { yield return ReviewRoot(true); }
    private IEnumerator ReviewRoot(bool driveRoot)
    {
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MobileRuntime/Art/Cat/PF_CL_CAT_Runtime_v12.prefab");
        var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/MobileRuntime/Art/Cat/CL_CAT_Mobile.controller");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Reports/"+(driveRoot?"wp03-physical-root-study-20261013":"wp03-unity-root-study-20261013")));
        Directory.CreateDirectory(output);
        foreach (string direction in new[]{"Left","Right"})
        {
            var cats = new GameObject[2];
            var skins = new SkinnedMeshRenderer[2];
            var animators = new Animator[2];
            var meshes = new[]{new Mesh(),new Mesh()};
            try
            {
                for (int i=0;i<2;i++)
                {
                    cats[i] = UnityEngine.Object.Instantiate(prefab);
                    cats[i].transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    cats[i].transform.localScale = Vector3.one*.045f;
                    foreach (var behaviour in cats[i].GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled=false;
                    skins[i] = cats[i].GetComponentInChildren<SkinnedMeshRenderer>();
                    skins[i].enabled=false;
                    animators[i] = cats[i].GetComponent<Animator>() ?? cats[i].AddComponent<Animator>();
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Editor/CatProneReview/CL_CAT_Turn"+(i==0?"Full":"Local")+direction+"_candidate.anim");
                    Assert.That(clip,Is.Not.Null);
                    var replacement = new AnimatorOverrideController(controller);
                    replacement["CL_CAT_StandIdle_v01_loop_96f"]=clip;
                    animators[i].runtimeAnimatorController=replacement;
                    animators[i].cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    animators[i].speed=0;
                }
                Rigidbody body = null;
                RootSchedule schedule = null;
                if (driveRoot)
                {
                    body = cats[1].AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.interpolation = RigidbodyInterpolation.None;
                    schedule = JsonUtility.FromJson<RootSchedule>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
                        "../../../Reports/wp03-turn-"+direction.ToLowerInvariant()+"-local-20261013/root-schedule.json"))));
                }
                yield return null;
                float worst=0, worstAngle=0;
                var rows=new System.Text.StringBuilder("frame,yaw,x,y,z,max_mesh_error\n");
                for(int frame=0;frame<=120;frame++)
                {
                    foreach(var animator in animators) { animator.Play("CL_CAT_StandIdle_v01_loop_96f",0,frame/120f); animator.Update(0); }
                    if (driveRoot)
                    {
                        var sample=schedule.samples[frame];
                        var p=sample.root_translation;
                        body.MovePosition(new Vector3(-p[0],p[2],-p[1])*.045f);
                        body.MoveRotation(Quaternion.Euler(0,-sample.yaw_degrees,0));
                        yield return new WaitForFixedUpdate();
                    }
                    yield return null;
                    var hips=skins.Select(s=>s.bones.First(b=>b.name=="Hips")).ToArray();
                    Quaternion rotation=hips[0].rotation*Quaternion.Inverse(hips[1].rotation);
                    Vector3 translation=hips[0].position-rotation*hips[1].position;
                    float t=frame/120f,cycle=Mathf.Min(1,Mathf.Floor(t*2)),u=t*2-cycle;
                    float expected=15*(cycle+u*u*(3-2*u));
                    float expectedYaw=direction=="Left"?-expected:expected;
                    float measuredYaw=driveRoot?body.rotation.eulerAngles.y:rotation.eulerAngles.y;
                    worstAngle=Mathf.Max(worstAngle,Mathf.Abs(Mathf.DeltaAngle(expectedYaw,measuredYaw)));
                    for(int i=0;i<2;i++) skins[i].BakeMesh(meshes[i],false);
                    var full=meshes[0].vertices;var local=meshes[1].vertices;
                    float error=0;
                    for(int v=0;v<full.Length;v++)
                    {
                        Vector3 a=skins[0].transform.position+skins[0].transform.rotation*full[v];
                        Vector3 b=skins[1].transform.position+skins[1].transform.rotation*local[v];
                        error=Mathf.Max(error,Vector3.Distance(a,driveRoot?b:rotation*b+translation));
                    }
                    worst=Mathf.Max(worst,error);
                    rows.AppendLine(FormattableString.Invariant($"{frame},{Mathf.DeltaAngle(0,rotation.eulerAngles.y):F6},{translation.x:F6},{translation.y:F6},{translation.z:F6},{error:F9}"));
                }
                File.WriteAllText(Path.Combine(output,direction+".csv"),rows.ToString());
                Debug.Log($"TURN_ROOT_STUDY driven={driveRoot} {direction} mesh={worst:R} angle={worstAngle:R}");
                Assert.That(worst,Is.LessThan(.0001f),"Rigid root must restore all vertices within 0.1mm.");
                Assert.That(worstAngle,Is.LessThan(.1f),"Turn angle must follow the authored two-step schedule.");
            }
            finally { foreach(var cat in cats) UnityEngine.Object.Destroy(cat); foreach(var mesh in meshes) UnityEngine.Object.Destroy(mesh); }
        }
#else
        Assert.Ignore("Editor-only turn decomposition study.");
        yield return null;
#endif
    }
}
