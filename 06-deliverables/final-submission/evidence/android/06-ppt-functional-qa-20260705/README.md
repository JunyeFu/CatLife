# CatLife PPT 功能完整性自主测试记录

测试时间：2026-07-05  
测试设备：Android 官方 Emulator `emulator-5556`，1080x2424，density 420  
测试包：`work/CatLife_Unity_Main/Reports/BuildSize/20260705-launcher-fix-mcp-release/CatLife_LauncherFix_MCP_Release.apk`  
包名：`com.catlife.mvp`  
启动 Activity：`com.unity3d.player.UnityPlayerGameActivity`

## 测试依据

- `06-deliverables/final-submission/CatLife_PPT_extracted_text_20260705.md`
- `08-handoff-docs/planning/CatLife_PPT功能缺陷对照表_20260705.md`
- 当前 Unity 工程源码与 Android APK 运行结果

本轮只测试当前规则内的 MVP：不测试森林场景，不把森林概念图作为当前 APK 缺陷。

## 证据文件

| 文件 | 用途 |
|---|---|
| `01_launch_0p8s.png` | 冷启动 0.8 秒截图 |
| `02_launch_4s.png` | 冷启动 4 秒截图 |
| `03_auto_focus_39s.png` | 进入页面后自动进入专注状态截图 |
| `04_after_unlock.png` | 上滑解锁后回到普通状态截图 |
| `05_cat_page.png` | 猫咪页面截图 |
| `06_record_page.png` | 记录页面截图 |
| `07_settings_page.png` | 设置页面截图 |
| `08_settings_after_input_attempt.png` | 设置页数字输入尝试后截图 |
| `09_manual_focus_after_start.png` | 手动点击开始专注后截图 |
| `logcat_launch_auto_focus.txt` | 冷启动与自动进入专注日志 |
| `logcat_after_unlock.txt` | 解锁后日志 |
| `logcat_manual_focus.txt` | 手动专注与 LLM fallback 日志 |
| `window_after_auto_focus.txt` | 自动专注后窗口焦点证据 |

## 总体结论

当前 APK 已能支撑 PPT 中“主场景页、猫咪陪伴、主动专注、自动进入专注、轻锁定、上滑退出、记录/猫咪/设置页面、LLM 失败降级”的演示链路。

仍不应对外强表述为“已完成真实 Android 后台行为识别”“已完成蓝心端侧 SDK 真实运行”“已完成真实 vivo 云端响应”，因为本轮 emulator 只证明了云端配置可用、请求会发起、失败后能降级到本地模板。

## 功能点对照表

