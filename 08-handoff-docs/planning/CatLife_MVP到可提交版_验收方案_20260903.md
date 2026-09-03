# CatLife 从 MVP 到可提交版：验收方案

日期：2026-09-03
状态：`ACTIVE`
控制依据：[开发控制总则](../../DEVELOPMENT_CONTROL.md)
阶段依据：[阶段开发方案](CatLife_MVP到可提交版_阶段开发方案_20260903.md)

## 1. 验收原则

- 每个硬门只验收同一构建身份产生的代码、场景、APK和证据。
- 静态文件、自动测试、Editor、模拟器、真实 AI、真机和提交材料分级记录，互不替代。
- 功能、视觉、隐私、包体任一必需项失败，该硬门即 `FAIL`。
- 不设置 MD5、SHA 或其他哈希校验门槛。
- AI 生图、旧截图、历史日志和 mock 数据不能作为当前 APK 通过证据。
- 验收报告必须写明：阶段、构建版本、Git 提交身份、Unity 版本、设备/模拟器、执行时间、用例结果和证据路径。

## 2. 统一证据目录

每个阶段使用以下结构，目录名中的 `<stage>` 使用 `G1`～`G7`：

```text
work/CatLife_Unity_Main/Reports/SubmissionRebuild/<stage>/
  acceptance.md
  editmode-results.xml
  playmode-results.xml
  editor/
  simulator/
  device/<device-name>/
  logs/
  build/
```

截图、录屏、APK和日志保持本地，不默认提交 Git；`acceptance.md` 只记录路径、构建身份与结论，不记录凭据。

## 3. 通用测试入口

Unity 项目：`D:\Agent\AIGC innovation\work\CatLife_Unity_Main`
Unity：`D:\UnityEngine\6000.4.9f1\Editor\Unity.exe`

EditMode：

```powershell
& 'D:\UnityEngine\6000.4.9f1\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'D:\Agent\AIGC innovation\work\CatLife_Unity_Main' `
  -runTests -testPlatform EditMode `
  -testResults 'D:\Agent\AIGC innovation\work\CatLife_Unity_Main\Reports\SubmissionRebuild\current\editmode-results.xml' `
  -logFile 'D:\Agent\AIGC innovation\work\CatLife_Unity_Main\Reports\SubmissionRebuild\current\editmode.log'
```

PlayMode：将上式 `EditMode` 改为 `PlayMode`，并写入独立结果和日志文件。

移动场景/资产检查：

```powershell
& 'D:\UnityEngine\6000.4.9f1\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'D:\Agent\AIGC innovation\work\CatLife_Unity_Main' `
  -executeMethod CatLife.Editor.CatLifeMobileBuildValidator.ValidateBatch `
  -logFile 'D:\Agent\AIGC innovation\work\CatLife_Unity_Main\Reports\SubmissionRebuild\current\validator.log'
```

最终集成阶段可以运行 `tools/art-pipeline/run_unity_hard_gates.ps1`；它会重建场景和应用导入策略，因此不得在只读验收或存在未确认素材改动时直接运行。

## 4. G1 运行架构验收

| 编号 | 用例 | 通过标准 | 最低证据 |
|---|---|---|---|
| G1-01 | 唯一会话真源 | 只有 `CatLifeSessionController` 产生阶段、截止时间和完成结果 | E0 + E1 |
| G1-02 | Recognition 接线 | UI/生命周期事件能进入聚合器并产生快照 | E1 + E2 |
| G1-03 | Cat Behavior 接线 | 猫行为能读取阶段和快照，但不能改计时/奖励 | E1 + E2 |
| G1-04 | UI 单向呈现 | UI 从状态渲染，页面切换不复制业务状态 | E1 + E2 |
| G1-05 | 重启兼容 | 旧 MVP 数据可读取，活动会话可恢复 | E1 |

G1 阻断项：第二套状态机、重复计时、组件空引用、旧系统自行切页或直接写奖励。

## 5. G2 猫咪动作与移动验收

