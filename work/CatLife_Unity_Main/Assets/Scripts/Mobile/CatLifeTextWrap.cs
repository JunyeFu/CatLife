using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Display-only Chinese tail balancing; the saved suggestion stays unchanged.
public static class CatLifeTextWrap
{
    public static string WithoutSingleCharacterTail(Text label, string source)
    {
        var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
        settings.verticalOverflow = VerticalWrapMode.Overflow;
        var generator = new TextGenerator();
        generator.Populate(source, settings);
        var lines = generator.lines;
        if (lines.Count < 2) return source;
        int last = lines[lines.Count - 1].startCharIdx;
        if (last <= 0 || last >= source.Length || source[last - 1] == '\n') return source;
        string tail = source.Substring(last).Trim();
        if (new StringInfo(tail).LengthInTextElements != 1) return source;
        int[] elements = StringInfo.ParseCombiningCharacters(source);
        int previous = elements.Last(index => index < last);
        if (previous <= lines[lines.Count - 2].startCharIdx || string.IsNullOrWhiteSpace(source.Substring(previous, last - previous))) return source;
        return source.Insert(previous, "\n");
    }
}
