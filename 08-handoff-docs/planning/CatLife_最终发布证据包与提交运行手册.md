# CatLife 最终发布证据包与提交运行手册

日期：2026-06-29
范围：不进入 Unity 编辑器的前提下，把从 APK 构建完成到录制演示视频、生成最终提交包之间的证据结构、命名、检查命令和交接责任固定下来。

## 1. 目标

最终提交不是只把 5 个文件放进目录。对 CatLife 来说，必须同时留下可追溯证据，证明：

- APK 是从当前 Unity 工程构建出来的；
- APK 至少在一台 Android 设备或 vivo 云真机上可安装、可启动、可跑主流程；
- 演示视频来自真实运行画面，不是纯设计稿；
- 大模型调用代码包没有密钥，且能看出 API/SDK 调用位置；
- 真实版/最终提交 APK 必须内置本机私密 vivo 云端 Demo 凭据，即包含云真机可用 key，保证上传云真机后可直接尝试真实 API；最终代码包、GitHub、日志、PPT、海报和视频字幕不能包含真实 AppKEY；
- PPT、海报、视频使用同一版截图和同一版产品口径。

## 2. 官方口径

| 来源 | 对本项目的落地要求 |
|---|---|
| 复赛交流会 PDF | 必交 PPT、演示视频+海报、作品文件、代码包；视频尽量 <=3 分钟、最长 <=5 分钟；作品文件必须可运行 |
| Android Developers App Signing | 最终 APK 需要明确 debug/release 签名口径；参赛提交不是上架 Google Play，但仍要记录签名类型和版本 |
| Android Developers ADB / screenrecord | `adb screenrecord` 默认适合采集最长 3 分钟的 MP4 真机画面，不包含音频，旁白应后期配音 |
| Unity Android build documentation | 需要记录 Unity 版本、Android Build Settings、Scripting Backend、Target Architecture、Package Name、Version |

## 3. 目录结构

最终目录：

```text
06-deliverables/final-submission/
```

建议结构：

```text
final-submission/
  CatLife_作品介绍PPT_v1.pptx
  CatLife_PPT_claim_audit_20260705.md
  CatLife_PPT_extracted_text_20260705.md
  CatLife_作品演示视频_v1.mp4
  CatLife_作品海报_v1.png
  CatLife_MVP_Android_v0.1.0.apk
  CatLife_LLM_code_package_v1.zip
  CatLife_submission_check_20260705.md
  evidence/
    00-build/
      android-build.log
      unity-build-settings.txt
      apk-sha256.txt
    01-install/
      android-install.txt
      device-info.txt
    02-runtime/
      android-runtime-logcat.txt
      vivo-cloud-llm-logcat.txt
      smoke-test-notes.md
    03-screenshots/
      launch.png
      town-main.png
      focus-state.png
      reward-state.png
    04-recordings/
      raw-device-recording.mp4
      shot_01_town_overview.mp4
      shot_02_cat_idle.mp4
    05-review/
      manual-review-notes.md
      upload-success-screenshot.png
```

说明：平台最终上传通常只需要 5 项材料；`evidence/` 是团队自证和答辩追溯资料，可以不全部上传，但必须本地保留。

## 4. 一键初始化

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/init-final-evidence.ps1
```

脚本会创建 `evidence/` 子目录和空模板文件，不会覆盖已有证据。

云真机录制前先生成交接文档：

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/prepare-cloud-device-handoff.ps1
```

该脚本输出 `06-deliverables/final-submission/CatLife_cloud_device_recording_handoff_20260705.md`，记录当前 APK 文件、SHA256、本机 ADB 状态、云真机 ADB 采证命令、网页下载文件清单和导入命令。它只记录私有凭据的存在/边界，不输出明文 AppKEY。真实版/最终提交 APK 仍必须包含本机 ignored 私有 Resources 中的云真机可用 key，方便上传云真机后直接演示真实 API 或记录 fallback。

