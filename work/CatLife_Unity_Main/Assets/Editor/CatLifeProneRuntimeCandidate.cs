using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CatLifeProneRuntimeCandidate
{
    const string Folder="Assets/Editor/CatProneReview/";
    public static void Build()
    {
        CatLifeProneImportReview.Run();
        string legacyPath=Folder+"CL_CAT_LegacyReview_v09.fbx";
        AssetDatabase.ImportAsset(legacyPath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(legacyPath);
        importer.animationType=ModelImporterAnimationType.Generic;
        importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.materialImportMode=ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var oldClips=AssetDatabase.LoadAllAssetsAtPath(legacyPath).OfType<AnimationClip>()
            .Where(c=>!c.name.StartsWith("__preview__")).ToArray();
        var newClips=AssetDatabase.LoadAllAssetsAtPath(Folder+"CL_CAT_ProneReview_v11.fbx")
            .OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__") && c.name!="CL_CAT_ProneReview_v11").ToArray();
        string controllerPath=Folder+"CL_CAT_IntegrationCandidate.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        if(controller.layers.Length==0) controller.AddLayer("Base Layer");
        var machine=controller.layers[0].stateMachine;
        foreach(var state in machine.states) machine.RemoveState(state.state);
        var lines=new List<string>();
        var states=new Dictionary<string,AnimatorState>();
        foreach(var clip in oldClips.Concat(newClips))
        {
            string name=clip.name.Substring(clip.name.LastIndexOf('|')+1);
            var state=machine.AddState(name);
            state.motion=clip;
            state.writeDefaultValues=true;
            states.Add(name,state);
            if(name=="CL_CAT_StandIdle_v01_loop_96f") machine.defaultState=state;
            lines.Add(name+" seconds="+clip.length);
        }
        foreach(string trigger in new[]{"BeginFocus","EndFocus","Nudge"})
            if(!controller.parameters.Any(p=>p.name==trigger)) controller.AddParameter(trigger,AnimatorControllerParameterType.Trigger);
        var enter=states["CL_CAT_StandIdle_v01_loop_96f"].AddTransition(states["CL_CAT_StandToLie_v11_150f"]);
        enter.hasExitTime=false; enter.hasFixedDuration=true; enter.duration=.18f;
        enter.AddCondition(AnimatorConditionMode.If,0,"BeginFocus");
        var rest=states["CL_CAT_StandToLie_v11_150f"].AddTransition(states["CL_CAT_ProneBreath_v11_loop_240f"]);
        rest.hasExitTime=true; rest.exitTime=1; rest.hasFixedDuration=true; rest.duration=.08f;
        var wake=states["CL_CAT_ProneBreath_v11_loop_240f"].AddTransition(states["CL_CAT_LieToStand_v11_120f"]);
        wake.hasExitTime=false; wake.hasFixedDuration=true; wake.duration=.08f;
        wake.AddCondition(AnimatorConditionMode.If,0,"EndFocus");
        var nudge=states["CL_CAT_ProneBreath_v11_loop_240f"].AddTransition(states["CL_CAT_ProneAttention_v11_48f"]);
        nudge.hasExitTime=false;nudge.hasFixedDuration=true;nudge.duration=.08f;
        nudge.AddCondition(AnimatorConditionMode.If,0,"Nudge");
        var resume=states["CL_CAT_ProneAttention_v11_48f"].AddTransition(states["CL_CAT_ProneBreath_v11_loop_240f"]);
        resume.hasExitTime=true;resume.exitTime=1;resume.hasFixedDuration=true;resume.duration=.12f;
        var finish=states["CL_CAT_LieToStand_v11_120f"].AddTransition(states["CL_CAT_StandIdle_v01_loop_96f"]);
        finish.hasExitTime=true; finish.exitTime=1; finish.hasFixedDuration=true; finish.duration=.35f;
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"CL_CAT_ProneReview_v11.fbx"));
        var animator=model.GetComponent<Animator>();
        if(animator==null) animator=model.AddComponent<Animator>();
        var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
        // The imported study opens in a prone pose with corrective=100.
        // Legacy clips have no curves for the new shapes; Animator defaults
        // must therefore be neutral, not the study's first-frame correction.
        for(int i=0;i<skin.sharedMesh.blendShapeCount;i++) skin.SetBlendShapeWeight(i,0);
        animator.runtimeAnimatorController=controller;
        animator.applyRootMotion=false;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        foreach(var clip in oldClips.Concat(newClips))
        {
            foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Transform)))
                if(binding.path!="" && model.transform.Find(binding.path)==null)
                    throw new Exception("Unresolved bone: "+binding.path);
        }
        // Compare the two standing endpoints on the SAME new mesh.
        var idle=oldClips.Single(c=>c.name.EndsWith("CL_CAT_StandIdle_v01_loop_96f"));
        var rise=newClips.Single(c=>c.name=="CL_CAT_LieToStand_v11_120f");
        Vector3[] Sample(AnimationClip clip,float time)
        {
            for(int i=0;i<skin.sharedMesh.blendShapeCount;i++) skin.SetBlendShapeWeight(i,0);
            clip.SampleAnimation(model,time);
            var mesh=new Mesh();
            skin.BakeMesh(mesh,false);
            var result=mesh.vertices;
            UnityEngine.Object.DestroyImmediate(mesh);
            return result;
        }
        var a=Sample(rise,rise.length);
        var b=Sample(idle,0);
        double sum=0;
        float max=0;
        for(int i=0;i<a.Length;i++){float d=Vector3.Distance(a[i],b[i]);max=Mathf.Max(max,d);sum+=d*d;}
        lines.Add($"RiseEnd_to_LegacyStand: max={max} rms={Math.Sqrt(sum/a.Length)} candidate mesh units");
        var breath=newClips.Single(c=>c.name=="CL_CAT_ProneBreath_v11_loop_240f");
        var attention=newClips.Single(c=>c.name=="CL_CAT_ProneAttention_v11_48f");
        var weights=skin.sharedMesh.boneWeights;
        var paws=Enumerable.Range(0,weights.Length).Where(i=>weights[i].weight0>.99f && skin.bones[weights[i].boneIndex0].name.EndsWith("leg2")).ToArray();
        foreach(var clip in new[]{breath,attention})
        {
            var start=Sample(clip,0);var middle=Sample(clip,clip.length*.5f);var end=Sample(clip,clip.length);
            float displacement=0,closure=0,pawMotion=0;
            for(int i=0;i<start.Length;i++){displacement=Mathf.Max(displacement,Vector3.Distance(start[i],middle[i]));closure=Mathf.Max(closure,Vector3.Distance(start[i],end[i]));}
            foreach(int i in paws) pawMotion=Mathf.Max(pawMotion,Vector3.Distance(start[i],middle[i]));
            lines.Add($"{clip.name}: midpointDelta={displacement:R} endpointError={closure:R} pawMidpointDelta={pawMotion:R} pawVertices={paws.Length}");
        }
        // Restore neutral defaults before evaluating the controller.
        Sample(idle,0);
        animator.Rebind();
        animator.Update(0);
        animator.SetTrigger("BeginFocus");
        for(int frame=0;frame<180;frame++) animator.Update(1f/60f);
        bool inRest=animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneBreath_v11_loop_240f");
        animator.SetTrigger("Nudge");
        for(int frame=0;frame<12;frame++) animator.Update(1f/60f);
        bool attentive=animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneAttention_v11_48f");
        for(int frame=0;frame<60;frame++) animator.Update(1f/60f);
        bool resumed=animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneBreath_v11_loop_240f");
        animator.SetTrigger("EndFocus");
        for(int frame=0;frame<180;frame++) animator.Update(1f/60f);
        bool standing=animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f");
        lines.Add($"Animator manual evaluation: entersProne={inRest}, attention={attentive}, resumesBreath={resumed}, returnsStanding={standing}");
        PrefabUtility.SaveAsPrefabAsset(model,Folder+"PF_CL_CAT_IntegrationCandidate.prefab");
        UnityEngine.Object.DestroyImmediate(model);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Reports/ProneImportReview");
        File.WriteAllLines("Reports/ProneImportReview/integration.txt",lines);
        if(!inRest || !attentive || !resumed || !standing) throw new Exception("Candidate Animator routing did not complete.");
        Debug.Log("PRONE_CANDIDATE_BUILT: "+lines.Last());
    }
}
