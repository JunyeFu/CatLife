# CatLife Final Manual Review Notes

| Item | Reviewer | Result | Notes |
|---|---|---|---|
| PPT opens and uses real screenshots | Codex | PARTIAL | `CatLife_作品介绍PPT_v1.pptx` exists, has 23 slides, and exports to PDF; content still needs human review for current Unity/APK screenshots and current no-forest rule. |
| Video plays and meets time/resolution limits | Codex | MISSING | `CatLife_作品演示视频_v1.mp4` is not present. Run `test-final-video.ps1` after adding the final MP4. |
| Poster opens and is portrait 70cm x 150cm | Codex | PARTIAL | `CatLife_作品海报_v1.png` opens, is `4134 x 8858`, embeds about 150 DPI metadata, is over 2MB, and no longer has the black rectangle artifact; final upload preview still needs human readability review. |
| APK installs and launches | Codex | MISSING | APK artifact and hash exist, but no real device/cloud install and launch evidence has been captured. |
| Code package has no secrets | Codex | PASS | `package-llm-code.ps1 -ForFinalSubmission` generated `CatLife_LLM_code_package_v1.zip`; `check-final-submission.ps1` reports `Secret scan: PASS, hits=0`. |
| Platform upload success screenshot saved | Codex | MISSING | Platform upload is not claimed. Save the final upload success screenshot only after the official submission succeeds. |
