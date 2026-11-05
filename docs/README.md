# CatLife 文档导航

[返回产品主页](../README.md)

| 从哪里开始 | 内容 |
|---|---|
| [当前进展](STATUS.md) | 最近运行版本、真实验证范围、未完成项 |
| [开发与资产准备](DEVELOPMENT.md) | Unity 入口、依赖、源码状态、资产与配置要求 |
| [真实画面及来源](showcase/README.md) | 截图、动态图的版本与来源区间 |
| [五类追问与缺陷实测](audits/2026-11-05-interview-audit.md) | 61项Unity测试、4类新问题及2项既有缺口 |
| [追加五题：会话、UI、统计、生命周期与发布](audits/2026-11-05-interview-audit-round2.md) | 新增16项测试，5项新增问题；累计77项 |
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
