# 统一.netmod发布

目标仓库为git origin对应的godblessmezyan-art/Survivalcraft2，主线main。当前荒野探险家0.83.0包含家居、天工绘卷、云天书典及现有优化，只有SCIEPort.dll和一个modinfo。

scripts/Publish-WildernessExplorerRelease.ps1使用统一Build-NetMod入口执行环境、Release、自动回归、原生资源与包内容校验。旧Fast脚本名称只保留转发兼容，不跳过验证。书典不独立发布。

上传前核对版本/目标仓库/实际构建提交及可访问性；没有推送的提交不得冒用其它提交发布。本次用户明确授权正常push main；其它任务默认边界见AGENTS.md。禁止force push、移动历史标签或覆盖他人资产。

已有同版本只在标签指向相同提交后核对资产SHA256，下载验证后复用；缺资产时可补传。不同提交/资产失败停止，不clobber远端。上传后再次核对远端digest。Android及真实房间未测试则保持预发布/待验证，不把Windows成功称为手机兼容。
