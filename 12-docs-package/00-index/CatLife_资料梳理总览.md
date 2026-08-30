# CatLife 资料梳理总览

整理日期：2026-07-01
项目目录：`D:\Agent\AIGC innovation`
资料包目录：`D:\Agent\AIGC innovation\12-docs-package`

## 1. 资料范围

本轮整理覆盖三类文件：

| 来源 | 处理 |
|---|---|
| `05-AIGC` 项目内文档 | 已按主题复制进资料包 |
| 桌面相关目录 | 补入 `C:\Users\fujunye\Desktop\实训开发\AIGC大赛初赛PPT最终版.pptx` |
| D 盘微信文件 | 补入 CatLife 项目计划、省赛上推作品公示，并将 5 份复赛说明 PDF 归档到 `01-competition-docs/` |

完整文件索引见 `00-index/manifest.csv`。索引包含来源路径、复制后路径、大小、修改时间和 SHA256，可用于后续查重和追溯。

未纳入的内容：镇海楼音乐 AIGC 项目、课程论文“降 AIGC”文档、蓝牙项目说明、复赛录屏 zip、重复 FBX。原因是它们与 CatLife 复赛文档交接无直接关系，或已经有更明确的工程文件留在 `03-3d-models`。

## 2. 建议阅读顺序

1. `02-core-project/README.md`：项目定位、技术栈、复赛提交物。
2. `03-planning-handoff/CatLife_当前项目内容与开发思路总览_20260701.md`：当前内容、产品闭环、资产状态、开发思路和下一步 P0。
3. `03-planning-handoff/CatLife_人类团队完整开发流程调研_20260701.md`：按真实人类团队工作流，把产品、资产、Blender、Unity、Android、LLM、QA、视频/PPT/海报和最终上传拆成完整阶段。
4. `03-planning-handoff/CatLife_复赛冲刺计划.md`：2026-06-29 当前进度、W5/W6 收口计划和 5 项提交物状态；作为历史冲刺计划参考。
5. `03-planning-handoff/CatLife_复赛官方材料校准记录_20260629.md`：根据 5 份复赛 PDF 校准 5 项提交物、评分口径、质量门禁和 vivo 工具。
6. `05-tech-llm-unity-android/CatLife_MVP_非Unity准备工作总方案.md`：从当前资产到 APK/视频的准备工作总纲。
7. `05-tech-llm-unity-android/CatLife_Android打包与真机QA方案.md`：Android 构建、安装、logcat、录屏、云真机证据。
8. `03-planning-handoff/CatLife_演示视频脚本与镜头表.md`：按官方视频规格拆分镜头和旁白。
9. `03-planning-handoff/CatLife_最终提交包检查表.md`：PPT、视频、海报、APK、代码包提交前逐项核对。
10. `03-planning-handoff/CatLife_从最新本地场景从零重建App流程.md`：明确后续从最新本地小镇/猫动画资产重建，不继承旧 Unity 场景。
11. `03-planning-handoff/CatLife_GitHub协作者上传内容定位与摘要_20260629.md`：从 GitHub 提交记录定位协作者上传内容，并对应到本地摘要文件。
12. `03-planning-handoff/CatLife_作品介绍PPT_10页精修脚本.md`：10 页 PPT 逐页文案、截图占位和评审对齐。
13. `03-planning-handoff/CatLife_海报文案与版式方案.md`：竖版海报主视觉、文案和导出检查。
14. `03-planning-handoff/CatLife_用户验证访谈与问卷模板.md`：至少 5 份用户验证的访谈和问卷模板。
15. `03-planning-handoff/CatLife_当前缺口审计_20260629.md`：当前状态到 APK/视频完成之间的证据缺口。
16. `03-planning-handoff/CatLife_Android真机测试记录模板.md`：APK 安装、启动、状态链、性能、logcat 记录模板。
17. `03-planning-handoff/CatLife_录屏剪辑执行清单.md`：演示视频录制、分段素材和剪辑时间线。
18. `03-planning-handoff/CatLife_最终发布证据包与提交运行手册.md`：最终提交目录、证据结构、ADB 命令和上传前验收。
19. `05-tech-llm-unity-android/CatLife_UI完整设计与交互规格_20260629.md`：完整 App UI 信息架构、页面、组件、状态和视觉风格。
20. `05-tech-llm-unity-android/CatLife_用例库指标流程表_20260629.md`：用例库、指标、主流程、异常流程和比赛指标。
21. `05-tech-llm-unity-android/CatLife_Unity实现技术与脚本概念_20260629.md`：Unity 技术选型、状态机、UI、猫动画和 LLM 降级脚本概念。
22. `03-planning-handoff/CatLife_UI顶格比赛提交装配说明_20260629.md`：带预览图、图片素材、Unity 脚本和一键安装命令的 UI 装配入口。
23. `03-planning-handoff/CatLife_主页目标稿拆解与摄像机控制_20260629.md`：主页 scene/UI 分层、猫咪活动范围和固定高度摄像机控制。
24. `03-planning-handoff/CatLife_猫咪聊天气泡模块实现说明_20260630.md`：猫咪状态气泡对象树、文案规则、脚本接线和验收。
25. `03-planning-handoff/CatLife_用户场景流程与用例覆盖验证_20260629.md`：用户需求、使用场景、操作响应模拟和覆盖缺口。
26. `05-tech-llm-unity-android/CatLife_大模型代码包与隐私降级方案.md`：大模型 API 调用标注、隐私过滤和本地降级。
27. `05-tech-llm-unity-android/CatLife_移动端3D性能预算与优化方案.md`：移动端面数、材质、贴图、光照、粒子和验证预算。
28. `05-tech-llm-unity-android/CatLife_猫咪小镇场景Unity落地方案.md`：小镇源文件、导入策略、黑线毛刺和 Prefab 结构。
29. `03-planning-handoff/CatLife_MVP_从当前状态到APK与演示视频执行计划.md`：P0/P1 执行清单、APK 和演示视频路线。
30. `03-planning-handoff/CatLife_复赛评审对照检查表.md`：对齐创新性、应用价值、完成度、大模型应用。
31. `02-core-project/CatLife_DEV-GUIDE_复赛开发规约.md`：开发纪律、质量门禁、风险红线。
32. `04-role-plans/分工_*.md`：逐人交付边界。
33. `05-tech-llm-unity-android/CatLife_技术规格_MVP状态机与行为识别.md`：四状态机和行为识别方案。
34. `06-art-assets-blender` 与 `10-blender-mcp-audit`：美术资产、Blender 双轨工程状态。
35. `00-index/CatLife_当前交付物位置索引_20260629.md`：6/29 文件整理位置索引，仅作为历史追溯，不再作为当前入口。
36. `07-deliverables-pitch`：PPT、路演、答辩、心理学依据。

