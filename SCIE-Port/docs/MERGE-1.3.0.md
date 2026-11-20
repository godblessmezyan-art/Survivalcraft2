# 主线合并验证 1.3.0

本地 ee01239 与远端 d0ef623 完整合并。远端跟踪引用通过显式 fetch 更新；本机仅跟踪 port 的配置未更改。骨龙三处未提交工作保留，构建使用 Git 索引快照，不纳入这些改动。已检查两个已知工作副本的 main 均为 ba27dd0。

运行根 scripts/Test-Environment.ps1 -GameDir D:/Games/SCNET-x25.01.17，通过固定程序集哈希与 ABI 检查。设置 FrameworkPathOverride 为 D:/Dev/Tools/net472/build/.NETFramework/v4.7.2，运行快照 SCIE-Port/scripts/Build-NetMod.ps1 -GameDir D:/Games/SCNET-x25.01.17 -OutputDir artifacts/merge-1.3.0/output，完整 Release 和包校验通过。首次快照构建因未链接本机授权上游目录而在打包失败；链接后完整重跑通过。特殊地形固定目标测试 126134 项通过。构建日志保存在 artifacts/merge-1.3.0/build-final.log。

包 SHA256：3DD689A1EBA28080B43C2CB5067921FD58F7726541C37EF612317BA89A15086C。Android 7723、旧世界与真实联机仍待验证。本地合并提交未推送，因此不发布指向其他提交的 Release。