上传 APK 前再运行私有凭据边界检查：

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/test-apk-credential-boundary.ps1
```

该脚本输出 `06-deliverables/final-submission/CatLife_apk_private_credential_boundary_20260705.md`，检查本机私有配置存在、`.gitignore` 保护、AppKEY 非占位、Unity 运行时 Resources 读取路径、构建脚本私有凭据证据链和 APK hash 是否匹配；报告只写 `REDACTED`，不输出明文 AppKEY。

如需给云真机上传前准备一个本机工作区，运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/prepare-cloud-device-upload-workspace.ps1
```

该脚本写入 `work/final-submission-cloud-upload/`，包括上传说明、APK 源路径、预期下载文件清单和小型脱敏证据副本。它默认不复制 2.8GB APK，避免重复占用磁盘；应从 manifest 中记录的 final-submission APK 路径上传。可提交的 manifest 位于 `06-deliverables/final-submission/CatLife_cloud_device_upload_workspace_manifest_20260705.md`。

## 5. 构建后必须记录

| 文件 | 内容 |
|---|---|
| `evidence/00-build/android-build.log` | Unity batch build 或手动构建日志 |
| `evidence/00-build/unity-build-settings.txt` | Unity 版本、Scenes In Build、包名、版本、IL2CPP/Mono、ARM64、压缩格式 |
| `evidence/00-build/apk-sha256.txt` | APK 文件名、大小、SHA256 |
| `evidence/01-install/android-install.txt` | `adb install -r` 完整输出 |
| `evidence/01-install/device-info.txt` | 设备型号、Android 版本、ABI、屏幕分辨率、测试方式 |
| `evidence/02-runtime/android-runtime-logcat.txt` | 启动后导出的 logcat |
| `evidence/02-runtime/vivo-cloud-llm-logcat.txt` | 过滤 `CatLife`、`vivo`、`LLM` 等关键词后，证明真实云端调用或 fallback 的日志 |
| `evidence/02-runtime/smoke-test-notes.md` | 手动记录普通、过渡、专注、奖励状态是否通过 |

## 6. 推荐命令

设备识别：

```powershell
adb devices
adb shell getprop ro.product.model
adb shell getprop ro.build.version.release
adb shell getprop ro.product.cpu.abi
```

安装和启动：

```powershell
adb install -r "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk" *> "06-deliverables/final-submission/evidence/01-install/android-install.txt"
adb logcat -c
adb shell monkey -p com.catlife.mvp 1
adb logcat -d > "06-deliverables/final-submission/evidence/02-runtime/android-runtime-logcat.txt"
adb logcat -d | Select-String "CatLife|vivo|LLM|fallback" > "06-deliverables/final-submission/evidence/02-runtime/vivo-cloud-llm-logcat.txt"
```

截图：

```powershell
adb exec-out screencap -p > "06-deliverables/final-submission/evidence/03-screenshots/town-main.png"
```

录屏：

```powershell
adb shell screenrecord --bit-rate 8000000 --time-limit 180 /sdcard/catlife-demo.mp4
adb pull /sdcard/catlife-demo.mp4 "06-deliverables/final-submission/evidence/04-recordings/raw-device-recording.mp4"
```

哈希：

```powershell
Get-FileHash -Algorithm SHA256 "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk"
```

视频自动检查：

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/test-final-video.ps1 `
  -VideoPath "06-deliverables/final-submission/CatLife_作品演示视频_v1.mp4"
```

该脚本会生成 `06-deliverables/final-submission/CatLife_video_manifest.md`。有 `ffprobe` 时自动检查时长、分辨率、编码信息；无 `ffprobe` 时必须人工补充或安装后复验，不能只凭 MP4 文件存在标记视频通过。

如果 vivo 云真机网页端不能提供 ADB endpoint，但能下载安装日志、logcat、截图或录屏，则使用人工导入脚本统一落到标准证据目录：

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/import-cloud-device-evidence.ps1 `
  -SourceDir "<云真机下载文件目录>" `
  -InstallLog "install.log" `
  -StartupLogcat "logcat_startup.txt" `
  -LlmLogcat "logcat_vivo_cloud_llm.txt" `
  -FocusLogcat "logcat_5min_focus.txt" `
  -FocusRecording "focus_5min_screenrecord.mp4" `
  -LaunchScreenshot "launch.png" `
  -TownScreenshot "town-main.png"