## 3. 项目从头到尾脉络

CatLife 的主线是“用陪伴式设计降低专注开始成本”。它不是单纯番茄钟、锁机 App 或宠物 App，而是通过行为识别判断用户从普通使用到专注的状态迁移，再用猫咪状态、轻锁定界面和奖励反馈形成闭环。

复赛材料把项目拆成五个交付物：PPT、演示视频、海报、可运行 APK、大模型调用代码包。评分口径为创新性 40%、应用价值 30%、完成度 20%、大模型应用 10%。因此现阶段最关键的不是单个文档是否齐全，而是工程闭环是否可以真实演示，尤其是 APK 可运行、LLM 调用可证明、实际产品截图可进入 PPT 和视频。

时间线已从原先 6 月 23 日截止重校准到 2026 年 7 月 6 日 8:00。6 月 23 日应视为内部初版里程碑，后续一周用于打磨、真机测试、视频精修和最终上传。

## 4. 当前资料状态

| 模块 | 状态 | 证据 |
|---|---|---|
| 官方规则 | 已集中 | `01-official-rules` |
| 项目定位 | 已集中 | `02-core-project/README.md` |
| 冲刺计划 | 已集中 | `03-planning-handoff` |
| 角色分工 | 已集中 | `04-role-plans` |
| 技术方案 | 初版明确 | `05-tech-llm-unity-android` |
| 美术资产 | 文档和图集已集中 | `06-art-assets-blender` |
| Blender 工程 | 已完成猫动画、小镇场景和本地资产归档整理 | `10-blender-mcp-audit`、`03-3d-models` |
| Unity/Android 前置计划 | 已补齐非 Unity 准备工作、移动端性能预算、小镇落地、APK/视频执行计划、Android 真机 QA、最终提交表，以及按人类团队真实工作流拆解的完整开发流程调研 | `07-tech-specs`、`08-handoff-docs/planning` |
| UI/用例/场景设计 | 已补齐完整 UI 规格、用例库、指标流程、Unity 概念脚本、用户场景覆盖验证，以及带预览图/图片素材/一键安装脚本的 UI 装配包；主页已明确为猫咪小镇 scene layer + UI overlay，摄像机固定高度旋转/前后移动，并加入猫咪状态聊天气泡 | `06-deliverables/catlife-ui-assembly-kit-20260629/`、`05-tech-llm-unity-android/CatLife_UI完整设计与交互规格_20260629.md`、`03-planning-handoff/CatLife_主页目标稿拆解与摄像机控制_20260629.md`、`03-planning-handoff/CatLife_猫咪聊天气泡模块实现说明_20260630.md` |
| APK 前视觉预览 | 已生成演示预览页、镜头映射和代表图，用于 PPT/视频/海报素材预排；仍需用真机画面替换 | `06-deliverables/demo-preview-20260629/` |
| 协作者 Unity 参考 | 已从 GitHub 提交记录定位原始上传，并从本地 Unity work batch 同步脚本、场景、设置和资源；只用于阅读脚本、场景结构和设置，不作为新开发基线 | `06-deliverables/unity-handoff-20260629/GITHUB_UPLOAD_SUMMARY.md`、`06-deliverables/unity-collaborator-reference-20260629/FILE_SUMMARY.md`、`work/unity-android-build-batch-20260629/` |
| 官方材料校准 | 已根据 `应用赛道复赛交流会-0528.pdf`、`创新创意经验分享.pdf`、`02-开发规约与质量门禁.pdf`、`03-评审对照检查表.pdf`、`04-官方信息完全整合.pdf` 校准提交物规格、评分权重、视频/海报要求、代码包 API 标注和 vivo 工具 | `03-planning-handoff/CatLife_复赛官方材料校准记录_20260629.md` |
| 路演/答辩 | 已有初赛 PPT、讲稿和心理学支撑 | `07-deliverables-pitch` |
| 外部补充 | 已筛选补入 | `09-external-found` |

