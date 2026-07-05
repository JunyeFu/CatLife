# -*- coding: utf-8 -*-
"""Extract and audit CatLife PPT text for claim/evidence alignment."""

from __future__ import annotations

import argparse
import datetime as _dt
import hashlib
import html
import re
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET


TEXT_NS = "{http://schemas.openxmlformats.org/drawingml/2006/main}t"


RULES = [
    {
        "id": "forest_scope",
        "title": "Forest scene wording must be concept-only",
        "severity": "manual_review",
        "patterns": [r"森林", r"专注森林", r"forest"],
        "guidance": "Current product rule is no forest scene. Keep any forest wording or visual as concept/history, not current runtime scope.",
    },
    {
        "id": "bluelm_completed_claim",
        "title": "Do not claim completed BlueLM on-device SDK without logcat evidence",
        "severity": "high",
        "patterns": [
            r"蓝心端侧\s*SDK\s*已完成",
            r"端侧\s*SDK\s*已完成",
            r"已完成\s*蓝心端侧",
            r"蓝心\s*3B\s*已接入",
            r"端侧模型\s*已完成",
            r"on[- ]device\s+SDK\s+completed",
        ],
        "guidance": "Use wording such as code-level bridge / cloud demo / fallback until cloud-device or on-device logcat evidence exists.",
    },
    {
        "id": "android_background_completed_claim",
        "title": "Do not claim completed true Android background recognition without evidence",
        "severity": "high",
        "patterns": [
            r"真实\s*Android\s*后台",
            r"后台行为识别\s*已完成",
            r"跨应用\s*行为识别\s*已完成",
            r"UsageStats\s*已接入",
            r"读取全手机",
            r"监控全手机",
        ],
        "guidance": "Current safe scope is app-internal aggregated events plus privacy-friendly pause/resume signals, unless install/logcat evidence proves more.",
    },
    {
        "id": "user_validation_completed_claim",
        "title": "User validation claims need real anonymized evidence",
        "severity": "medium",
        "patterns": [
            r"用户验证\s*已完成",
            r"已完成\s*用户访谈",
            r"\b5\s*份\s*访谈",
            r"问卷\s*结果",
            r"用户反馈\s*数据",
        ],
        "guidance": "If real anonymized feedback is unavailable, present this as planned validation or remove completed-data wording.",
    },
    {
        "id": "llm_overclaim",
        "title": "LLM should not be described as directly controlling cat transforms",
        "severity": "medium",
        "patterns": [
            r"大模型\s*直接\s*控制",
            r"LLM\s*直接\s*控制",
            r"大模型驱动猫咪行动",
            r"大模型驱动猫咪行为",
        ],
        "guidance": "Use: LLM provides safe text and high-level behavior bias; Unity local rules own movement, navigation, and animation.",
    },
    {
        "id": "privacy_redline",
        "title": "PPT must not imply raw content collection",
        "severity": "high",
        "patterns": [
            r"读取输入内容",
            r"读取聊天",
            r"读取剪贴板",
            r"录屏识别",
            r"收集应用内容",
        ],
        "guidance": "Public claim should be privacy-friendly aggregated features only: no raw text, no screenshots, no cross-app content.",
    },
]


def natural_slide_key(name: str) -> tuple[int, str]:
    match = re.search(r"slide(\d+)\.xml$", name)
    if match:
        return (int(match.group(1)), name)
    return (10**9, name)


def extract_slide_text(pptx: Path) -> list[dict[str, object]]:
    slides: list[dict[str, object]] = []
    with zipfile.ZipFile(pptx) as zf:
        names = sorted(
            [n for n in zf.namelist() if n.startswith("ppt/slides/slide") and n.endswith(".xml")],
            key=natural_slide_key,
        )
        for index, name in enumerate(names, start=1):
            xml = zf.read(name)
            root = ET.fromstring(xml)
            parts = []
            for node in root.iter(TEXT_NS):
                if node.text:
                    parts.append(node.text)
            text = " ".join(part.strip() for part in parts if part.strip())
            slides.append({"index": index, "path": name, "text": text})
    return slides


