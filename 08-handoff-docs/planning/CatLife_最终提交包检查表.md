# CatLife 最终提交包检查表

日期：2026-06-29
目标：提交前用同一张表核对 PPT、视频、海报、APK、代码包，避免漏文件或版本不一致。

## 1. 最终目录

所有最终提交物统一放入：

```text
06-deliverables/final-submission/
```

建议文件名：

```text
CatLife_作品介绍PPT_v1.pptx
CatLife_作品演示视频_v1.mp4
CatLife_作品海报_v1.png
CatLife_MVP_Android_v0.1.0.apk
CatLife_大模型调用代码包_v1.zip
CatLife_提交自检表_20260705.md
```

## 2. 五项提交物检查

| 项 | 文件 | 必须检查 | 状态 |
|---|---|---|---|
| PPT | `CatLife_作品介绍PPT_v1.pptx` | 可打开、无缺图、逻辑包含痛点/方案/技术/完成度/模型应用 | 待填 |
| 视频 | `CatLife_作品演示视频_v1.mp4` | MP4；尽量 <=3 分钟，最长 <=5 分钟；横版 1920x1080 或竖版 1080x1920；展示最终形态、名称、交互 UI 与功能 | 待填 |
| 海报 | `CatLife_作品海报_v1.png` | 竖版 70cm x 150cm；jpg/jpeg/png；作品名、slogan、宣传图齐备；文件大小按官方 PDF“整体大小不低于 2M”和平台最终限制复核 | 待填 |
| APK | `CatLife_MVP_Android_v0.1.0.apk` | 可安装、可启动、可完成主流程 | 待填 |
| 代码包 | `CatLife_大模型调用代码包_v1.zip` | 无密钥、README 清楚、示例可读；重点标注大模型 API 调用部分 | 待填 |

## 3. PPT 内容门禁

| 页/模块 | 要点 | 证据来源 |
|---|---|---|
| 标题 | CatLife、团队名、赛道 | README |
| 痛点 | 大学生数字分心、进入专注困难 | 讲稿/调研 |
| 方案 | 行为识别 + 猫咪陪伴 + 轻锁定 | 技术方案 |
| 产品流程 | 普通、过渡、专注、奖励 | 状态机文档 |
| 3D 资产 | 猫咪动画、小镇场景 | 交付物索引 |
| 技术架构 | 本地规则 + 可选 LLM | 大模型代码包方案 |
| 隐私安全 | 不录屏、不读输入内容、不跨 App 监控 | 非敏感行为方案 |
| 完成度 | APK、录屏、截图、QA 结果 | final-submission |
| 评审对齐 | 创新性、应用价值、完成度、大模型应用 | 评审检查表 |

## 4. APK 自检

| 检查 | 通过条件 |
|---|---|
| 文件存在 | APK 在 `final-submission` |
| 私有 key 打包 | 真实版/最终提交 APK 构建前确认本机 ignored `Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json` 存在且会进入 APK |
| 安装 | `adb install -r` 返回 Success |
| 启动 | 5 秒内可见首屏或主场景 |
| 主场景 | 猫 + 小镇可见 |
| 状态链 | 普通 -> 过渡 -> 专注 -> 奖励 |
| LLM 路径 | logcat 能判断 vivo 云端真实调用、BlueLM 端侧调用、失败码或本地 fallback 状态之一 |
| 日志 | 无 crash、ANR、关键 NullReference |
| 录屏 | 至少 1 段真机流程视频 |

## 5. 代码包自检

提交前运行文本搜索。公开代码包、提交材料、日志、字幕和说明文档不得包含明文 AppKEY；真实版/最终提交 APK 可以包含本机 ignored 私有 Resources 中的云真机可用 key，但该 key 不进入 GitHub 或代码包。

```powershell
Select-String -Path "06-deliverables/final-submission/**/*" -Pattern "sk-|api_key|API_KEY|secret|password|token" -CaseSensitive:$false
```

如果命中，必须人工确认不是密钥或从包里移除。`private_config_presence_redacted.txt` 只能记录私有配置“存在/可用/脱敏 AppID”，不能记录完整 AppKEY。

代码包必须包含：

- README；
- 模型调用说明；
- API 调用位置标注；
- 聚合特征示例；
- Prompt；
- 本地降级逻辑；
- 无网络/无 Key 行为说明。

## 6. 视频自检

| 项 | 标准 |
|---|---|
| 第一屏 | 5 秒内看到 CatLife 和猫咪/小镇 |
| 产品闭环 | 真实展示状态变化 |
| 技术解释 | 有行为识别和大模型边界 |
| 时长 | 不超过复赛要求 |
| 清晰度 | 字幕可读，录屏不卡顿 |
| 隐私 | 无通知、账号、密钥、聊天内容 |

## 7. 提交前最终动作

1. 复制最终文件到 `06-deliverables/final-submission/`。
2. 逐个双击打开。
3. APK 真机安装并录屏。
4. 搜索密钥和隐私内容。
5. 更新本检查表状态。
6. 压缩或上传前记录 SHA256。
7. 上传后保存平台成功截图。

## 8. 最终状态记录模板

| 项 | 值 |
|---|---|
| 检查日期 | 待填 |
| 检查人 | 待填 |
| Unity 版本 | 待填 |
| Android 设备 | 待填 |
| APK 文件名 | 待填 |
| APK SHA256 | 待填 |
| 视频文件名 | 待填 |
| 代码包文件名 | 待填 |
| 已知限制 | 待填 |
| 是否可提交 | 待填 |
