# CatLife 行业调研与 PRD 知识准备

日期：2026-10-08。性质：知识底稿与调研协议，不是完成的行业报告 / PRD。

## 1. 产品所属领域的工作定义

根据当前功能和已确认范围，暂定为 **数字健康 / 专注管理 × 虚拟宠物陪伴 × 会后生成式 AI 建议**。这是本项目工作分类，不是已经证明的市场分类或行业规模。

核心待验证价值：减少开始专注的阻力、提供低压力陪伴、帮助理解一次会话。反假设：猫的活动、奖励和 AI 建议可能增加点按、认知负担或情绪压力。不得将本 App 内触控统计称为心理诊断，不得把实现功能等同于有效改善注意力。

## 2. 阅读顺序与产物

| 顺序 | 资料 | 阅读目标 | 转成项目产物 |
|---|---|---|---|
| 1 | Sommerville《Software Engineering》10版；SWEBOK V4 | 过程、需求、架构、测试与质量的关系 | 生命周期与最小验收链 |
| 2 | Wiegers / Beatty《Software Requirements》3版 | 业务 / 用户 / 功能需求、优先级、质量属性 | PRD骨架与可测验收 |
| 3 | Rogers 等《Interaction Design》6版 | 研究、概念模型、原型与评估 | 访谈、旅程、可用性脚本 |
| 4 | Wendel《Designing for Behavior Change》2版；Fogg《Persuasive Technology》 | 行为障碍、干预、社会反馈、自主权 | 猫咪陪伴机制假设与反指标 |
| 5 | Swink《Game Feel》 | 控制、运动、反馈与可读性 | 行走—停止—姿态与 AI 反应验收 |
| 6 | Google PAIR / Microsoft HAX | AI 的用户价值、解释、控制与失败 | 会后 AI 状态和用户感知验证 |
| 7 | Android 质量指南；NIST 生成式 AI 框架 | 设备质量、外部接口与输出风险 | 真机矩阵与适用的 AI 评估用例 |

书目、版本、官方入口、各项访问限制已核验并写入站点。没有下载或转载付费教材全文，也未声称已经完成所有专题精读。

## 3. 后续行业调研怎么做

1. 先确认目标人群、地区、比赛演示或公开上架，以及用户将发布的具体任务书。
2. 分四类选样：纯专注计时、游戏化专注、虚拟宠物、AI 陪伴。具体产品清单待正式调研时按当前可用版本确定，不先伪造优胜结论。
3. 每个样本记录：官方来源 / 版本 / 日期、目标人群、主任务、核心交互、AI 的具体作用、离线能力、数据权限、收费方式、引导压力、留存机制、实际体验与差异。
4. 评论只作问题线索，不冒充代表性样本；用户访谈保留匿名原话、观察与反例，解释和事实分栏。
5. 输出机会矩阵：问题证据 → 现有替代方式 → 未满足处 → CatLife 假设 → 最小验证；市场规模没有可靠口径时留空，不拼凑数字。

证据行模板：`编号 | 问题 | 来源URL/材料 | 日期与版本 | 可核验事实 | 我的解释 | 反例/局限 | 对PRD影响`。

## 4. PRD 草案结构（尚待输入，不视为冻结）

- 背景与问题：真实使用情境、已有替代方式、明确不解决什么。
- 用户与任务：主要 / 次要用户、年龄边界、任务旅程与价值假设。
- 目标与衡量：启动、完成、主动中断、返回理解、分心反指标；先定义口径，不杜撰目标提升率。
- 范围与版本：领养、会话、猫行为、记录成长、设置、AI、离线；森林 / 商城 / 社交等继续排除。
- 主流程与状态：Normal → Transition → Focus → Reward，恢复、中断、权限、AI失败与清除。
- 数据与接口：记录与派生统计、用户同意、聚合上传、本地动作允许列表与凭据方案。
- 体验：已确认 QA 与真实截图差异、固定镜头、动作可读性、可访问性。
- 非功能：包体、目标设备与帧率、稳定、数据完整性、隐私、许可。
- 验收与发布：需求 ID → 任务 → 用例 → 同一候选证据，用户 UAT、Go/No-Go、渠道回执。
- 待决项：目标用户、渠道、真机、参与者、AI成本、公开包架构与两周容量。

## 5. 官方来源

- [Software Engineering — Pearson](https://www.pearson.com/en-us/subject-catalog/p/learning-theories-an-educational-perspective/P200000003258)
- [Software Requirements — Microsoft Press](https://www.microsoftpressstore.com/store/software-requirements-9780735679665)
- [Interaction Design — Wiley 书目](https://uat.store.wiley.com/en-us/interaction-design-beyond-human-computer-interaction-6th-edition-p-9781119901112)
- [Designing for Behavior Change — O’Reilly](https://www.oreilly.com/library/view/designing-for-behavior/9781492056027/)
- [Persuasive Technology — Elsevier](https://shop.elsevier.com/books/persuasive-technology/fogg/978-1-55860-643-2)
- [Game Feel — Routledge](https://www.routledge.com/Game-Feel-A-Game-Designers-Guide-to-Virtual-Sensation/Swink/p/book/9780123743282)
- [SWEBOK — IEEE Computer Society](https://www.computer.org/education/bodies-of-knowledge/software-engineering/resources/)
- [People + AI — Google PAIR](https://pair.withgoogle.com/guidebook-v2/)
- [Human-AI Interaction — Microsoft HAX](https://www.microsoft.com/en-us/haxtoolkit/ai-guidelines/)
- [Core App Quality — Android Developers](https://developer.android.com/docs/quality-guidelines/core-app-quality)
- [AI RMF — NIST](https://www.nist.gov/itl/ai-risk-management-framework)

部分出版社直访有限制：Wiley 正式产品页、IEEE 主入口曾返回 403；保留官方检索与可访问目录信息，不将其描述为全文阅读。动态渠道规则、定价和产品功能在正式调研 / 提审时重新核验。
