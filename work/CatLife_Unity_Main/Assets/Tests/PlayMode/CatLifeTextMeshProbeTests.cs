using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CatLifeTextProbeEffect : BaseMeshEffect
{
    public readonly List<UIVertex> vertices = new List<UIVertex>();
    public override void ModifyMesh(VertexHelper helper)
    {
        vertices.Clear();
        for (int i = 0; i < helper.currentVertCount; i++)
        { var vertex = new UIVertex(); helper.PopulateUIVertex(ref vertex, i); vertices.Add(vertex); }
    }
}

// Read the real uGUI mesh before choosing the glyph/Unicode mapping for WP06.7.
public sealed class CatLifeTextMeshProbeTests
{
    [Serializable] private sealed class Quad { public Vector2[] position, uv; }
    [Serializable] private sealed class Character { public Vector2 cursor; public float advance; }
    [Serializable] private sealed class Sample
    {
        public string text;
        public int utf16Length, meshVertices, nativeVertices;
        public int[] textElementStarts, lineStarts;
        public Character[] characters;
        public Quad[] quads;
    }
    [Serializable] private sealed class Report { public string font; public Sample[] samples; }

    [UnityTest]
    public IEnumerator ProbeActualGlyphMesh()
    {
        string output = Environment.GetEnvironmentVariable("CATLIFE_UI_CAPTURE_DIR");
        if (string.IsNullOrEmpty(output)) Assert.Ignore("Opt-in text mesh probe output is not configured.");
        const string key = "CatLife.Mobile.Data.v1";
        bool had = PlayerPrefs.HasKey(key); string saved = PlayerPrefs.GetString(key);
        try
        {
            var data = new CatLifeAppData(); data.settings.catAdopted = true; data.settings.aiEnabled = false;
            PlayerPrefs.SetString(key, CatLifeDataJson.Serialize(data));
            Type installer = Type.GetType("CatLifeUiStyleInstaller, Assembly-CSharp-Editor");
            yield return (AsyncOperation)installer.GetMethod("LoadReviewForCapture").Invoke(null, null);
            yield return null;
            var root = GameObject.Find("CatLifeMobileView");
            Font font = root.GetComponentsInChildren<Text>(true).First(t => t.name == "TodayText").font;
            var go = new GameObject("GlyphProbe", typeof(RectTransform), typeof(Text), typeof(CatLifeTextProbeEffect));
            go.transform.SetParent(root.transform, false);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = 34; text.supportRichText = false;
            text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(600, 320);
            var samples = new List<Sample>();
            foreach (string value in new[] { "甲乙丙丁", "甲 乙\n丙丁", "A\u0301甲\U0001F600乙", "来源：本地洞察\n完成这一小步。" })
            {
                text.text = value; Canvas.ForceUpdateCanvases(); yield return null; yield return null;
                var vertices = go.GetComponent<CatLifeTextProbeEffect>().vertices;
                Assert.That(vertices.Count % 4, Is.Zero);
                var quads = new List<Quad>();
                for (int i = 0; i < vertices.Count; i += 4)
                    quads.Add(new Quad { position = vertices.Skip(i).Take(4).Select(v => (Vector2)v.position).ToArray(), uv = vertices.Skip(i).Take(4).Select(v => (Vector2)v.uv0).ToArray() });
                samples.Add(new Sample {
                    text = value, utf16Length = value.Length, meshVertices = vertices.Count,
                    nativeVertices = text.cachedTextGenerator.verts.Count,
                    textElementStarts = StringInfo.ParseCombiningCharacters(value),
                    lineStarts = text.cachedTextGenerator.lines.Select(l => l.startCharIdx).ToArray(),
                    characters = text.cachedTextGenerator.characters.Select(c => new Character { cursor = c.cursorPos, advance = c.charWidth }).ToArray(),
                    quads = quads.ToArray()
                });
            }
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "glyph-mesh.json"), JsonUtility.ToJson(new Report { font = font.name, samples = samples.ToArray() }, true));
            UnityEngine.Object.Destroy(go);
        }
        finally
        {
            if (had) PlayerPrefs.SetString(key, saved); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
