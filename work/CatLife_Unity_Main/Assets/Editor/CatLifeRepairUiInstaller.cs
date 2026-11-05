using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Incremental additions to the approved UI; existing cards and buttons keep their geometry.
public static class CatLifeRepairUiInstaller
{
    public static void Install()
    {
        var paths = new System.Collections.Generic.HashSet<string> { CatLifeMobileUiPrefabBuilder.PrefabPath };
        foreach (string scene in new[] { "Assets/Scenes/CatLifeMobile.unity", CatLifeFigmaUiInstaller.ScenePath,
            "Assets/Editor/CatProneReview/NavigationTurnCandidate/CatLifeNavigationTurnReview.unity" })
            if (System.IO.File.Exists(scene))
                foreach (string dependency in AssetDatabase.GetDependencies(scene, true).Where(p => p.EndsWith(".prefab")))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dependency);
                    if (prefab.GetComponent<CatLifeMobileUiPresentation>() != null) paths.Add(dependency);
                }
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            Apply(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    public static void Apply(GameObject root)
    {
        Transform Find(string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
        Find("CatNameText").GetComponent<Text>().supportRichText = false;
        Find("BubbleText").GetComponent<Text>().supportRichText = false;
        var growth = Find("GrowthText").GetComponent<Text>();
        if (!root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "GrowthNameText"))
        {
            var name = Object.Instantiate(growth, growth.transform.parent); name.name = "GrowthNameText";
            name.supportRichText = false; name.text = "";
            var rect = name.rectTransform; rect.anchorMin = new Vector2(rect.anchorMin.x, .60f); rect.anchorMax = new Vector2(rect.anchorMax.x, .91f);
            var level = growth.rectTransform; level.anchorMin = new Vector2(level.anchorMin.x, .30f); level.anchorMax = new Vector2(level.anchorMax.x, .60f);
        }
        if (!root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ClockRecoveryPanel"))
        {
            var panel = Object.Instantiate(Find("AiConsentPanel").gameObject, Find("TransientLayer")); panel.name = "ClockRecoveryPanel";
            var text = panel.GetComponentsInChildren<Text>(true).First(t => t.name == "AiConsentPanelText");
            text.name = "ClockRecoveryText"; text.supportRichText = false;
            text.text = "设备时间发生变化\n无法确定离线经过时间。\n可从计划时长上限继续，或确认中断（奖励为零）。";
            var buttons = panel.GetComponentsInChildren<Button>(true);
            buttons[0].name = "ClockInterrupt"; buttons[0].GetComponentInChildren<Text>().text = "确认中断";
            buttons[1].name = "ClockContinue"; buttons[1].GetComponentInChildren<Text>().text = "继续上限时长";
            panel.GetComponent<Image>().raycastTarget = true; panel.SetActive(false);
        }
        if (!root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "SaveRecoveryPanel"))
        {
            var panel = Object.Instantiate(Find("AiConsentPanel").gameObject, Find("TransientLayer"));
            panel.name = "SaveRecoveryPanel";
            var text = panel.GetComponentsInChildren<Text>(true).First(t => t.name == "AiConsentPanelText");
            text.name = "SaveRecoveryText"; text.supportRichText = false;
            var card = text.transform.parent.GetComponent<RectTransform>();
            card.anchorMin = new Vector2(.08f, .24f); card.anchorMax = new Vector2(.92f, .76f);
            var rect = text.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.08f, .43f); rect.anchorMax = new Vector2(.92f, .95f);
            var buttons = panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < 3; i++)
            {
                var button = i < 2 ? buttons[i] : Object.Instantiate(buttons[1], card);
                button.name = new[] { "RecoveryRetry", "RecoveryExport", "RecoveryClear" }[i];
                var label = button.GetComponentInChildren<Text>(); label.name = button.name + "Text";
                label.text = new[] { "重新读取", "导出原始数据", "确认清除" }[i]; label.supportRichText = false;
                var buttonRect = button.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(.08f, .30f - i * .10f); buttonRect.anchorMax = new Vector2(.92f, .39f - i * .10f);
            }
            panel.GetComponent<Image>().raycastTarget = true;
            panel.SetActive(false);
        }
    }
}
