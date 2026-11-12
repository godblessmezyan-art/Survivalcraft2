# 荒野探险家与云天书典统一主线

当前统一版本0.83.0，源码全部位于SCIE-Port/src/SCIEPort。阅读模块维护在Features/CelestialCodex；家居、天工绘卷与其它模块共用SCIEPort.csproj、SCIEPortLoader、modinfo.json和一份SCIEPort.dll。

所有入口生成荒野探险家.netmod及相同字节的版本副本。根Build-CelestialCodex和Build-WildernessExplorerFast保留为兼容入口并转发统一构建；不恢复src/CelestialCodex独立工程、CelestialCodex.dll或独立安装包。Builder Assistant与遗迹工程继续独立。

正常合并保留本地家居/多人/BCL与远端UI/导入/回收修复；运行代码保留书典956、三GUID、原阅读缓存，绘卷971及原家居身份。旧远端绘卷957的升级处理见SCIE-Port/docs/TIANGONG-BCL-0.82.0.md。

完整构建执行环境、自动回归及包内容校验。当前Windows和元数据证据不能代替Android7723/真实联机。详见SCIE-Port/docs/TIANGONG-DIRECT-0.83.0.md。