| 编号 | 用例 | 通过标准 | 最低证据 |
|---|---|---|---|
| G2-01 | 动作清单一致 | FBX clips、manifest、Animator 和动作 ID 一一对应 | E0 + E2 |
| G2-02 | 普通态移动 | 猫能从出生点寻路到至少 3 个不同兴趣点 | E2 + E3 |
| G2-03 | 移动循环 | 5 分钟内出现“移动—到点—动作—继续移动” | E3 录屏 |
| G2-04 | 四状态差异 | Normal 高活跃；Transition 靠近/注视；Focus 安静休息；Reward 起身庆祝 | E3 录屏 |
| G2-05 | 动画质量 | 无滑步、明显跳变、穿地、漂浮和世界位移漂移 | E2 + E3 |
| G2-06 | 导航安全 | 不穿建筑、不离岛；路径失败能回到安全点 | E2 + E3 |
| G2-07 | 用户互动 | 点击猫只触发允许的轻反馈，Focus 中不形成高干扰循环 | E3 |

G2 必须由真实运行录屏验收，Animator 中有状态名称不算通过。

## 6. G3 行为识别与自动专注验收

| 编号 | 用例 | 通过标准 | 最低证据 |
|---|---|---|---|
| G3-01 | 事件采集 | 点击、滑动、停顿、页面、猫互动、前后台各能形成事件 | E1 + E2 |
| G3-02 | 状态谱 | 三项分值随输入变化，并输出注意偏离/转移中/稳定及趋势 | E1 + E3 |
| G3-03 | 自动进入 | 达到适应时长和阈值后出现可取消过渡，再开始 Focus | E3 录屏 |
| G3-04 | 主动进入 | 开始按钮仍可独立进入 Setup/Transition/Focus | E3 |
| G3-05 | 不重复触发 | 已在 Transition/Focus 时不会再次自动开始 | E1 + E3 |
| G3-06 | 隐私边界 | 记录和日志中没有原始文本、触点、包名和屏幕内容 | E0 + E3 |
| G3-07 | 评审层真实性 | 评审层数值来自本次实时事件，不是预置演示值 | E2 + E3 |

模拟器脚本必须覆盖高频操作、逐渐减少操作和稳定停留三段，并保存对应录屏与 logcat。

## 7. G4 前端视觉验收

统一画幅：模拟器实际 `1080×2424`；Canvas 基准 `1080×2400` 并应用 Safe Area。

| 编号 | 页面 | 必须可见 | 通过标准 |
|---|---|---|---|
| G4-01 | Home | 猫、小镇、今日状态、气泡、开始、成长/记录/设置 | 主体和层次匹配 QA-01 |
| G4-02 | Setup | 小镇背景、底部准备卡、时长/模式/AI | 不遮猫头，匹配 QA-02 |
| G4-03 | Focus | 倒计时、安静猫、上滑退出 | 无多余按钮，匹配 QA-03 |
| G4-04 | Reward | 时长、稳定度、成长、爪印、猫反馈 | 匹配 QA-04 |
| G4-05 | Records | 今日、最长稳定、真实七日图、会话列表 | 匹配 QA-05 |
| G4-06 | Growth/Cat | 心情、动作解释、成长、解锁内容 | 匹配 QA-05/08/11 |
| G4-07 | Settings | 默认时长、提醒、识别、AI、隐私、清除数据 | 匹配 QA-06 |
| G4-08 | 通用组件 | 暖色圆角系统、图标、字体、48dp 点击区 | 匹配 QA-09 |

每页同时提交确认稿和真实模拟器截图。文字正确但仍是临时矩形、布局遮挡、背景消失、猫咪漂浮或空岛错误均为 `FAIL`。最终需要用户逐页确认。

## 8. G5 MiMo 可感知闭环验收

| 编号 | 用例 | 通过标准 | 最低证据 |
|---|---|---|---|
| G5-01 | 首次同意 | 明确说明只发送聚合值；拒绝后保持本地功能 | E3 |
| G5-02 | 请求中 | 完成会话后显示低打扰等待状态 | E3 录屏 |
| G5-03 | 真实成功 | 一次真实 `mimo_cloud` 请求成功，UI 显示来源 | E4 |
| G5-04 | 结构化结果 | 同时显示温和提醒、任务回归建议和猫咪反应 | E3 + E4 |
| G5-05 | 动作执行 | `recommendedLocalAction` 经允许列表映射并播放已解锁动作 | E3 + E4 |
| G5-06 | 单次请求 | 同一会话请求数为 1，无自动重试 | E1 + E4 |
| G5-07 | 失败路径 | 断网时显示本地洞察和真实来源，不影响奖励 | E3 |
| G5-08 | 凭据 | 日志、截图、代码和提交材料不出现真实 API key | E0 + E4 |