## 5. Blender 与模型工程状态

截至 2026-07-01，Blender/模型工程分为猫咪动画和猫咪小镇两条线：

| 项 | 当前结论 |
|---|---|
| 猫咪动画源文件 | 当前使用 `03-3d-models/blender-work/CatLife_cat_animation_coordinate_corrected.blend` |
| 猫咪动画交付包 | 已输出 `06-deliverables/cat-animation-final-package-20260629/` |
| 动画数量 | 10 个动作；作为 Unity Animator 动作来源 |
| 动画坐标标准 | Blender 源保持 `+Z` up，头朝 `-Y`，尾朝 `+Y` |
| 小镇当前场景 | 以 no-merge / no-skybox 思路为准；正式天空、灯光和运行时相机在 Unity 做 |
| 小镇风格证据 | `03-3d-models/catlife-town/reports/style-pass/` |
| 小镇废弃方案 | `mesh_clean` 合并实验已归档，不能作为生产场景使用 |
| 原始猫模型 | 已移动到 `03-3d-models/source-cat-models/original-meshy-quadruped/` |
| 旧动画中间文件 | 已归档到 `03-3d-models/blender-work/archive/20260629-animation-iterations/` |

注意：Blender 侧当前重点已经从“继续制作单个资源”转为“把可用资源稳定交给 Unity/Android”。真正的下一步风险在 Unity/Android 侧，必须做导入、材质、帧率、内存和包体实测。

## 6. 分工与交付边界

| 成员 | 主责 | 当前资料中的关键产物 |
|---|---|---|
| 吴若琪 | 技术统筹、大模型方案、代码包 | `分工_吴若琪_技术统筹.md` |
| 傅钧烨 | Blender 建模、资产、最终上传 | `分工_傅钧烨_Blender建模.md`、Blender 审计文档 |
| 陈泓森 | Unity 脚本、状态机、场景联调 | `分工_陈泓森_Unity脚本.md` |
| 严辰乐 | Android 集成、APK、真机测试 | `分工_严辰乐_Android集成.md` |
| 傅钧漪 | PPT、海报、视频、答辩物料 | `分工_傅钧漪_宣传物料.md` |

## 7. P0-P3 问题清单

### P0 必须先解决

| 问题 | 判定标准 |
|---|---|
| APK 是否可安装启动 | 真机安装、冷启动、无崩溃 |
| Unity 是否能导入 Runtime FBX | 场景可见、材质不丢、比例正确 |
| LLM 是否真实调用 | 有可演示 API/SDK 调用、代码包标注清楚 |
| 核心闭环是否可录屏 | 开屏、领养/主场景、专注、退出、记录 |

### P1 直接影响复赛分数

| 问题 | 判定标准 |
|---|---|
| 移动端性能 | Unity >25fps、内存 <300MB、启动 <3s |
| 实际产品截图 | PPT 第 8 页和视频中使用真实效果，不用纯设计稿 |
| 视频素材 | 3 分钟竖版视频能完整展示作品最终形态 |
| 海报素材 | 使用 Render 分支输出高质量主视觉 |

### P2 提升表达可信度

| 问题 | 判定标准 |
|---|---|
| 心理学支撑 | SDT、正念注意力回归、轻约束设计和产品机制逐条对应 |
| 用户验证 | 至少 5 份访谈或问卷摘要进入 PPT |
| 差异化表达 | 清楚说明不是番茄钟、不是强锁机、不是普通宠物 |

### P3 打磨项

| 问题 | 判定标准 |
|---|---|
| 命名规范 | 提交物名称统一、版本号清晰 |
| 引用和错别字 | PPT、讲稿、海报无明显硬伤 |
| 资料归档 | 交接包、代码包、素材包可追溯 |

## 8. 下一步建议

1. 用 `CatLife_runtime.fbx` 做 Unity 导入测试，记录材质、比例、帧率和内存。
2. 建立 Android 真机测试表，优先确认空 APK、Unity 场景 APK、完整闭环 APK 三阶段。
3. 把 LLM 调用最小 Demo 固化成代码包，至少包含 `LLMClient.cs`、Prompt、超时降级和 README。
4. 用 Render 分支补输出 3 类图：全景、猫/入口局部、海报竖版主图。
5. 用本资料包的 `manifest.csv` 做最终材料核对，避免提交前漏文件或版本混乱。
