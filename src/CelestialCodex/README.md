# 云天书典已整合进荒野探险家

自荒野探险家 **0.77.0** 起，云天书典直接编入 `SCIEPort.dll`，不再单独构建或安装。

- [使用、替换安装与验证](../../SCIE-Port/docs/CELESTIAL-CODEX.md)
- [当前源码](../../SCIE-Port/src/SCIEPort/Features/CelestialCodex/)
- [独立0.1.4 HTTP修复历史](../../docs/CELESTIAL-CODEX-HTTP-0.1.4.md)

用户在2026-11-04反馈独立0.1.4测试通过；整合包的Android/旧世界/实际多人需另行验收。956/DATA0与缓存目录保持，安装整合版时移除独立包；不要清理原阅读缓存。旧 `scripts/Build-CelestialCodex.ps1` 命令现在转到荒野探险家的Release构建。
