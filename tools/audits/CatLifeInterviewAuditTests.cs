using System;
using System.Collections;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Adversarial acceptance probes. Failing cases document defects; do not weaken them to pass.
public sealed class CatLifeInterviewAuditTests
{
    private const string DataKey = "CatLife.Mobile.Data.v1";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private string savedData;
    private bool hadData;

#if UNITY_EDITOR
    private UnityEditor.InsecureHttpOption originalHttp;
    [OneTimeSetUp] public void PermitLoopbackTransportForAudit()
    {
        originalHttp = UnityEditor.PlayerSettings.insecureHttpOption;
        UnityEditor.PlayerSettings.insecureHttpOption = UnityEditor.InsecureHttpOption.DevelopmentOnly;
    }
    [OneTimeTearDown] public void RestoreTransportPolicy()
    {
        UnityEditor.PlayerSettings.insecureHttpOption = originalHttp;
    }
#endif

    [SetUp] public void PreserveData()
    {
        hadData = PlayerPrefs.HasKey(DataKey);
        savedData = PlayerPrefs.GetString(DataKey, "");
    }

    [TearDown] public void RestoreData()
    {
        if (hadData) PlayerPrefs.SetString(DataKey, savedData);
        else PlayerPrefs.DeleteKey(DataKey);
        PlayerPrefs.Save();
    }

    [TestCase("你患有抑郁症，应该停止服药")]
    [TestCase("你太没用了，不继续学我就离开你")]
    [TestCase("我能判断你有焦虑症，这是诊断结果")]
    public void ChineseUnsafeAdviceMustBeRejectedDespiteFalseSafetyFlags(string text)
    {
        Type suggestionType = Type.GetType("CatLife.LLM.LLMBehaviorSuggestion, Assembly-CSharp");
        object raw = Activator.CreateInstance(suggestionType);
        suggestionType.GetField("gentleReminder").SetValue(raw, text);
        suggestionType.GetField("returnSuggestion").SetValue(raw, "写下下一小步");
        suggestionType.GetField("catReactionReason").SetValue(raw, "安静陪伴");
        object[] args = { raw, null, null };
        bool accepted = (bool)suggestionType.GetMethod("TryBuildSafe").Invoke(null, args);
        Assert.That(accepted, Is.False, "Chinese medical inference or coercion reached the accepted output boundary: " + args[2]);
    }

    [Test]
    public void ExternalActionCommandIsRejected()
    {
        Type type = Type.GetType("CatLife.LLM.LLMBehaviorSuggestion, Assembly-CSharp");
        object raw = Activator.CreateInstance(type);
        type.GetField("animatorCommandIncluded").SetValue(raw, true);
        object[] args = { raw, null, null };
        Assert.That((bool)type.GetMethod("TryBuildSafe").Invoke(null, args), Is.False);
    }

    [Test]
    public void BackwardWallClockMustNotExtendBeyondPlannedDuration()
    {
        var data = new CatLifeAppData();
        var controller = new CatLifeSessionController(data);
        controller.BeginTransition(1500, 10000);
        controller.EnterFocus(10000);
        int remaining = controller.RemainingSeconds(6400); // device clock moves back one hour
        Assert.That(remaining, Is.LessThanOrEqualTo(1500), "Clock rollback extended a 25-minute session.");
    }

