# Builder Assistant 0.25.2：Android 云端刷新运行库修复

2026-11-09。用户确认故障入口是建筑助手蓝图工具的云端刷新。日志明确报告 `HttpClientHandler.set_AllowAutoRedirect(bool)` 缺失，调用 `BlueprintCloudTransport.Get → BlueprintCloudClient.Read`，Mod MVID `837e68cb452b4eb8ac865bb80b94fcc0`、mscorlib MVID `9d0f885b92554f54bb3ffdcb8916ab03`。这不是内置云天书典的网络类。

## 来源与范围

任务开始时本地 main `95333d0` 的建筑助手为0.24.0，没有云端模块。只取回远端 main 的建筑助手云端提交 `a572d05` 与 `3b6b08e`，分别作为本地 `7ff5c5f`、`7bd6ce1`，解决两份共用文档的追加记录冲突，保留本地荒野探险家0.81.0与全部家居/多人/书典内容。没有合入远端其它项目变更，没有修改SCIE-Port文件、开发规范或远端配置。

远端0.25.1仍直接引用三个已裁剪的HTTP setter和 `MediaTypeHeaderValue.MediaType` getter。源码重建旧DLL作负对照，MVID `a4e2fc2f-e854-4c93-bcbf-f5587723c740`；它不是用户手机上原始DLL，不能用重建MVID认定安装包完全相同。

## 实现

- 建筑助手自有 `BlueprintHttpPolicy` 无跨工程引用。public setter存在时经反射调用；不存在时仅接受已经留存Android程序集验证的 `_delegatingHandler → SocketsHttpHandler._settings → HttpConnectionSettings` 布局，设置 `_allowAutoRedirect=false`、`_useCookies=false`、`_automaticDecompression=None`，随后回读三个public getter。类型、字段或回读不符，在发送前拒绝并释放handler。TLS/证书校验不改。
- JSON类型以该运行库实际保留的 `ContentType.ToString()` 检查完整媒体类型边界；charset、压缩响应拒绝、长度/流式上限、12秒截止与取消保持。
- 扩大对云端、JSON、蓝图/家具导入和存储路径的Android元数据审计后，又检出七项缺失成员：`File.ReadAllText(path, encoding)`、`File.WriteAllText(path, text, encoding)`、`File.WriteAllBytes`、`File.Replace`、`DirectoryInfo.GetFiles(pattern)`、`FileSystemInfo.Delete`、`UInt32.ToString(format)`。改用已解析通过的流读写、Directory.GetFiles/File.Delete与八位十六进制逐位格式化。文件名哈希、UTF-8无BOM、重复下载和本地文件不覆盖语义保持。
- 私有缓存先完整写临时文件，再将旧完成文件改名为 `.previous`、发布新文件、移除前一份；两次重命名之间退出时，下次读取/写入恢复旧完成文件。已有新文件时以新文件为准；权威删除同时移除前一份，避免重新出现已删除内容。这是可恢复的两次重命名，不宣称单次原子替换；不涉及玩家蓝图库的覆盖。
- 日志细分 `http/deadline`、`http/handler`、`http/send`、`http/headers`、`http/body`、decode/cache，首行带有界异常类型/消息。仍只有网络/超时/HTTP5xx回退缓存，BCL错误不会伪装为离线成功。
- 修正Windows PowerShell构建参数中带空格OutputPath末尾反斜杠吞掉后续参数的问题；目标检查放在最终DLL生成后，并显式检查实际打包DLL。

目标仍net472/API1.42/SC2.4.10.6；版本/程序集/FileVersion同步为0.25.2。无加载器/Hook、ID/DATA/GUID、网络协议、世界存档、地形写入、材料族或蓝图schema变更。81种材料、10个家具模板及原边界/预算/取消/撤销/权限限制保持。

## 实际验证

仓库根执行（本机依赖只读，不提交）：

```powershell
$env:FrameworkPathOverride='D:/Dev/Tools/net472/build/.NETFramework/v4.7.2'
$env:DOTNET_ROOT='C:/Users/85196/AppData/Local/Microsoft/dotnet'
$env:DOTNET_ROLL_FORWARD='Major'
$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
# 修改前的0.25.1源码留存负对照
dotnet build src/BuilderAssistant/BuilderAssistant.csproj -c Release -p:SCNetGameDir=D:/Games/SCNET-x25.01.17 -o artifacts/blueprint-http-0.25.1-negative --nologo
# 修复后的最终完整构建
$env:BUILDER_CLOUD_HTTP_NEGATIVE_DLL=(Resolve-Path artifacts/blueprint-http-0.25.1-negative/BuilderAssistant.dll).Path
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-NetMod.ps1 -GameDir D:/Games/SCNET-x25.01.17 -Configuration Release
pwsh -NoProfile -File scripts/Test-BlueprintCloudAndroidBcl.ps1 -AndroidBclDir artifacts/celestial-audit/android-bcl -CecilPath D:/Dev/Tools/ilspy/.store/ilspycmd/9.1.0.7988/ilspycmd/9.1.0.7988/tools/net8.0/any/Mono.Cecil.dll -ModDll artifacts/build/BuilderAssistant.dll -NegativeDll artifacts/blueprint-http-0.25.1-negative/BuilderAssistant.dll
$env:BUILDER_CLOUD_LIVE='1'
& tests/BuilderAssistant.TargetTests/bin/Release/net472/BuilderAssistant.TargetTests.exe D:/Games/SCNET-x25.01.17 (Resolve-Path artifacts/build/BuilderAssistant.dll).Path
```