```

该脚本会对文本证据做 AppKEY、Authorization、Bearer、token 脱敏，复制到 `evidence/android/`、`evidence/03-screenshots/` 和 `evidence/04-recordings/`，并生成 `evidence/android/05-summary/manual_cloud_device_import.md`。导入完成后仍必须运行最终自检脚本。

如果云真机 ADB endpoint 需要等待分配或页面连接较慢，使用等待采证脚本：

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/wait-and-collect-stage9-android-evidence.ps1 `
  -ApkPath "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk" `
  -CloudAdbEndpoint "<云真机IP:端口>" `
  -TimeoutSeconds 900
```

该脚本会轮询 `adb devices`，检测到设备后调用 `collect-stage9-android-evidence.ps1`。如果超时仍无设备，只写 `evidence/android/05-summary/stage9_wait_for_device_status.md`，不生成虚假的安装、logcat 或录屏通过证据。

如果已经拿到云真机下载文件和最终演示视频，用最终证据导入脚本一次性导入并重跑闸门：

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/test-final-evidence-inputs.ps1 `
  -SourceDir "<云真机下载文件目录>" `
  -FinalVideo "<最终演示视频 mp4>"

powershell -ExecutionPolicy Bypass -File tools/final-submission/import-final-submission-evidence.ps1 `
  -SourceDir "<云真机下载文件目录>" `
  -FinalVideo "<最终演示视频 mp4>"
```

预检脚本输出 `CatLife_final_evidence_input_check_20260705.md`，只检查输入文件是否存在和关键日志信号，不复制文件。导入脚本会把视频复制为 `CatLife_作品演示视频_v1.mp4`，调用 `import-cloud-device-evidence.ps1` 做文本脱敏和证据导入，运行 `test-final-video.ps1`，再运行 `run-final-submission-gates.ps1`。输出 `CatLife_final_evidence_import_summary_20260705.md`。如果证据不完整，它必须保持 incomplete。

## 7. 提交前顺序

1. 初始化 `evidence/` 目录。
2. 放入 APK、PPT、视频、海报、代码包。
3. 记录构建设置和 APK SHA256。
4. 真机或云真机安装 APK。
5. 录制至少一段完整状态链：普通 -> 过渡 -> 专注 -> 奖励。
6. 从录屏中截取 PPT/海报需要的真实画面。
7. 生成最终演示视频并检查时长、分辨率、隐私。
8. 确认真实版/最终提交 APK 已包含本机私密 vivo 云端配置和云真机可用 key，且 GitHub、代码包、日志、截图、PPT、海报和视频字幕不包含真实 AppKEY。
9. 生成云真机录制交接文档，确认采证清单和 APK hash 与待上传 APK 一致。
10. 生成云真机上传工作区，确认 APK 源路径、hash、脱敏证据和预期下载文件名。
11. 运行 PPT 文本口径审计，解决高风险命中；当前 PPT claim audit 与 patch report 已能自动关闭森林范围和大模型行为口径项。
12. 运行公开材料密钥扫描，确认公开文本、报告、工具、规划文档、技术规格和代码包模板没有明文 AppKEY、Bearer token 或等价凭据。
13. 打包大模型代码包并确认无密钥。
14. 运行最终检查脚本，上传平台后保存成功截图。

## 8. 验收定义

只有同时满足以下条件，才能把 CatLife 标记为“可提交”：