    [TestCase("{")]
    [TestCase("null")]
    [TestCase("{\"records\":null,\"settings\":null}")]
    public void InvalidSavedDataMustNotReachBootAsUnhandledOrNullState(string json)
    {
        CatLifeAppData loaded = null;
        Assert.DoesNotThrow(() => loaded = CatLifeDataJson.Deserialize(json),
            "Awake calls this deserializer directly; corruption needs an explicit recovery/error path.");
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.records, Is.Not.Null);
        Assert.That(loaded.settings, Is.Not.Null);
    }

    [Test]
    public void CompletionRemainsExactlyOnceAfterRestore()
    {
        var data = new CatLifeAppData();
        var controller = new CatLifeSessionController(data);
        controller.BeginTransition(60, 10000);
        controller.EnterFocus(10000);
        Assert.That(controller.TryComplete(10060, out var record), Is.True);
        Assert.That(controller.TryComplete(10061, out _), Is.False);
        data = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data));
        controller = new CatLifeSessionController(data);
        Assert.That(controller.TryComplete(10062, out _), Is.False);
        Assert.That(data.records.Count, Is.EqualTo(1));
        Assert.That(record.GrowthAwarded, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator TurningAiOffMustPreventPendingResponseFromBeingStored()
    {
        var initial = new CatLifeAppData();
        initial.settings.catAdopted = true;
        PlayerPrefs.SetString(DataKey, CatLifeDataJson.Serialize(initial));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var data = (CatLifeAppData)app.GetType().GetField("data", Private).GetValue(app);
        data.settings.aiEnabled = true;
        data.settings.aiConsent = true;
        var record = new CatLifeSessionRecord { completed = true, plannedSeconds = 60,
            actualSeconds = 60, aiRequestAttempted = true, aiRequestCount = 1,
            aiRequestStatus = "requesting" };
        data.records.Add(record);
        app.GetType().GetMethod("ToggleAi", Private).Invoke(app, null);
        Assert.That(data.settings.aiEnabled, Is.False);
        Component client = app.GetComponent("MockCatLLMClient");
        client.GetType().GetField("<LastSource>k__BackingField", Private).SetValue(client, "mimo_cloud");
        Type type = client.GetType().Assembly.GetType("CatLife.LLM.LLMBehaviorSuggestion");
        object suggestion = Activator.CreateInstance(type);
        type.GetField("gentleReminder").SetValue(suggestion, "会话已完成");
        type.GetField("returnSuggestion").SetValue(suggestion, "写下下一步");
        type.GetField("catReactionReason").SetValue(suggestion, "安静陪伴");
        // Inject a completed transport result; no network request or credential is used.
        app.GetType().GetMethod("ApplyAiSuggestion", Private).Invoke(app, new[] { data, (object)record, suggestion });
        Assert.That(record.aiRequestStatus, Is.Not.EqualTo("succeeded"),
            "A response arriving after AI was disabled was still committed.");
        Assert.That(record.aiGentleReminder, Is.Null.Or.Empty);
    }

    [UnityTest] public IEnumerator RealTransport429CallsErrorOnce() =>
        CheckTransport(25487, 429, "{}", 0, "generic_cloud_network_429");

    [UnityTest] public IEnumerator RealTransportMalformed200CallsErrorOnce() =>
        CheckTransport(25488, 200, "not-json", 0, "generic_cloud_response_parse_");

    [UnityTest] public IEnumerator RealTransportTimeoutCallsErrorOnce() =>
        CheckTransport(25489, 200, "{}", 2500, "generic_cloud_network_");

    private IEnumerator CheckTransport(int port, int status, string body, int delayMs, string errorPrefix)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        int requests = 0;
        Task server = Task.Run(() =>
        {
            using (TcpClient peer = listener.AcceptTcpClient())
            {
                Interlocked.Increment(ref requests);
                using (var stream = peer.GetStream())
                {
                    var buffer = new byte[8192];
                    stream.Read(buffer, 0, buffer.Length);
                    if (delayMs > 0) Thread.Sleep(delayMs);
                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " Audit\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
                    try { stream.Write(headers, 0, headers.Length); stream.Write(bytes, 0, bytes.Length); }
                    catch (System.IO.IOException) when (delayMs > 0) { /* expected after client timeout */ }
                }
            }
        });
        GameObject host = new GameObject("LocalTransportAudit");
        try
        {
            Type clientType = Type.GetType("CatLife.LLM.MockCatLLMClient, Assembly-CSharp");
            Component client = host.AddComponent(clientType);
            clientType.GetField("cloudTimeoutSeconds", Private).SetValue(client, 1);
            Type configType = clientType.Assembly.GetType("CatLife.LLM.GenericCloudConfig");
            object config = Activator.CreateInstance(configType);
            configType.GetField("apiEndpoint").SetValue(config, "http://127.0.0.1:" + port + "/");
            configType.GetField("apiKey").SetValue(config, "AUDIT_PLACEHOLDER_NOT_A_SECRET");
            object context = Activator.CreateInstance(clientType.Assembly.GetType("CatLife.LLM.CatPromptContext"));
            string error = null; int errors = 0;
            Action<string> onError = value => { error = value; errors++; };
            var flow = (IEnumerator)clientType.GetMethod("CoRequestGenericCloud", Private)
                .Invoke(client, new object[] { config, context, null, onError });
            yield return flow;
            Assert.That(error, Does.StartWith(errorPrefix));
            Assert.That(errors, Is.EqualTo(1));
            Assert.That(requests, Is.EqualTo(1));
            while (!server.IsCompleted) yield return null;
            if (server.IsFaulted) throw server.Exception;
        }
        finally
        {
            listener.Stop();
            UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
