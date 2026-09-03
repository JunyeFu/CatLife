using System.Collections;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CatLifeMobileFlowTests
{
    [Test]
    public void CameraDirectorExposesApprovedFixedPresets()
    {
        System.Type type = System.Type.GetType("CatLifeCameraDirector, Assembly-CSharp");
        GameObject go = new GameObject("Camera", typeof(Camera));
        Component director = go.AddComponent(type);
        type.GetMethod("Configure").Invoke(director, new object[] { go.GetComponent<Camera>() });
        object home = type.GetMethod("GetPreset").Invoke(director, new object[] { CatLifeSessionPhase.Normal });
        object focus = type.GetMethod("GetPreset").Invoke(director, new object[] { CatLifeSessionPhase.Focus });
        object transition = type.GetMethod("GetPreset").Invoke(director, new object[] { CatLifeSessionPhase.Transition });
        Assert.That((Vector3)home.GetType().GetProperty("Position").GetValue(home), Is.EqualTo(new Vector3(.1f, 1.9f, 1.2f)));
        Assert.That((float)focus.GetType().GetProperty("Fov").GetValue(focus), Is.EqualTo(72f));
        Assert.That((float)transition.GetType().GetProperty("Duration").GetValue(transition), Is.EqualTo(2f));
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GenericCloudConfigUsesMiMoDefaults()
    {
        System.Type configType = System.Type.GetType("CatLife.LLM.GenericCloudConfig, Assembly-CSharp");
        Assert.That(configType, Is.Not.Null);
        object config = System.Activator.CreateInstance(configType);
        Assert.That((string)configType.GetField("provider").GetValue(config), Is.EqualTo("mimo"));
        Assert.That((string)configType.GetField("apiEndpoint").GetValue(config), Is.EqualTo("https://api.xiaomimimo.com/v1/chat/completions"));
        Assert.That((string)configType.GetField("model").GetValue(config), Is.EqualTo("mimo-v2.5"));
        Assert.That(configType.GetField("apiKey"), Is.Not.Null);
    }

    [Test]
    public void GenericCloudParserAcceptsPlainTextWithoutRetry()
    {
        System.Type clientType = System.Type.GetType("CatLife.LLM.MockCatLLMClient, Assembly-CSharp");
        MethodInfo parse = clientType.GetMethod("TryParseGenericCloudSuggestion", BindingFlags.Static | BindingFlags.Public);
        object[] args = { "今天也专注得很好，继续保持。", null, null };
        bool accepted = (bool)parse.Invoke(null, args);
        Assert.That(accepted, Is.True);
        Assert.That(args[1], Is.Not.Null);
        Assert.That((string)args[2], Does.StartWith("passed"));
    }

    [Test]
    public void MiMoRequestDisablesThinkingAndRequestsJson()
    {
        System.Type clientType = System.Type.GetType("CatLife.LLM.MockCatLLMClient, Assembly-CSharp");
        MethodInfo build = clientType.GetMethod("BuildGenericCloudRequestJson", BindingFlags.Static | BindingFlags.Public);
        Assert.That(build, Is.Not.Null);
        string json = (string)build.Invoke(null, new object[] { "mimo-v2.5", null });
        StringAssert.Contains("\"thinking\":{\"type\":\"disabled\"}", json);
        StringAssert.Contains("\"response_format\":{\"type\":\"json_object\"}", json);
    }

    [UnityTest]
    public IEnumerator MobileSceneRoutesPagesAndStartsAfterTransition()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        Assert.That(app, Is.Not.Null);
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("HomeHudLayer"));
        Click("RecordsButton");
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("RecordsPanel"));
        Click("RecordsBack");
        Click("StartButton");
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("SetupPanel"));
        Click("SetupStart");
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Transition));
        yield return new WaitForSecondsRealtime(2.1f);
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Focus));
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("FocusPanel"));
    }

    [UnityTest]
    public IEnumerator SceneContainsStableLandmarkEntrypointsAndAnimatorStates()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Assert.That(GameObject.Find("CL_BLD_TomatoClockTower_01").GetComponent("CatLifeLandmark"), Is.Not.Null);
        Assert.That(GameObject.Find("CL_BLD_CatHouse_01").GetComponent("CatLifeLandmark"), Is.Not.Null);
        Animator animator = GameObject.Find("CatLifeMobileCat").GetComponentInChildren<Animator>();
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer.CL_CAT_FocusRest_v01_loop_96f")), Is.True);
    }

    [UnityTest]
    public IEnumerator MobileSceneWiresRecognitionThroughSingleRuntimeSystemsRoot()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        GameObject systems = GameObject.Find("CatLifeRuntimeSystems");
        Assert.That(systems, Is.Not.Null);
        Assert.That(systems.GetComponent("RealtimeFeatureEngine"), Is.Not.Null);
        Assert.That(systems.GetComponent("MockRecognitionProvider"), Is.Not.Null);
        Component coordinator = systems.GetComponent("CatLifeMobileRuntimeCoordinator");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileRuntimeCoordinator"), Is.Null);

        Component features = systems.GetComponent("RealtimeFeatureEngine");
        coordinator.GetType().GetMethod("RecordUiEvent").Invoke(coordinator, new object[] { "page_enter_contract_test" });
        Assert.That((string)features.GetType().GetProperty("LastAcceptedBehaviorEvent").GetValue(features), Is.EqualTo("page_enter_contract_test"));
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Focus });
        object snapshot = features.GetType().GetProperty("Latest").GetValue(features);
        Assert.That((bool)snapshot.GetType().GetField("isFocusSessionActive").GetValue(snapshot), Is.True);
    }

    private static void Click(string name)
    {
        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (button.name == name) { button.onClick.Invoke(); return; }
        Assert.Fail("Button not found: " + name);
    }

    private static T Property<T>(Component component, string name) { return (T)component.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetValue(component); }
}
