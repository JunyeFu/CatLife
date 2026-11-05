# CatLife：追加五类面试追问与实测（第6–10题）

日期：2026-11-05。延续[第一轮](2026-11-05-interview-audit.md)，只增加未覆盖的测试点。**本轮16个独立用例：9通过、7失败，归并5项新增问题。两轮合计77个独立用例，63通过、14失败。** 其中本轮为11项Unity PlayMode及5项APK静态检查，不混称全部为Unity或Android真机测试。

## 选题依据

仍是公开面经主题的项目化追问，不声称存在统计排名：

- [AI应用开发实习原帖](https://www.nowcoder.com/feed/main/detail/40920533fad14136bff01c3928c7e953)包含上下文、后端适配、工具权限、记忆和IO等工程追问；据此补充会话隔离与请求生命周期。
- 指标口径参考[数据开发面试主题](https://www.nowcoder.com/discuss/809573962119176192)搜索摘要；原帖正文读取受限，只用其支持“指标口径是追问主题”，不据此认定CatLife业务口径。
- 行为判据以[Unity协程生命周期](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.StopCoroutine.html)、[uGUI富文本](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/StyledText.html)、[Android所有文件访问](https://developer.android.com/training/data-storage/manage-all-files)和[Android备份说明](https://developer.android.com/identity/data/autobackup)为依据。

## 6. 不同会话的异步结果如何隔离？并发请求会不会串页或相互覆盖？

**执行2项，2通过。**

1. 旧记录正在等待AI时启动下一次Focus，再注入旧记录的成功回调。旧结果只进入旧记录；当前Focus页面、截止时间和记录数量不变。
2. 同一个本地客户端并发提交两次请求。第二次只收到一次`busy`错误，第一请求仍完成，`IsBusy`恢复false，来源为`local_template`。

该范围未发现新的串会话问题。测试注入的是本地回调，不证明真实云服务高并发或多用户隔离；当前产品也没有多账号服务端。

## 7. 用户昵称和模型文本如何进入UI？富文本与Emoji边界安全吗？

**执行3项，1通过、2失败。**

- **通过：**序列化结果页的实际`rewardInsight`控件关闭富文本，模型文本按普通文字呈现。
- **IA-07 / P2：昵称富文本未隔离。** 通过真实命名输入框输入`<b>橘</b>`，调用领取和成长页；可见的成长Text开启`supportRichText`，且未经转义拼入该昵称。昵称被当成格式标签处理。这是本地UI呈现问题，**不是浏览器XSS、远程执行或他人账号攻击**。
- **IA-08 / P2：截断产生不完整UTF-16。** 向建议字段输入23个中文字符和一个猫Emoji，24码元截断后只剩Emoji的高代理项，产生不合法的Unicode标量序列。当前测试证明截断结果损坏，未声称所有Android字体都出现同一种乱码。

根因：`CatLifeMobileApp.AdoptCat/ShowGrowth`把原始昵称放入格式文本；`LLMBehaviorSuggestion.SanitizeLine`直接按`string.Length/Substring`截断。

修复方向：用户昵称采用普通文本独立控件或明确的字面呈现方案，保留系统自己的等级颜色；截断至少不切开代理对，再按实际需求决定是否支持组合字符簇，不必引入新的UI框架。

## 8. 今日、七日、成长和解锁的指标口径是否一致？短会话会不会漏计？

**执行4项，1通过、3失败。**

| 同日两条中断记录 | 总秒数 | 汇总后取整应显示 | 当前今日统计 |
|---|---:|---:|---:|
| 59秒 + 59秒 | 118 | 1分钟 | 0分钟 |
| 90秒 + 90秒 | 180 | 3分钟 | 2分钟 |
| 119秒 + 119秒 | 238 | 3分钟 | 2分钟 |

**IA-09 / P2：累计时长过早取整。** `CatLifeStats.Derive`在每条记录上计算`actualSeconds / 60`后累加，丢掉每条记录的不足一分钟部分；今日、七日总数和对应每日桶使用同一个有损值。三个用例都在TodayMinutes断言失败，周桶影响由同一`minutes`变量的代码路径确认，未把未执行到的后续断言写成额外失败测试。

本轮验收假设明确为“统计已记录的总时长，再转成整分钟”；中断不奖励不等于抹掉已记录时长。如果产品有意按每条整分钟统计，应明确显示口径，而不能把当前值称为所有记录的总分钟。

控制用例通过：跨午夜会话按既有契约归到结束日期，中断不发成长/爪印，也不解锁动作。未以另一个统计假设推翻已有日期契约。

修复方向：先累计秒，再分别转换今日和每日桶；同时明确周总数与各天显示舍入的关系。

## 9. 请求执行期间对象被停用，恢复后会不会留下永久busy？

**执行2项，1通过、1失败。**

- 控制通过：仅设置`MonoBehaviour.enabled=false`不停止其协程，本地请求能完成并释放busy。
- **IA-10 / P2：宿主停用后复用锁死。** 对真实`MockCatLLMClient`发起延迟本地请求，停用其GameObject，等待超过延迟再重新启用。下一请求返回`MockCatLLMClient busy.`，旧协程已停止，busy不能自行释放。

`IsBusy=false`只位于协程正常返回路径，缺少宿主停止/取消后的状态释放。这个反例覆盖的是**GameObject停用并复用**，不是已证明按Home键、Android进后台就会触发；当前普通页面切换也没有被证明停用客户端宿主，因此按生命周期潜在缺口处理。

修复方向：明确客户端请求所有权，在宿主停用时结束/取消其工作并恢复可复用状态，同时约定是否通知调用方。不为此新增请求队列或重试框架。

## 10. Release候选的权限、身份和备份策略能解释清楚吗？

**使用Android SDK的aapt读取真实r34 APK，5项检查4通过、1失败。**

通过：包明确为`com.catlife.review.ui / 0.3.0 / code 3`；非debuggable；没有相机、麦克风、联系人、短信权限；有INTERNET且Manifest没有明确开启明文HTTP。后者是配置检查，不等同于已经抓包验证所有连接。

**IA-11 / P1（公开发行前）：旧端侧模型权限进入当前候选。** 实际APK仍声明：

```text
android.permission.MANAGE_EXTERNAL_STORAGE
android.permission.READ_EXTERNAL_STORAGE (maxSdkVersion=32)
```

源头已定位到`Assets/Plugins/Android/AndroidManifest.xml`，同文件还保留旧`com.catlife.bluelm.DEFAULT_MODEL_PATH`的`/sdcard/1225/1.7.0.4_1225_mtk9500`。`CatLifeRuntimeAssemblyValidator.ValidateAndroidBlueLmPlugin`甚至要求Manifest包含该权限和路径，说明仅删Manifest条目会与旧验证契约冲突。

当前本地专注与会后云端建议没有证明需要所有文件访问。**声明权限不等于用户已授权，本轮未执行授权、读取用户文件或证明数据外传。** r34仍是私有候选；这里指出公开发行前应清理的权限与旧插件契约。

Manifest未明确声明`allowBackup/fullBackupContent/dataExtractionRules`。依据Android默认行为，备份应另行决定包含哪些记录；未执行系统备份或证明本机已发生上传，不把“未显式配置”直接判为泄露。

修复方向：按当前运行方案移除不需要的端侧权限及模型路径，同步旧验证器的适用范围，再检查新包合并Manifest；不直接在公开包中保留测试/旧SDK权限。

## 本轮缺陷表

| ID | 优先级 | 结果 | 下一步 |
|---|---|---|---|
| IA-07 昵称富文本 | P2 | 命名→成长页真实控件复现 | 隔离用户文本与系统格式 |
| IA-08 Emoji截断 | P2 | 一条边界反例失败 | 保持合法Unicode边界 |
| IA-09 累计时间漏计 | P2 | 三组时长反例失败 | 汇总秒数后按口径转分钟 |
| IA-10 宿主复用busy | P2，潜在生命周期缺口 | 停用复用失败；仅禁用组件控制通过 | 完成取消/释放契约 |
| IA-11 旧存储权限 | P1，发行前 | 实际APK和源Manifest均存在 | 同步清理权限、旧路径与验证器 |

第一轮IA-01–06继续保留，未修改或标为修复。发布前优先收口IA-01/02/05/11，再按产品影响处理P2项。

## 证据与复现

- Unity 6000.4.9f1，隐藏batch运行，`Reports/agent-runs/interview-round2-20261105/tests.xml`：11项，5过6失败。
- `Reports/agent-runs/interview-audit-20261105-round2/package-audit.json`：5项静态检查，4过1失败；同目录保存合并Manifest与汇总。
- [Unity审计夹具](../../tools/audits/CatLifeInterviewAuditRoundTwoTests.cs)：复制到Unity工程`Assets/Tests/PlayMode/`，以PlayMode过滤`CatLifeInterviewAuditRoundTwoTests`运行；结束后移出Assets。本轮已移出，避免正常回归自动包含红例。
- [APK检查脚本](../../tools/audits/test_catlife_release_package.py)：传入`--apk`、`--aapt`及`--output`复现，结果失败时进程退出码为1。

本轮只新增测试与文档；业务逻辑未修，未新构建APK、调用真实模型、启动真机测试或更改系统时钟。使用本地模板客户端，并保存/恢复原PlayerPrefs。累计77项包含两轮独立用例，不把重复读取结果算新测试；累计为63通过、14失败，9类已复现/静态确认的问题及2项既有证据/交付缺口。
