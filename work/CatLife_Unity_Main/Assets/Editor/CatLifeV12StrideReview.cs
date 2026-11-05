using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CatLifeV12StrideReview
{
    public static void Run()
    {
        var cat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MobileRuntime/Art/Cat/PF_CL_CAT_Runtime_v12.prefab"));
        cat.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);cat.transform.localScale=Vector3.one*.045f;
        var animator=cat.GetComponent<Animator>();Object.DestroyImmediate(animator);
        var skin=cat.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MobileRuntime/Art/Cat/AnimationsV12/CL_CAT_Walk_v01_loop_60f.anim");
        string[] feet={"frontleg2","R_frontleg2","backleg2","R_backleg2"};
        var points=feet.Select(f=>new System.Collections.Generic.List<int>()).ToArray();
        int[] bones=feet.Select(f=>Array.FindIndex(skin.bones,b=>b.name==f)).ToArray();
        using(var counts=skin.sharedMesh.GetBonesPerVertex())using(var weights=skin.sharedMesh.GetAllBoneWeights())
        {
            int offset=0;
            for(int v=0;v<counts.Length;v++)
            {
                for(int f=0;f<4;f++)
                {
                    float sum=0;for(int j=0;j<counts[v];j++)if(weights[offset+j].boneIndex==bones[f])sum+=weights[offset+j].weight;
                    if(sum>=.99f)points[f].Add(v);
                }
                offset+=counts[v];
            }
        }
        var rows=new StringBuilder("frame,foot,sole_y,center_x,center_y,center_z,forward_speed\n");var previous=new Vector3[4];
        for(int frame=0;frame<=60;frame++)
        {
            clip.SampleAnimation(cat,frame/60f);skin.BakeMesh(baked,false);var vertices=baked.vertices;
            for(int f=0;f<4;f++)
            {
                var world=points[f].Select(v=>skin.transform.position+skin.transform.rotation*vertices[v]).ToArray();
                Vector3 center=Vector3.zero;foreach(var p in world)center+=p;center/=world.Length;
                float speed=frame==0?0:(center.z-previous[f].z)*60;previous[f]=center;
                rows.AppendLine(FormattableString.Invariant($"{frame},{feet[f]},{world.Min(p=>p.y):R},{center.x:R},{center.y:R},{center.z:R},{speed:R}"));
            }
        }
        File.WriteAllText("../../Reports/cat-animation-v12/walk-stride.csv",rows.ToString());
        Object.DestroyImmediate(baked);Object.DestroyImmediate(cat);
        Debug.Log("V12_WALK_STRIDE_SAMPLED 61 frames; fixed root and actual runtime scale; no navigation claim.");
    }
}