- `check-final-submission.ps1` 自动检查通过；
- `audit-final-requirements.ps1` 没有 `MISSING` 或 `PARTIAL` 行；当前 PPT/安全相关 `MANUAL_REVIEW` 已由可复跑审计报告自动关闭；
- `audit-ppt-claims.ps1` 已生成 PPT 文本审计，且没有高风险、中风险或人工复核命中；本地最终 PPT 已通过 `patch-ppt-claims.ps1` 把大模型表述降级为行为偏置，并把森林相关文字改为历史概念，不作为当前产品范围；
- 人工打开 PPT、视频、海报、APK、代码包全部无明显错误；
- 视频必须有 `CatLife_video_manifest.md`，并确认时长、分辨率、首屏、隐私和功能演示符合要求；
- `evidence/` 中有构建、安装、设备、运行日志和录屏证据；
- 视频第一屏 5 秒内能看到 CatLife 与猫咪/小镇；
- PPT 和视频的界面截图来自同一版 APK 或同一版 Unity 运行画面；
- 代码包没有真实密钥，README 标明大模型调用位置和降级行为；
- `vivo-cloud-llm-logcat.txt` 能说明真实调用成功，或说明失败原因和本地 fallback 已接管；
- 已记录上传成功截图或平台提交确认。

最终审计命令：

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/run-final-submission-gates.ps1
powershell -ExecutionPolicy Bypass -File tools/final-submission/audit-ppt-claims.ps1 -AllowHits
powershell -ExecutionPolicy Bypass -File tools/final-submission/scan-final-secrets.ps1
powershell -ExecutionPolicy Bypass -File tools/final-submission/audit-final-requirements.ps1
```

`run-final-submission-gates.ps1` 是最终总闸门入口，会按顺序生成 handoff、上传工作区、视频 manifest、PPT 口径审计、公开密钥扫描、提交检查和最终要求审计，并输出 `CatLife_final_submission_master_gate_20260705.md`。只要视频、云真机安装/logcat/录屏证据缺失，它必须保持 incomplete，不能作为阶段 10 完成证明。

## 9. 当前缺口

截至 2026-07-05，当前项目已有 Unity 原型、vivo 云端 Demo 配置路径、本机私密 APK 打包配置和本机真实版 APK：

- APK：`06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk`
- SHA256：`97CA85AC82AF3A875B0D61E782B4E5C9506ABB86EE58E3B645CE6A61321A96B1`
- 私有 key 边界：真实版 APK 必须包含本机 ignored 私有 Resources 中的云真机可用 key；GitHub、代码包、日志、截图、PPT、海报、录屏字幕和公开文档不得包含明文 AppKEY。
- PPT 文本审计：`06-deliverables/final-submission/CatLife_PPT_claim_audit_20260705.md` 当前 PASS，高风险 0、中风险 0、人工复核 0；修复记录见 `CatLife_PPT_claim_patch_20260705.md`。
- 公开材料密钥扫描：`06-deliverables/final-submission/CatLife_public_secret_scan_20260705.md` 当前 PASS，命中 0；报告不回显匹配行内容。
- 云真机交接文档：`06-deliverables/final-submission/CatLife_cloud_device_recording_handoff_20260705.md` 已生成，记录 APK hash、ADB 发现结果、云真机 ADB/网页下载两条采证路径和导入命令；该文档不包含明文 AppKEY。
- 云真机上传工作区 manifest：`06-deliverables/final-submission/CatLife_cloud_device_upload_workspace_manifest_20260705.md` 已生成，记录本机 ignored 上传工作区、APK 源路径、hash 和预期下载证据文件；默认不复制 APK，避免重复占用磁盘。
- 最终要求审计：`06-deliverables/final-submission/CatLife_final_requirements_audit_20260705.md` 当前 PASS 10、PARTIAL 1、MISSING 5、MANUAL_REVIEW 0；剩余全是视频和云真机/设备运行证据。

当前仍缺视频、vivo 云真机安装证据、启动/LLM logcat 和录屏证据。PPT 已复制到 `06-deliverables/final-submission/CatLife_作品介绍PPT_v1.pptx`，海报已生成到 `06-deliverables/final-submission/CatLife_作品海报_v1.png`，代码包已生成到 `06-deliverables/final-submission/CatLife_LLM_code_package_v1.zip`，并分别记录 SHA256。下一步必须优先填充本手册定义的 `evidence/` 目录，不能仅凭 APK、PPT、海报和代码包文件存在标记为最终可提交。
