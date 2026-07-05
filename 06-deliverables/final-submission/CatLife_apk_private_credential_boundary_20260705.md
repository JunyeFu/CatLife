# CatLife APK Private Credential Boundary

Generated: 2026-07-05 11:45:50
Project root: C:\Users\fujunye\Desktop\Agent\05-AIGC

## Summary

- Ready for cloud-device real APK credential boundary: True
- Pass: 13
- Fail: 0
- APK path: C:\Users\fujunye\Desktop\Agent\05-AIGC\06-deliverables\final-submission\CatLife_MVP_Android_v0.1.0.apk
- APK SHA256: 97CA85AC82AF3A875B0D61E782B4E5C9506ABB86EE58E3B645CE6A61321A96B1
- Private AppKEY value: REDACTED

## Check Rows

| Check | Status | Evidence | Risk if missing |
|---|---|---|---|
| Private credential file exists locally | PASS | work/CatLife_Unity_Main/Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json | Real APK cannot be exported with vivo cloud credentials. |
| Private credential file is git-ignored | PASS | git check-ignore work/CatLife_Unity_Main/Assets/Resources/CatLifePrivate/vivo_cloud_credentials.json | Plaintext AppKEY could leak into Git or code package. |
| Private credential JSON parses | PASS | JSON parse without printing secret values | Build may include invalid credentials. |
| AppID matches expected vivo resource | PASS | AppID: 2026414599 | Cloud request may use the wrong competition resource. |
| AppKEY is present and not placeholder-like | PASS | AppKEY present=True; placeholder-like=False; value=REDACTED | Cloud-device APK may only run fallback instead of attempting real API. |
| Endpoint and model are usable | PASS | endpoint_https=True; model_present=True | Runtime may reject direct cloud API config. |
| Unity runtime loads private Resources config | PASS | C:\Users\fujunye\Desktop\Agent\05-AIGC\work\CatLife_Unity_Main\Assets\Scripts\LLM\VivoCloudDemoConfig.cs | APK runtime may never read the private config. |
| Unity runtime rejects public placeholder keys | PASS | C:\Users\fujunye\Desktop\Agent\05-AIGC\work\CatLife_Unity_Main\Assets\Scripts\LLM\VivoCloudDemoConfig.cs | Public example credentials may be treated as usable. |
| Unity Android build records private Resources boundary | PASS | C:\Users\fujunye\Desktop\Agent\05-AIGC\work\CatLife_Unity_Main\Assets\Editor\CatLifeAndroidBuild.cs | Build evidence may not prove Resources loadability precondition. |
| APK artifact exists | PASS | C:\Users\fujunye\Desktop\Agent\05-AIGC\06-deliverables\final-submission\CatLife_MVP_Android_v0.1.0.apk | No real/local APK is available for cloud-device recording. |
| APK hash evidence matches current APK | PASS | C:\Users\fujunye\Desktop\Agent\05-AIGC\06-deliverables\final-submission\evidence\android\00-build\apk-sha256.txt | Evidence may describe a different APK than the one uploaded. |
| APK decompressed entries contain private AppKEY bytes | PASS | entry_count=1; entries=assets/bin/Data/e398723ad9f5dda47b8bb4c57db5a4a8 | Real APK may not contain the private cloud-device key. |
| APK decompressed entries contain AppID bytes | PASS | entry_count=2; entries=assets/bin/Data/Managed/Metadata/global-metadata.dat, assets/bin/Data/e398723ad9f5dda47b8bb4c57db5a4a8 | Real APK may not contain the expected vivo AppID. |

## Boundary Rule

- The real/local APK is expected to be exported with the ignored private Unity Resources config so the vivo cloud device can try the real API without extra setup.
- Public GitHub files, code package files, logs, screenshots, PPT, poster, and video subtitles must not contain the plaintext AppKEY.
- This report proves the local build preconditions and redacted evidence chain; final Stage9 still requires cloud/local install, startup, LLM/fallback, focus-flow, and recording evidence.
