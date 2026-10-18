using System;
using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// A single, defensive door into OPTIONAL game-internal services that are used to
    /// feed blueprint text into the import pipeline.
    ///
    /// IMPORTANT ABI REALITY: the members referenced here (<see cref="ClipboardManager"/>)
    /// are public <c>Game.*</c> types that compile against the verified Windows
    /// x25.01.17 assembly, but they are NOT part of the documented SCNET Mod API and
    /// their behavior on the 7723 Android client is UNVERIFIED. To honor the rule that
    /// an unverified interface must never be assumed to work, every call is wrapped so
    /// that a missing member, a signature mismatch, or a thrown exception simply
    /// reports "unavailable" instead of crashing <c>OnBlueprintTool</c> (which runs on
    /// the main thread). When this returns null the caller transparently falls back to
    /// the safe toast + file-library browse that only uses the proven
    /// ComponentGui.DisplaySmallMessage path.
    /// </summary>
    internal static class GameInterop
    {
        /// <summary>
        /// Read the OS clipboard as text, or return null when it is unavailable,
        /// empty, or errors on this platform. Android/7723: 待验证 — the direct
        /// <see cref="ClipboardManager.ClipboardString"/> binding is only compile-
        /// proven on Windows.
        /// </summary>
        public static string TryReadClipboardText()
        {
            try
            {
                return ClipboardManager.ClipboardString;
            }
            catch
            {
                // Any failure (missing member on Android, host not ready, etc.) is a
                // graceful "no clipboard", never an exception to the caller.
                return null;
            }
        }

        /// <summary>
        /// Reserved, NOT-yet-enabled entry point for a real Android Storage Access
        /// Framework document picker. Kept as a separate door so wiring SAF later does
        /// not disturb the clipboard path. Always reports unavailable until the native
        /// SAF call is proven on-device.
        /// </summary>
        /// <remarks>Android SAF: 不支持（未接入）— pending native-call verification.</remarks>
        public static bool TryPickBlueprintFileText(out string text, out string error)
        {
            text = null;
            error = "安卓文件选择器（SAF）尚未在 7723 客户端验证，暂不启用；请用剪贴板粘贴或放入蓝图库目录。";
            return false;
        }
    }
}
