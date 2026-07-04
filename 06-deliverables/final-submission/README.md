# CatLife Final Submission Folder

This folder is the canonical destination for final competition deliverables.

Expected files:

- `CatLife_作品介绍PPT_v1.pptx`
- `CatLife_作品演示视频_v1.mp4`
- `CatLife_作品海报_v1.png`
- `CatLife_MVP_Android_v0.1.0.apk`
- `CatLife_LLM_code_package_v1.zip`
- `CatLife_提交自检表_20260705.md`

Current status as of 2026-07-05:

- APK exists locally: `CatLife_MVP_Android_v0.1.0.apk`.
- LLM code package exists locally: `CatLife_LLM_code_package_v1.zip`.
- PPT exists locally: `CatLife_作品介绍PPT_v1.pptx`.
- Poster exists locally: `CatLife_作品海报_v1.png`.
- Video, cloud-device install/logcat evidence, and recording evidence are still missing.
- APK and ZIP are local final deliverables and are ignored by Git; the manifest and redacted evidence files are tracked.
- PPT is also a local final deliverable and ignored by Git; `CatLife_PPT_manifest.md` is tracked.
- Poster is also a local final deliverable and ignored by Git; `CatLife_poster_manifest.md` is tracked.

Official competition constraints currently tracked:

- Video: MP4 preferred; target <=3 minutes, hard maximum <=5 minutes; 1920x1080 landscape or 1080x1920 portrait.
- Poster: portrait 70cm x 150cm; jpg/jpeg/png preferred; must include work name, slogan if any, and promotional visual. The extracted PDF text says the overall size should not be lower than 2M; verify against the upload platform before final export.
- Product file: must be runnable. CatLife targets Android APK.
- Code package: all code or core code is acceptable, but the large-model API call section must be clearly marked.

Use these planning documents before filling the folder:

- `08-handoff-docs/planning/CatLife_最终提交包检查表.md`
- `08-handoff-docs/planning/CatLife_最终发布证据包与提交运行手册.md`
- `08-handoff-docs/planning/CatLife_演示视频脚本与镜头表.md`
- `08-handoff-docs/planning/CatLife_作品介绍PPT_10页精修脚本.md`
- `08-handoff-docs/planning/CatLife_海报文案与版式方案.md`
- `08-handoff-docs/planning/CatLife_用户验证访谈与问卷模板.md`
- `07-tech-specs/CatLife_Android打包与真机QA方案.md`
- `07-tech-specs/CatLife_大模型代码包与隐私降级方案.md`

The large-model code package template is prepared at:

- `06-deliverables/llm-code-package-template/`

Package it only after replacing provider-specific API parsing and confirming no secrets are present.

Run the final submission checker after adding real deliverables:

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/check-final-submission.ps1
```

The checker writes:

- `06-deliverables/final-submission/CatLife_submission_check_20260705.md`

To initialize the local evidence folder before device testing:

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/init-final-evidence.ps1
```

This creates `06-deliverables/final-submission/evidence/` with build, install, runtime, screenshot, recording, and manual review subfolders.

To collect Stage9 Android/vivo cloud-device evidence after the APK exists:

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/collect-stage9-android-evidence.ps1 `
  -ApkPath "06-deliverables/final-submission/CatLife_MVP_Android_v0.1.0.apk" `
  -CloudAdbEndpoint "<vivo cloud adb ip:port>"
```

The Stage9 collector writes only redacted credential status. The real APK must contain the local ignored `Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json` when it is exported for vivo cloud-device recording, so the cloud phone can try the real vivo API without extra setup. Generated logs, summaries, code package files, GitHub files, screenshots, PPT, poster, and video subtitles must not contain the plaintext AppKEY.

To prepare a draft large-model code package for review:

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/package-llm-code.ps1
```

By default this writes to `work/llm-code-package-output/`, not to the final submission folder.

To prepare the final local code package:

```powershell
powershell -ExecutionPolicy Bypass -File tools/final-submission/package-llm-code.ps1 -ForFinalSubmission
```

This writes `CatLife_LLM_code_package_v1.zip` and `CatLife_LLM_code_package_manifest.md` in this folder.
