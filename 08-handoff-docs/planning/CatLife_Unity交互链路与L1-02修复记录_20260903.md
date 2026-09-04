# CatLife Unity 交互链路与 L1-02 修复记录

日期：2026-09-04
状态：`G2_PASS`

## 1. Unity 交互方式

工程安装了 `com.coplaydev.unity-mcp`。实时方式是 Unity Editor Bridge 连接本地 MCP Server；当前 Codex 会话未注册该 MCP 服务，且项目规则不允许擅自修改全局配置，因此本轮采用 Unity 6 项目本地接口：`-executeMethod` 生成场景/构建 APK，`-runTests` 执行 EditMode/PlayMode，并通过 ADB 驱动 Android 模拟器。两条链路操作的是同一工程，不改变运行代码接口。

## 2. 已修复内容

- 从最终猫 FBX 建立派生运行母版，输出单骨架、单 SkinnedMesh、17 个有效动作的 `CL_CAT_Runtime.fbx`；Neutral 占位动作不进入运行映射。
- 修复 Walk 根骨位移折叠；新增坐下、坐姿循环、趴下、专注呼吸、专注注意和起身动作。Focus 动作不再复用坐姿，腿链与躯干形成可见低位趴姿。
- Animator 保持单层状态，通过直接 CrossFade 由唯一行为驱动器控制；Normal 漫游与 Transition/Focus/Reward 主链不并发写 Animator。
- 正式场景建立四块可走区、四个稳定兴趣点、建筑语义禁走区、岛边约束和安全恢复点；普通态兴趣点限制在固定摄像机可见范围。
- 修复 NavMesh 路径起点使用视觉高度导致寻路失败的问题，路径计算前投影到 NavMesh。
- 猫咪增加 3D Collider、指针映射和 uGUI `CatUiInteractionBridge`。透明背景不再拦截世界交互，`SafeArea` 的 raycast 覆盖被真实序列化；Android 注入点击可触发一次轻反馈及气泡。
- 构建器在构建前清除同路径旧 APK，避免残留输出影响产物判断；包体以 APK 文件长度为准，不误用 Unity 的展开构建量。

## 3. 验证结果

- Blender 动作边界审计：17 个动作，每个起/中/末 3 个采样，共 51 个采样完成。
- Unity EditMode：10/10 通过。
- Unity PlayMode：14/14 通过。
- 正式场景：229,286 三角面、15 个材质、167 个清单资产。
- Android Release：`0.3.0 (3)`、ARM64、IL2CPP、APK 52,109,664 字节（49.70 MiB），签名 v2 验证通过。
- Android 模拟器：Pixel 9 API 35，1080×2424，约 2GB RAM；5 分钟 03 秒连续录屏，PID 始终为 4419，目标崩溃/ANR/空引用/资源缺失计数为 0。
- 真实 ADB 点击：`[CatLifeCatInteraction] tap` 精确出现 1 次，屏幕显示“喵，我在这里，陪你慢慢来。”。
- 1 分钟会话：进入 Focus 后猫为低位趴姿；自然完成进入 Reward，显示稳定度 100%、成长 +1、爪印 +1。

## 4. 证据

- `Reports/Simulator/L1-02/final9-cat-tap.png`
- `Reports/Simulator/L1-02/final9-focus.png`
- `Reports/Simulator/L1-02/final9-reward.png`
- `Reports/Simulator/L1-02/final9-roaming-part1.mp4`（02:59）
- `Reports/Simulator/L1-02/final9-roaming-part2.mp4`（02:04）
- `work/CatLife_Unity_Main/Logs/L1-02-final9-editmode.xml`
- `work/CatLife_Unity_Main/Logs/L1-02-final9-playmode.xml`
- `work/CatLife_Unity_Main/Logs/L1-02-final9-android-release.log`
- `work/CatLife_Unity_Main/Logs/L1-02-cat-action-bounds-final.log`

## 5. 结论与边界

G2 通过，下一开发入口为 L1-03/G3 行为识别与自动专注。Reward 卡片会遮挡起身动作、气泡尚未逐帧跟随头部，这两项归入 L1-04 视觉层收口。当前证据来自约 2GB Android 模拟器；4GB Android 真机 G7 仍按用户要求暂缓，不能声明真机通过。
