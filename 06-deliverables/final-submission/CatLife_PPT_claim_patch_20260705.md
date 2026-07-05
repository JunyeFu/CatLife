# CatLife PPT Claim Patch Report

Generated: 2026-07-05 08:27:39
PPT: `C:\Users\fujunye\Desktop\Agent\05-AIGC\06-deliverables\final-submission\CatLife_作品介绍PPT_v1.pptx`
Before SHA256: `603D667D706FAAE28EB02BE7E7D9A165A862E4F55835019B8B09603ADF969EE1`
After SHA256: `74FC37D7C88E0ED7C77F5731C7B71CA33165662048BA5D56148A82536B9319AC`
Replacement count: `4`

## Changes

| Slide XML | Count | Before | After | Reason |
|---|---:|---|---|---|
| ppt/slides/slide7.xml | 1 | `大模型驱动猫咪行为` | `大模型提供行为偏置` | Reduce LLM wording from direct behavior driving to safe high-level bias. |
| ppt/slides/slide18.xml | 1 | `图一：森林场景普通状态概念图` | `图一：历史概念场景普通状态图（不进入当前APK）` | Mark forest ordinary-state material as historical concept only. |
| ppt/slides/slide18.xml | 1 | `图二：森林场景专注状态概念图` | `图二：历史概念场景专注状态图（不进入当前APK）` | Mark forest focus-state material as historical concept only. |
| ppt/slides/slide19.xml | 1 | `十五、场景设计简介与预览：猫咪小镇场景预览图与森林场景资产展示` | `十五、场景设计简介与预览：猫咪小镇场景预览图与历史概念资产展示` | Remove forest-scene wording from the visual-only scene preview title. |

## Scope

- This patch only changes extractable PPT slide XML text.
- It does not edit bitmap text embedded in images.
- Re-run `audit-ppt-claims.ps1 -AllowHits` and refresh the PPT manifest after patching.
