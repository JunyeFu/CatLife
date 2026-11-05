using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Isolated arrival-pose study; the scene and source clips are not saved.
public static class CatLifeV12TerrainPoseReview
{
    const string Folder = "../../Reports/cat-animation-v12/terrain-pose-study/";
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
        var cat = GameObject.Find("CatLifeMobileCat");
        Object.DestroyImmediate(cat.GetComponent<Animator>());
        cat.transform.SetPositionAndRotation(new Vector3(2.49f, .30895412f, 2.12f), Quaternion.Euler(0, 180, 0));
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MobileRuntime/Art/Cat/AnimationsV12/CL_CAT_StandIdle_v01_loop_96f.anim");
        clip.SampleAnimation(cat, 0);
        Physics.SyncTransforms();
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh();
        skin.BakeMesh(baked, false);
        var vertices = baked.vertices;
        string[] roots = { "frontleg", "R_frontleg", "backleg", "R_backleg" };
        var chains = roots.Select(root => new[] { root, root + "0", root + "1", root + "2" }
            .Select(name => skin.bones.Single(b => b.name == name)).ToArray()).ToArray();
        var sourcePositions = chains.Select(chain => chain.Select(b => b.position).ToArray()).ToArray();
        var sourceRotations = chains.Select(chain => chain.Select(b => b.rotation).ToArray()).ToArray();
        var lengths = sourcePositions.Select(p => Enumerable.Range(0, 3).Select(i => Vector3.Distance(p[i], p[i + 1])).ToArray()).ToArray();
        var goals = new Vector3[4];
        var soleOffsets = new float[4];
        var report = new StringBuilder();
        float bodyOffset = 0;
        using (var counts = skin.sharedMesh.GetBonesPerVertex()) using (var weights = skin.sharedMesh.GetAllBoneWeights())
        {
            var indices = chains.Select(chain => Array.IndexOf(skin.bones, chain[3])).ToArray();
            var sole = Enumerable.Repeat(new Vector3(0, float.PositiveInfinity, 0), 4).ToArray();
            int offset = 0;
            for (int v = 0; v < counts.Length; v++)
            {
                for (int f = 0; f < 4; f++)
                {
                    float sum = 0;
                    for (int j = 0; j < counts[v]; j++) if (weights[offset + j].boneIndex == indices[f]) sum += weights[offset + j].weight;
                    if (sum < .99f) continue;
                    var p = skin.transform.position + skin.transform.rotation * vertices[v];
                    if (p.y < sole[f].y) sole[f] = p;
                }
                offset += counts[v];
            }
            for (int f = 0; f < 4; f++)
            {
                var ground = Physics.RaycastAll(sole[f] + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.transform.IsChildOf(cat.transform)).OrderBy(h => h.distance).First();
                soleOffsets[f] = sourcePositions[f][3].y - sole[f].y;
                goals[f] = sourcePositions[f][3] + Vector3.up * (ground.point.y + .001f - sole[f].y);
                Vector3 horizontal = goals[f] - sourcePositions[f][0]; horizontal.y = 0;
                float reach = lengths[f].Sum() * .98f;
                float vertical = Mathf.Sqrt(Mathf.Max(0, reach * reach - horizontal.sqrMagnitude));
                float permitted = goals[f].y + vertical - sourcePositions[f][0].y;
                bodyOffset = Mathf.Min(bodyOffset, permitted);
                report.AppendLine($"{roots[f]} lengths={string.Join(",", lengths[f])}; hip={sourcePositions[f][0]}; sole={sole[f]}; ground={ground.point.y:R} {ground.collider.name}; maxBodyOffset={permitted:R}");
            }
            var landings = new System.Collections.Generic.List<(Vector3 offset, float range, float floor)>();
            for (int x = -10; x <= 10; x++)
            for (int z = -10; z <= 10; z++)
            {
                Vector3 shift = new Vector3(x * .05f, 0, z * .05f);
                var heights = new float[4];
                for (int f = 0; f < 4; f++)
                    heights[f] = Physics.RaycastAll(sole[f] + shift + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore)
                        .Where(h => !h.transform.IsChildOf(cat.transform)).OrderBy(h => h.distance).First().point.y;
                float range = heights.Max() - heights.Min();
                if (range <= .04f) landings.Add((shift, range, heights.Max()));
            }
            File.WriteAllLines(Folder + "landing-options.txt", landings.OrderBy(p => p.offset.sqrMagnitude).Take(12)
                .Select(p => $"offset={p.offset}; distance={p.offset.magnitude:R}; heightRange={p.range:R}; highestGround={p.floor:R}"));
        }
        Capture(cat, skin, baked, "before");
        foreach (Transform child in cat.transform)
            if (child.name == "CL_CAT_CORRECTED_Armature" || child.name == "CL_CAT_CORRECTED_Mesh")
                child.position += Vector3.up * bodyOffset;
        float worstLength = 0, worstGoal = 0;
        for (int f = 0; f < 4; f++)
        {
            var p = sourcePositions[f];
            Vector3 root = p[0] + Vector3.up * bodyOffset;
            Vector3 lowerDirection = ((p[3] - p[2]).normalized + (goals[f] - root).normalized).normalized;
            Vector3 hock = goals[f] - lowerDirection * lengths[f][2];
            Vector3 axis = hock - root;
            float distance = axis.magnitude;
            float a = lengths[f][0], b = lengths[f][1];
            if (distance >= a + b || distance <= Mathf.Abs(a - b))
                throw new Exception("Terrain goal outside upper-chain reach: " + roots[f] + " " + distance + " / " + (a + b));
            axis /= distance;
            Vector3 referenceAxis = (p[2] - p[0]).normalized;
            Vector3 pole = p[1] - p[0]; pole -= referenceAxis * Vector3.Dot(pole, referenceAxis);
            pole -= axis * Vector3.Dot(pole, axis); pole.Normalize();
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            Vector3 knee = root + axis * along + pole * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            Vector3[] solved = { root, knee, hock, goals[f] };
            for (int i = 0; i < 4; i++)
            {
                chains[f][i].position = solved[i];
                chains[f][i].rotation = i == 3 ? sourceRotations[f][i] : Quaternion.FromToRotation(p[i + 1] - p[i], solved[i + 1] - solved[i]) * sourceRotations[f][i];
            }
            for (int i = 0; i < 3; i++) worstLength = Mathf.Max(worstLength, Mathf.Abs(Vector3.Distance(chains[f][i].position, chains[f][i + 1].position) - lengths[f][i]));
            worstGoal = Mathf.Max(worstGoal, Vector3.Distance(chains[f][3].position, goals[f]));
        }
        skin.BakeMesh(baked, false);
        report.AppendLine($"bodyOffset={bodyOffset:R}; lengthError={worstLength:R}; goalError={worstGoal:R}; minBakedY={baked.vertices.Min(v => v.y):R}");
        File.WriteAllText(Folder + "result.txt", report.ToString());
        Capture(cat, skin, baked, "after");
        Object.DestroyImmediate(baked);
        Debug.Log("V12_TERRAIN_POSE_STUDY_FINISHED bodyOffset=" + bodyOffset + " lengthError=" + worstLength);
    }

    static void Capture(GameObject cat, SkinnedMeshRenderer skin, Mesh baked, string name)
    {
        skin.BakeMesh(baked, false);
        skin.enabled = false;
        var surface = new GameObject("Terrain pose evaluated surface");
        surface.AddComponent<MeshFilter>().sharedMesh = baked;
        surface.AddComponent<MeshRenderer>().sharedMaterial = skin.sharedMaterial;
        surface.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
        var camera = new GameObject("Terrain pose camera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = .95f;
        camera.transform.position = cat.transform.position + Vector3.right * 2.6f + Vector3.up * .65f + Vector3.back * .4f;
        camera.transform.LookAt(cat.transform.position + Vector3.up * .55f);
        var rt = new RenderTexture(1000, 700, 24); camera.targetTexture = rt;
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1000, 700, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1000, 700), 0, 0); image.Apply();
        File.WriteAllBytes(Folder + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; rt.Release();
        Object.DestroyImmediate(image); Object.DestroyImmediate(rt); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(surface);
    }
}