def audit(slides: list[dict[str, object]]) -> list[dict[str, str]]:
    hits: list[dict[str, str]] = []
    for slide in slides:
        text = str(slide["text"])
        for rule in RULES:
            for pattern in rule["patterns"]:
                if re.search(pattern, text, flags=re.IGNORECASE):
                    excerpt = text
                    if len(excerpt) > 180:
                        excerpt = excerpt[:177] + "..."
                    hits.append(
                        {
                            "slide": str(slide["index"]),
                            "rule": rule["id"],
                            "severity": rule["severity"],
                            "title": rule["title"],
                            "pattern": pattern,
                            "excerpt": excerpt,
                            "guidance": rule["guidance"],
                        }
                    )
                    break
    return hits


def escape_table(value: str) -> str:
    return html.escape(value).replace("|", "/").replace("\n", " ")


def write_text_extract(path: Path, slides: list[dict[str, object]]) -> None:
    lines = ["# CatLife PPT Extracted Text", ""]
    for slide in slides:
        lines.append(f"## Slide {slide['index']}")
        lines.append("")
        lines.append(str(slide["text"]) if slide["text"] else "(no text extracted)")
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def write_report(path: Path, pptx: Path, slides: list[dict[str, object]], hits: list[dict[str, str]]) -> None:
    digest = hashlib.sha256(pptx.read_bytes()).hexdigest().upper()
    high = sum(1 for h in hits if h["severity"] == "high")
    medium = sum(1 for h in hits if h["severity"] == "medium")
    manual = sum(1 for h in hits if h["severity"] == "manual_review")
    status = "PASS" if not hits else ("BLOCKED_BY_HIGH_RISK_CLAIMS" if high else "MANUAL_REVIEW_REQUIRED")

    lines = [
        "# CatLife PPT Claim Audit",
        "",
        f"Generated: {_dt.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"PPT: `{pptx}`",
        f"SHA256: `{digest}`",
        f"Slides extracted: `{len(slides)}`",
        "",
        "## Summary",
        "",
        f"- Status: `{status}`",
        f"- High-risk hits: `{high}`",
        f"- Medium-risk hits: `{medium}`",
        f"- Manual-review hits: `{manual}`",
        "",
    ]

    if not hits:
        lines.extend(
            [
                "No configured high-risk claim patterns were found in extracted slide text.",
                "This does not replace visual/manual PPT review.",
                "",
            ]
        )
    else:
        lines.extend(
            [
                "## Claim Hits",
                "",
                "| Slide | Severity | Rule | Matched pattern | Evidence excerpt | Required action |",
                "|---:|---|---|---|---|---|",
            ]
        )
        for hit in hits:
            lines.append(
                "| {slide} | {severity} | {title} | `{pattern}` | {excerpt} | {guidance} |".format(
                    slide=hit["slide"],
                    severity=escape_table(hit["severity"]),
                    title=escape_table(hit["title"]),
                    pattern=escape_table(hit["pattern"]),
                    excerpt=escape_table(hit["excerpt"]),
                    guidance=escape_table(hit["guidance"]),
                )
            )
        lines.append("")

    lines.extend(
        [
            "## Scope",
            "",
            "- This audit only checks extractable slide text inside the PPTX.",
            "- It cannot inspect embedded bitmap text, speaker narration, or visual-only claims.",
            "- Final closure still requires manual PPT review against the current Unity/APK evidence.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pptx", required=True)
    parser.add_argument("--report", required=True)
    parser.add_argument("--text-out", required=True)
    parser.add_argument("--allow-hits", action="store_true")
    args = parser.parse_args()

    pptx = Path(args.pptx)
    report = Path(args.report)
    text_out = Path(args.text_out)
    if not pptx.exists():
        print(f"PPTX not found: {pptx}", file=sys.stderr)
        return 2

    report.parent.mkdir(parents=True, exist_ok=True)
    text_out.parent.mkdir(parents=True, exist_ok=True)
    slides = extract_slide_text(pptx)
    hits = audit(slides)
    write_text_extract(text_out, slides)
    write_report(report, pptx, slides, hits)
    print(f"Wrote {report}")
    print(f"Wrote {text_out}")
    if hits and not args.allow_hits:
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
