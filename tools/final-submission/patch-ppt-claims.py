# -*- coding: utf-8 -*-
"""Patch final CatLife PPT extractable claim text to match current evidence."""

from __future__ import annotations

import argparse
import datetime as _dt
import hashlib
import shutil
import sys
import tempfile
import zipfile
from pathlib import Path


REPLACEMENTS = [
    (
        "大模型驱动猫咪行为",
        "大模型提供行为偏置",
        "Reduce LLM wording from direct behavior driving to safe high-level bias.",
    ),
    (
        "图一：森林场景普通状态概念图",
        "图一：历史概念场景普通状态图（不进入当前APK）",
        "Mark forest ordinary-state material as historical concept only.",
    ),
    (
        "图二：森林场景专注状态概念图",
        "图二：历史概念场景专注状态图（不进入当前APK）",
        "Mark forest focus-state material as historical concept only.",
    ),
    (
        "十五、场景设计简介与预览：猫咪小镇场景预览图与森林场景资产展示",
        "十五、场景设计简介与预览：猫咪小镇场景预览图与历史概念资产展示",
        "Remove forest-scene wording from the visual-only scene preview title.",
    ),
]


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def patch_pptx(pptx: Path, *, backup: bool) -> tuple[list[dict[str, object]], str, str]:
    before_hash = sha256(pptx)
    backup_path = ""
    if backup:
        backup_file = pptx.with_suffix(pptx.suffix + ".bak")
        shutil.copy2(pptx, backup_file)
        backup_path = str(backup_file)

    changes: list[dict[str, object]] = []
    temp = tempfile.NamedTemporaryFile(prefix="catlife-ppt-patch-", suffix=".pptx", delete=False)
    temp_name = temp.name
    temp.close()
    try:
        with zipfile.ZipFile(pptx, "r") as src, zipfile.ZipFile(temp_name, "w") as dst:
            for info in src.infolist():
                data = src.read(info.filename)
                if info.filename.startswith("ppt/slides/slide") and info.filename.endswith(".xml"):
                    text = data.decode("utf-8")
                    original = text
                    for old, new, reason in REPLACEMENTS:
                        count = text.count(old)
                        if count:
                            text = text.replace(old, new)
                            changes.append(
                                {
                                    "entry": info.filename,
                                    "old": old,
                                    "new": new,
                                    "count": count,
                                    "reason": reason,
                                }
                            )
                    if text != original:
                        data = text.encode("utf-8")
                dst.writestr(info, data)
        shutil.move(temp_name, pptx)
    finally:
        Path(temp_name).unlink(missing_ok=True)

    after_hash = sha256(pptx)
    return changes, before_hash, after_hash if changes else before_hash


def write_report(path: Path, pptx: Path, changes: list[dict[str, object]], before_hash: str, after_hash: str) -> None:
    lines = [
        "# CatLife PPT Claim Patch Report",
        "",
        f"Generated: {_dt.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"PPT: `{pptx}`",
        f"Before SHA256: `{before_hash}`",
        f"After SHA256: `{after_hash}`",
        f"Replacement count: `{sum(int(c['count']) for c in changes)}`",
        "",
        "## Changes",
        "",
    ]
    if not changes:
        lines.append("No configured text replacements were needed.")
    else:
        lines.extend(["| Slide XML | Count | Before | After | Reason |", "|---|---:|---|---|---|"])
        for change in changes:
            lines.append(
                "| {entry} | {count} | `{old}` | `{new}` | {reason} |".format(
                    entry=change["entry"],
                    count=change["count"],
                    old=str(change["old"]).replace("|", "/"),
                    new=str(change["new"]).replace("|", "/"),
                    reason=str(change["reason"]).replace("|", "/"),
                )
            )
    lines.extend(
        [
            "",
            "## Scope",
            "",
            "- This patch only changes extractable PPT slide XML text.",
            "- It does not edit bitmap text embedded in images.",
            "- Re-run `audit-ppt-claims.ps1 -AllowHits` and refresh the PPT manifest after patching.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pptx", required=True)
    parser.add_argument("--report", required=True)
    parser.add_argument("--no-backup", action="store_true")
    args = parser.parse_args()

    pptx = Path(args.pptx)
    if not pptx.exists():
        print(f"PPTX not found: {pptx}", file=sys.stderr)
        return 2

    report = Path(args.report)
    report.parent.mkdir(parents=True, exist_ok=True)
    changes, before_hash, after_hash = patch_pptx(pptx, backup=not args.no_backup)
    write_report(report, pptx, changes, before_hash, after_hash)
    print(f"Wrote {report}")
    print(f"Patched replacements: {sum(int(c['count']) for c in changes)}")
    if not changes:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