| PPT 功能点 | 测试结论 | 证据 | 问题 / 说明 |
|---|---|---|---|
| APK 可安装并启动 | PASS | `cmd package resolve-activity` 返回 `com.catlife.mvp/com.unity3d.player.UnityPlayerGameActivity`；前序 launcher 修复后已启动成功 | 已修复此前无 Launcher Activity 的问题 |
| 开屏页展示 CatLife 品牌图 | FAIL | `01_launch_0p8s.png` 为黑屏，`02_launch_4s.png` 为 Unity 默认 splash | 未看到用户给定的白底猫咪 Cat Life 开屏图；PPT“开屏页”演示口径仍有风险 |
| 主场景页 / 小镇场景 | PASS | `04_after_unlock.png` | 主楼、猫咪、按钮、开始专注按钮均可见 |
| 右侧猫咪 / 记录 / 设置页面 | PASS | `05_cat_page.png`、`06_record_page.png`、`07_settings_page.png` | 页面能打开，内容为当前运行数据 |
| 真实数据状态栏 | PASS | `03_auto_focus_39s.png`、`04_after_unlock.png` | 顶部今日专注、专注中计时随状态显示 |
| 主动点击开始专注 | PASS | `09_manual_focus_after_start.png`；`logcat_manual_focus.txt` 中 `focus_start source=ui_or_auto` | 进入专注后右侧按钮隐藏，显示轻锁定滑槽 |
| 自动进入专注 | PARTIAL | `03_auto_focus_39s.png` | 能自动进入专注，但当前更像进入页面后的定时触发；PPT 说法是“操作频率持续下降后自动识别”，真实频率识别证据不足 |
| 轻锁定与上滑退出 | PASS | `03_auto_focus_39s.png`、`04_after_unlock.png` | 专注态显示上滑解锁，滑动后恢复普通按钮和开始专注按钮 |
| 专注状态按钮隐藏 | PASS | `03_auto_focus_39s.png`、`09_manual_focus_after_start.png` | 猫咪、记录、旋转、设置按钮在专注态隐藏 |
| 猫咪在场景中陪伴 / 行走 | PASS | `04_after_unlock.png`、`09_manual_focus_after_start.png` | 猫咪在场景内可见，普通态和专注态位置/姿态有变化 |
| 猫咪 10 动作完整联动 | PARTIAL | 代码中存在 `CatAnimationController` 和行为状态映射；截图只覆盖行走/站立/静态陪伴 | 本轮未逐个触发 10 个动作，仍需动画状态专项测试 |
| 设置页每轮专注 / 自动进入专注配置 | PARTIAL | `07_settings_page.png` 显示当前设置；源码有 `InputField` 和 `onEndEdit` | ADB 输入尝试后 `08_settings_after_input_attempt.png` 仍为 25 分钟 / 10 秒，未证明 APK 内可顺畅修改 |
| 记录页专注统计与反馈 | PASS | `06_record_page.png` | 能显示今日概览、最近 7 天、游戏化反馈、猫咪反馈 |
| 完成专注后奖励闭环 | PARTIAL | 记录页已有奖励字段 | 本轮未跑满 25 分钟，也因设置输入未验证，未完成短时专注完成态测试 |
| AI 行为识别机制 | PARTIAL | 源码存在 `Recognition`、`RealtimeFeatureEngine`、`AndroidBehaviorEventBridge` | 运行证据主要是 App 内事件和定时自动专注；没有真实后台切屏/跨应用节奏采集证据 |
| 大模型心理引导 | PARTIAL | `logcat_manual_focus.txt` 多次出现 `llm_request llm_source=vivo_cloud_pending app_id=20****99 cloud_config_usable=True` | 官方 emulator 当前网络无法解析 vivo 云端，实际结果为 `local_template` fallback |
| LLM 隐私边界 / 降级 | PASS | `logcat_manual_focus.txt` 出现 masked app id 和 fallback；本目录敏感词扫描未发现明文 AppKEY | 日志中未暴露明文 key；fallback 逻辑可运行 |
| 首次使用猫咪领取 / 命名 / 基础设置 | FAIL | 冷启动后直接进入 Unity splash 和主场景流程 | 当前 APK 没有 onboarding、领养、命名流程 |
| 背景音量降低 | NOT TESTED | 无音频系统专项证据 | PPT 相关说法需要音频组件或手动录屏验证 |
| 真机/云真机可录制演示 | PARTIAL | 官方 emulator 可运行并截图 | vivo 云真机真实网络、录屏、安装日志仍需补充 |

## 本轮新增缺陷

| 优先级 | 缺陷 | 影响 | 建议 |
|---|---|---|---|
| P0 | 冷启动未显示用户给定 CatLife 开屏图，只显示 Unity 默认 splash | PPT 已明确“开屏页已给出”，演示时会直接露出不一致 | 配置 Android/Unity Splash 背景与 Logo，或在首场景做真正的 CatLife 开屏 Canvas，并关闭/替换默认 Unity splash |
| P0 | 自动进入专注更像定时器，不是行为频率下降识别 | PPT 技术创新点依赖“行为识别进入专注” | 将自动进入判断改为 RealtimeFeatureEngine 的低交互/低滑动窗口评分，保留定时器只作 fallback |
| P1 | 设置页输入在 ADB 测试中未能修改 25 分钟 / 10 秒 | 用户无法稳定自定义每轮专注和自动进入时间会影响体验 | 增大 InputField 命中区，增加 `+/-` 按钮或独立设置弹窗，并记录保存成功 toast |
| P1 | vivo 云端请求在官方 emulator 上 DNS 失败 | PPT/演示只能证明 fallback，不能证明真实云端响应 | 在云真机或有外网 DNS 的设备上复测，保留成功响应 logcat；失败时 PPT 口径保持“可降级演示” |
| P1 | 10 动作与完成奖励未逐项验收 | 猫咪行为系统完整性证据不足 | 增加 Debug 测试入口或自动化状态轮播，输出每个动作截图/日志 |
| P2 | 首次领取/命名流程缺失 | PPT 用户流程中提到首次使用初始化 | 若时间有限，PPT 改为“后续版本”；若保留承诺则新增轻量 onboarding |

## 下一轮建议测试

1. 修复开屏图后，重复冷启动 0.5s / 2s / 4s 截图。
2. 用 Debug 短专注时长跑完成态：开始专注 -> 完成 -> 记录页奖励更新。
3. 建立 10 动作状态轮播测试，输出动作名、Animator state、截图。
4. 在云真机上重测 vivo 云端请求，确认是否为 emulator DNS 问题。
5. 对设置页做真触控复测；如果用户手动也不好输入，优先改为步进器。

