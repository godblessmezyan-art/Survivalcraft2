# 快艇美术资源规范

当前 v0.4.0 使用原创低多边形模型和用户提供的 1254×1254 PNG 快艇贴图，不依赖游戏本体模型或贴图。贴图按原始尺寸打包，没有重采样；当前 UV 仍按低多边形验证模型设计。若 Android 设备不支持该非 2 次幂尺寸，后续再生成 1024×1024 兼容版；若出现图案错位，则需要继续调整模型 UV。

## 网页图片生成规格

请生成一张正方形、无透视的材质图，而不是带背景的快艇效果图：

- 文件名：`Speedboat.png`
- 格式：PNG，RGBA 或 RGB，禁止 WebP/JPEG 伪装成 PNG。
- 尺寸：推荐 512×512；最低 256×256，边长必须是 2 的幂。
- 风格：与 Survivalcraft 的低多边形、清晰色块风格协调。
- 内容：红色玻璃钢船壳、白色横向装饰带、深灰防滑甲板、少量橙色警示色。
- 禁止：文字、Logo、水印、现实照片、透明船体、大面积高频噪点、透视场景和外部背景。
- 接缝：四边尽量可平铺，不要把关键图案贴在边缘。

可直接使用的提示词：

```text
Square seamless game texture atlas for a low-poly speedboat, flat orthographic material sheet, bold red fiberglass hull panels, clean white racing stripe, dark charcoal non-slip deck, tiny orange safety accents, simple Survivalcraft-like pixel-clean shapes, no boat silhouette, no perspective, no environment, no text, no logo, no watermark, 512x512 PNG
```

正式贴图路径为 `src/BuilderAssistant/Assets/Textures/Speedboat.png`。替换后重新构建，并在 Android 测试世界检查 UV、接缝和缩放。若最终外形需要更换，模型必须继续保留名为 `Speedboat` 的网格节点，否则 `SpeedboatBlock.Initialize()` 会找不到网格。
