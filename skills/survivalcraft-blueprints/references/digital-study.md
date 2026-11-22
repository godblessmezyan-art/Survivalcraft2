# 数字书房发布契约

用户在2026-11-15要求：以后建筑蓝图生成并验证后，默认上传并发布到数字书房供网页/绘卷云端下载。授权限于生成的建筑JSON，不包含更改认证、建持久密钥、直接改生产数据库、删除他人蓝图或代push游戏源码。

目标页面：https://wxhappylife.top/digital-study/index.html#blueprints

公开API：`https://wxhappylife.top/digital-study/api/game-blueprints/v1/blueprints`。管理员API：同站点的`/api/admin/game-blueprints/v1/blueprints`，需要现有网站Bearer管理员身份。蓝图库与game-library书籍库分离。

权威源码：[协议](https://github.com/godblessmezyan-art/Digital-Study/blob/main/docs/GAME-BLUEPRINTS-V1.md)、[validator](https://github.com/godblessmezyan-art/Digital-Study/blob/main/apps/api/src/game-blueprints/blueprint-validator.ts)、[controller](https://github.com/godblessmezyan-art/Digital-Study/blob/main/apps/api/src/game-blueprints/game-blueprints.controller.ts)。上传结构错误时重新核对当前源码，不凭旧文档扩大生产权限。

当前核对的服务契约：

- UTF-8无BOM、1字节至2MiB；schema1/2/3可读，新文件统一3，kind固定builderassistant.blueprint。
- 网站最多10000格、每轴64。绘卷本地32768格/128轴不扩大网站限制。大建筑拆成共锚点模块，记录装配偏移。
- 网站结构校验接受材料key和f文本，但不证明Mod支持；先走真实Mod导入器/能力表。
- 网站只展开旧10个家具模板。AncientMoonWindow_A、AncientTeaCorner_A、AncientObservatory_A先经真实SCIEPort解析器导出flat cells，保留detail族和DATA。
- SHA256按完整原文件字节计算，含空白和末尾换行。校验后不重排JSON。
- 写请求不自动重试。401/403需恢复授权；409需重读ID/revision；超时先查目录与哈希，避免重复创建。

API顺序：

1. GET公开列表`?page=1&pageSize=50`，同SHA已发布则直接回读核验。
2. `POST /api/auth/login` JSON username/password，或用现有`DIGITAL_STUDY_ADMIN_TOKEN`；`GET /api/auth/me`核对role=admin。登录令牌仅在进程内使用，不自动保存。只有用户明确给出凭据配置位置才读取，不遍历浏览器数据库或其他凭据存储。
3. POST管理员`/validate`，multipart字段`file`，只预检。核对name/schema/kind/size/blockCount/byteLength/sha256。
4. POST管理员根路径，multipart `file`，创建草稿。明确修改旧蓝图才POST`/:id/json`并传multipart `expectedRevision`。
5. POST`/:id/publish`，JSON `{"expectedRevision":1}`。保存草稿不能算交付。
6. 匿名GET公开`/:id`、`/:id/json?revision=N`与列表，核对状态、元信息、原始文件字节和哈希。仅固定同域路径，不跟随重定向。

命令示例（相对当前checkout；GameDir和ModDll取实际验证环境）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-BlueprintJson.ps1 -Path SCIE-Port/blueprints/ancient-detail-kit.json -GameDir D:/Games/SCNET-x25.01.17 -ModDll SCIE-Port/artifacts/ancient-0.89.0/build/SCIEPort.dll -ReportPath artifacts/digital-study/ancient-detail-kit.validation.json
powershell -NoProfile -ExecutionPolicy Bypass -File SCIE-Port/scripts/Test-BlueprintJson.ps1 -Path SCIE-Port/blueprints/ancient-bookhouse-corners.json -GameDir D:/Games/SCNET-x25.01.17 -ModDll SCIE-Port/artifacts/ancient-0.89.0/build/SCIEPort.dll -PortableOutput artifacts/digital-study/ancient-bookhouse-corners.json -ReportPath artifacts/digital-study/ancient-bookhouse-corners.validation.json
python skills/survivalcraft-blueprints/scripts/publish_blueprints.py --item SCIE-Port/blueprints/ancient-detail-kit.json artifacts/digital-study/ancient-detail-kit.validation.json --item artifacts/digital-study/ancient-bookhouse-corners.json artifacts/digital-study/ancient-bookhouse-corners.validation.json --report artifacts/digital-study/publication.json --dry-run
```

凭据已配置时去掉`--dry-run`。用户已明确授权把现有登录保存到本机技能目录，使用`private/credentials.dpapi.json`：用户名和密码一起由Windows DPAPI CurrentUser加密，脚本只在内存解密并登录正常API。文件不进Git；脚本先找自身技能目录，再找CODEX_HOME（缺省~/.codex）的同名安装技能，也可显式传`--credential-file`。换电脑/Windows用户需重新授权配置。本机交互登录加`--login`，由用户自行输入密码。更新一个已检查条目用`--update-id UUID`。脚本没有删除、下架或改权限功能，报告不保存令牌。

本环境浏览器控制曾连续超时，公开HTTPS API正常；这不等于已登录。可改用已验证的正常API流程，不能提取磁盘浏览器会话或绕过管理员鉴权。