G5 的录屏必须连续包含：完成会话、请求中、结果出现、猫咪执行动作。单独截图成功文字不足以通过。

## 9. G6 其余产品闭环验收

| 编号 | 用例 | 通过标准 | 最低证据 |
|---|---|---|---|
| G6-01 | 首次使用 | 领取、命名、基础设置完成并持久化 | E3 |
| G6-02 | 再次启动 | 不重复强制 onboarding，猫名和设置保留 | E3 |
| G6-03 | 猫咪状态 | 心情、动作原因、成长、爪印、解锁一致 | E1 + E3 |
| G6-04 | 五地标 | 钟楼、猫屋、鱼店、奖励树、广场均有真实入口或轻互动 | E3 |
| G6-05 | 轻锁定 | 隐藏入口、画面降刺激、背景音量降低、上滑确认 | E3 |
| G6-06 | 完成与中断 | 完成发奖励；中断记录但奖励为 0 | E1 + E3 |
| G6-07 | 后台恢复 | 目标时间继续；后台次数、稳定段和结果正确 | E1 + E3 |
| G6-08 | 进程恢复 | 杀进程重启后恢复活动会话或最终结果 | E3 |
| G6-09 | 清除数据 | 二次确认后只清除 CatLife 本地数据 | E3 |

G6 通过后冻结功能，不再加入未列入范围的新页面。

## 10. G7 构建、真机与材料验收

### 10.1 构建

- Release ARM64 APK/AAB；版本高于 `0.3.0 (3)`。
- 构建场景只有正式 `CatLifeMobile`。
- APK/AAB 不超过 120 MiB。
- 依赖树不含 `.blend`、原始 GLB、原始大图和未引用贴图。
- 飞行模式下可完成启动、主动专注、完成/中断、记录、成长、设置和重启读档。

### 10.2 模拟器

安装前清理 `com.catlife.mvp` 数据；按真实 SurfaceView 边界换算坐标。必须完成：首页、onboarding、主动专注、自动专注、后台恢复、进程恢复、滑动中断、自然完成、MiMo 成功、离线降级、记录、成长、设置、地标互动和 10 分钟稳定运行。

### 10.3 真机

最低设备：Android 10+、4GB RAM。若沿用 QA-13，第二台 Android 设备也必须完成验收。

每台记录：设备型号、Android 版本、RAM、安装结果、冷启动、10 分钟运行、平均/最低可观察帧率、崩溃、ANR、资源缺失、温度/热降频观察、后台恢复、进程恢复和截图/录屏。

任何一台要求内设备未执行时，G7 为 `BLOCKED`，不得写成真机通过。

### 10.4 比赛材料

建立“功能声明—APK 操作路径—自动测试—截图/录屏—材料页码”映射。PPT、海报、视频、代码包和说明必须使用同一 RC：

- 森林只标概念资产，不宣称场景切换；
- 后台能力只表述实际采集的离开时长、返回间隔和次数；
- MiMo 表述为聚合特征上的会后心理建议与猫动作偏置；
- 商业模式明确为未来规划，不写成已实现功能；
- 没有运行证据的能力从材料中删除或降级为规划。

## 11. 验收报告模板

```text
阶段：Gx
构建版本：
Git 提交身份：
Unity 版本：
设备/模拟器：
执行时间：

用例总数：
PASS：
FAIL：
BLOCKED：

阻断项：
证据目录：
结论：PASS / FAIL / BLOCKED
下一阶段入口：
```

## 12. 当前验收状态

| 硬门 | 状态 | 原因 |
|---|---|---|
| G1 | `PASS` | 唯一运行系统根已接入；EditMode 10/10、PlayMode 7/7 |
| G2 | `IN_PROGRESS` | NavMesh、自主漫游和 Walk 已恢复；IdleBreath、禁走区、连续巡游及模拟器录屏待完成 |
| G3 | `NOT_STARTED` | 行为识别未装配 |
| G4 | `NOT_STARTED` | 当前 UI 是功能骨架 |
| G5 | `NOT_STARTED` | MiMo 已连通但未形成可感知动作闭环 |
| G6 | `NOT_STARTED` | onboarding、地标和完整轻锁定等仍缺 |
| G7 | `NOT_STARTED` | 仅有 MVP 模拟器证据，提交 RC 尚不存在 |

下一次验收只允许执行 G1；G1 未通过时，不签署 G2。
