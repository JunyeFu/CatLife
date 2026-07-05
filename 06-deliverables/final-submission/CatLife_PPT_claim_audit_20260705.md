# CatLife PPT Claim Audit

Generated: 2026-07-05 08:18:48
PPT: `C:\Users\fujunye\Desktop\Agent\05-AIGC\06-deliverables\final-submission\CatLife_作品介绍PPT_v1.pptx`
SHA256: `603D667D706FAAE28EB02BE7E7D9A165A862E4F55835019B8B09603ADF969EE1`
Slides extracted: `23`

## Summary

- Status: `MANUAL_REVIEW_REQUIRED`
- High-risk hits: `0`
- Medium-risk hits: `1`
- Manual-review hits: `2`

## Claim Hits

| Slide | Severity | Rule | Matched pattern | Evidence excerpt | Required action |
|---:|---|---|---|---|---|
| 7 | medium | LLM should not be described as directly controlling cat transforms | `大模型驱动猫咪行为` | 定位： CatLife 是一款 心理引导式专注陪伴软件 ，它基于 AI 行为识别 感知用户状态、以 大模型驱动猫咪行为 实施 心理学引导专注策略 、 提供虚拟陪伴 。 五、产品定位： 不是计时工具，更是 AI 识别 + 心理引导 + 虚拟陪伴的专注状态迁移系统 产品通过识别用户点击、滑动、停顿与页面切换等行为变化，判断用户当前所处的注意状态；再由大模型... | Use: LLM provides safe text and high-level behavior bias; Unity local rules own movement, navigation, and animation. |
| 18 | manual_review | Forest scene wording must be concept-only | `森林` | 十五、场景设计简介与预览 场景是猫咪活动的载体， 在帮助用户舒缓心情，释放情绪方面有着不可或缺的作用 。 CatLife 中场景并非只是不变的“风景画”，它可以 承载猫咪与用户的互动 ，为人机交互体验更上一层楼提供助力。 Low-poly 设计风格 与猫咪设计风格相同的低多边形场景可以提升猫咪在场景中的协调性， 降低用户分辨场景内容的精力消耗 。多边形... | Current product rule is no forest scene. Keep any forest wording or visual as concept/history, not current runtime scope. |
| 19 | manual_review | Forest scene wording must be concept-only | `森林` | 十五、场景设计简介与预览：猫咪小镇场景预览图与森林场景资产展示 * 本页为纯图片展示页 | Current product rule is no forest scene. Keep any forest wording or visual as concept/history, not current runtime scope. |

## Scope

- This audit only checks extractable slide text inside the PPTX.
- It cannot inspect embedded bitmap text, speaker narration, or visual-only claims.
- Final closure still requires manual PPT review against the current Unity/APK evidence.