第一条旧版本构建命令只在修复前运行，不应对当前源码重跑并称为旧版。初次完整构建发现OutputPath问题；第一次扩大Android审计检出上述七项文件/格式调用。这些失败不是通过证据。最终成功日志位于忽略目录 `artifacts/blueprint-http-full-build-final.log`、`blueprint-http-android-audit.log`、`blueprint-http-live.log`，包独立检查为 `blueprint-http-package-check.json`。

| 范围 | 状态 | 证据/限制 |
|---|---|---|
| 固定Windows目标环境/ABI与Release | 已确认 | 根Test-Environment入口三程序集登记哈希匹配，net472 0警告0错误，白盒模型目标Engine导入通过 |
| 离线逻辑及网络/文件回归 | 已确认 | 3443项；包括schema/材料/预览/施工/撤销、模拟下载/断网/12秒超时、同版本保护、缓存128文件上限及中断恢复；真实loopback handler不跟随302、不回放Cookie |
| 最终Mod目标检查/负对照 | 已确认 | 2023项，49个路径类型及状态机IL检查；编译负对照四HTTP调用被拦截；源码重建0.25.1文件/HTTP引用被拦截 |
| 留存Android BCL成员/布局 | 已确认 | 最终DLL286个成员解析，0缺失，禁止桌面/GAC兜底；重建0.25.1精确检出11项缺失。只是元数据审计，不是Android执行 |
| 最终net472 DLL公网目录 | 已确认 | 生产HTTPS目录total=0、非缓存；开启探针后2025项目标检查；没有已发布蓝图可验证公网下载 |
| 0.25.2包内容 | 已确认 | 9条目，唯一BuilderAssistant.dll，modinfo/七份资源逐字节匹配源码，标准/版本副本一致；无游戏DLL或SCIEPort资源 |
| Android7723刷新/下载/重启离线、触屏及实际Local/Server/Client | 待验证 | 没有手机、完整游戏或联机执行，不从Windows/元数据推定兼容 |

Windows登记指纹：Survivalcraft.exe `3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD`；Engine.dll `98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694`；EntitySystem.dll `7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C`。

留存Android程序集SHA256：mscorlib `C51F1D8E4F5701F3D3DBFF9A98658D5F74B387BF5911B0F061527BE16ACAA3A3`，System `A109C9C23A34DA847B08771475469FF2FFD3BF6FF7CB5DBDE0B48FB82245F75E`，System.Net.Http `7C42DDD0D9DD231E343C08876F8D925150B0BA4D34A7B9CF602AE55D28713132`。mscorlib MVID与用户日志吻合，用户本次APK及其余BCL哈希未取得，7723重打包结论不能扩大。

产物 `artifacts/BuilderAssistant.netmod` / `BuilderAssistant-0.25.2.netmod`：2414734字节，SHA256 `6AC4360DED110DF3AE62B1E74C183777E4E440EF17C6FF2969C5A9EB37570550`；DLL SHA256 `3983CA4DB1FB02426E0498DD2475CDC6BF80D585AAC8A631451AEAA4A921A904`，MVID `416fe576-2241-40e9-9f66-90b6cd88bd77`，ProductVersion=0.25.2，不嵌入构建前的旧提交号。

## 安装与发布

只替换建筑助手，NetMods内保留一份新版BuilderAssistant包后完全退出重启；荒野探险家和阅读缓存不变。先在可丢弃世界测试903蓝图工具→云端→刷新→下载并载入；目录为空时须网站管理员发布样本后才可验收下载。随后断网重开检查本地副本与缓存，再按原权限分别验证单机、房主与普通加入者。不要用主存档首测。

任务开始远端main为 `3b6b08e`；修复尚仅在本地main，未推送前不得创建指向其它GitHub提交的0.25.2构建Release。保留经校验的包，待用户同步源码且确认构建提交可在GitHub访问后，以Android待验证预发布上传；助手不执行git push。远端其它SCIE-Port变更未纳入本次修复，本地/远端main仍有分叉，后续同步须合并保留两侧内容，不能强推覆盖。
