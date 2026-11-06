"""Export the showcase's fixed copy as a small variable WOFF and shared UI color tokens."""
import argparse
import json
from html.parser import HTMLParser
from pathlib import Path
from fontTools import subset
from fontTools.ttLib import TTFont


class Copy(HTMLParser):
    def __init__(self):
        super().__init__(); self.text = []
    def handle_data(self, value):
        self.text.append(value)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--font-source", type=Path, required=True)
    parser.add_argument("--report-dir", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    target = root / "docs/showcase"
    tokens = json.loads((root / "work/CatLife_Unity_Main/Assets/MobileRuntime/UI/Style/CL_UI_StyleTokens.json").read_text(encoding="utf-8"))
    copy = Copy(); copy.feed((target / "layout.html").read_text(encoding="utf-8"))
    characters = set("".join(copy.text)) | set(chr(i) for i in range(32, 127))
    font = TTFont(args.font_source)
    required = {ord(c) for c in characters if not c.isspace()}
    missing = sorted(required - font.getBestCmap().keys())
    assert not missing, missing
    cutter = subset.Subsetter(); cutter.populate(unicodes=required); cutter.subset(font)
    for name_id, name in {1: "CatLife Showcase", 3: "CatLife Showcase Variable v1", 4: "CatLife Showcase Variable", 6: "CatLifeShowcase-Variable", 16: "CatLife Showcase"}.items():
        font["name"].removeNames(nameID=name_id); font["name"].setName(name, name_id, 3, 1, 0x409)
    fonts = target / "fonts"; fonts.mkdir(exist_ok=True)
    output = fonts / "CatLifeShowcase-VF.woff"; font.flavor = "woff"; font.save(output)
    license_text = (args.font_source.parent / "OFL-NotoSansSC.txt").read_text(encoding="utf-8")
    (fonts / "OFL-CatLifeShowcase.txt").write_text("\n".join(line.rstrip() for line in license_text.splitlines()) + "\n", encoding="utf-8")
    exported = TTFont(output); cmap = exported.getBestCmap()
    widths = [exported["hmtx"][cmap[ord(c)]][0] for c in "0123456789"]
    assert len(set(widths)) == 1
    css = "/* Generated from CL_UI_StyleTokens.json by tools/showcase/prepare-style.py. */\n:root{" + ";".join(f"--{key}:{value}" for key, value in tokens["colors"].items()) + "}\n"
    css += '@font-face{font-family:CatLifeShowcase;src:url("fonts/CatLifeShowcase-VF.woff") format("woff");font-weight:100 900;font-style:normal;font-display:swap}\n'
    (target / "style-tokens.css").write_text(css, encoding="utf-8")
    args.report_dir.mkdir(parents=True, exist_ok=True)
    report = {"font_source": str(args.font_source), "output": str(output), "bytes": output.stat().st_size, "codepoints": len(cmap), "missing": missing,
              "axes": [{"tag": a.axisTag, "min": a.minValue, "max": a.maxValue} for a in exported["fvar"].axes], "digit_advance": widths,
              "scope": "showcase artwork typography; app screenshots are unchanged"}
    (args.report_dir / "web-font.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
