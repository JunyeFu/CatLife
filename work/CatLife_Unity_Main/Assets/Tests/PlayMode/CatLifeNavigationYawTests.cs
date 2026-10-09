using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using CatLife.Mobile;
using System.IO;
using System.Text;

public sealed class CatLifeNavigationYawTests
{
    [UnityTest]
    public IEnumerator ObserveStatisticsOffRoamingHeading()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        data.settings.localBehaviorStatsEnabled = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Camera.main.aspect = 1080f / 2424f;
        var cat = GameObject.Find("CatLifeMobileCat");
        var body = cat.GetComponent<Rigidbody>();
        var agent = cat.GetComponent<NavMeshAgent>();
        var animator = cat.GetComponentInChildren<Animator>();
        var report = new StringBuilder("seconds,position,yaw,desired,velocity,animatorLocalEuler,clip\n");
        for (int i = 0; i < 60; i++)
        {
            yield return new WaitForSeconds(.5f);
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            report.AppendLine($"{(i+1)*.5f:F1},\"{body.position:F3}\",{body.rotation.eulerAngles.y:F2},\"{agent.desiredVelocity:F3}\",\"{body.linearVelocity:F3}\",\"{animator.transform.localEulerAngles:F2}\",{clips[0].clip.name}");
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp03-r6-stats-off-heading.csv")), report.ToString());
        Assert.That(cat.activeInHierarchy, Is.True);
    }

    [UnityTest]
    public IEnumerator CompareFrozenYawWithUprightOnlyConstraints()
    {
        foreach (var constraints in new[] { RigidbodyConstraints.FreezeRotation,
            RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ })
        {
            var item = new GameObject("Cat navigation yaw probe");
            var body = item.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = constraints;
            for (int i = 0; i < 20; i++)
            {
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.Euler(0, 90, 0), 8.4f));
                yield return new WaitForFixedUpdate();
            }
            Debug.Log("CATLIFE_YAW_PROBE constraints=" + constraints + " yaw=" + body.rotation.eulerAngles.y);
            if (constraints != RigidbodyConstraints.FreezeRotation)
                Assert.That(Quaternion.Angle(body.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(1f));
            Object.Destroy(item);
            yield return null;
        }
    }
}
