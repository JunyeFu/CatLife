# CatLife 文档导航

[返回产品主页](../README.md)

| 从哪里开始 | 内容 |
|---|---|
| [当前进展](STATUS.md) | 最近运行版本、真实验证范围、未完成项 |
| [开发与资产准备](DEVELOPMENT.md) | Unity 入口、依赖、源码状态、资产与配置要求 |
| [真实画面及来源](showcase/README.md) | 截图、动态图的版本与来源区间 |
| [双端设计自查](audits/2026-11-06-ui-design-self-review.md) | 软件/GitHub十条设计规范的实际缺口与平台边界 |
| [视觉修复与增量开发控制](development/2026-11-06-ui-style-repair-plan.md) | 已确认三项取舍、五包顺序、文件白名单及G0–G4验收 |
| [WP06.5基础交付](development/2026-11-06-ui-style-foundation.md) | 模拟器保留数据恢复、独立分支、参数与460字库、真实画面/风格参考边界 |
| [WP06.6静态UI/G1](development/2026-11-06-ui-static-results.md) | 去描边、中性色、四暗底字重、焦点/排版及实际双画幅检查 |
| [五类追问与缺陷实测](audits/2026-11-05-interview-audit.md) | 61项Unity测试、4类新问题及2项既有缺口 |
| [追加五题：会话、UI、统计、生命周期与发布](audits/2026-11-05-interview-audit-round2.md) | 新增16项测试，5项新增问题；累计77项 |
| [逐项修复方案与执行顺序](audits/2026-11-05-repair-plan.md) | IA-01～11修改范围、步骤、验收及五批排期 |
| [修复执行与实际发现](audits/2026-11-05-repair-results.md) | 用例、空目录还原、候选配置与模拟器验收；保留失败与未通过项 |
| [技术规格](../07-tech-specs) | 产品流程、UI、状态机和 Android 方案 |
| [开发记录](../08-handoff-docs/planning) | 阶段问题、修复与验证报告 |
| [资产规范](../02-asset-specs) | 低多边形资产需求与规范 |
| [历史比赛交付](../06-deliverables) | 比赛阶段材料、概念设计与旧交接包 |

## 仓库约定

- 根 README 是产品展示入口，`docs/` 是当前文档入口；历史资料保留原目录，以免破坏现有引用。
- 当前 Unity 工程位于 `work/CatLife_Unity_Main/`。旧比赛材料中的计划不自动等于当前功能。
- `.blend`、FBX、原始 GLB、大视频、APK、Unity 缓存与私有配置不作为主页资源提交。
- 展示资源放在 `docs/showcase/`，说明真实运行版本；设计预览与运行截图分别标记。
- 文档更新日期与产品验证日期分开记录。局部测试通过不扩展为完整发行验收。
