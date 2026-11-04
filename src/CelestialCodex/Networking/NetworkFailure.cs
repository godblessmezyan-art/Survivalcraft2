using System;

namespace CelestialCodex.Networking
{
    internal static class NetworkFailure
    {
        internal static string Code(Exception error)
        {
            string result = "NET";
            for (int i = 0; error != null && i < 8; i++, error = error.InnerException)
            {
                string name = error.GetType().FullName;
                if (name == "System.MissingMethodException" || name == "System.MissingFieldException" || name == "System.TypeLoadException" || name == "System.NotImplementedException" || name == "System.NotSupportedException") return "BCL";
                if (name == "System.Security.Authentication.AuthenticationException" || name == "Mono.Security.Interface.TlsException" || name == "Javax.Net.Ssl.SSLHandshakeException" || name == "Javax.Net.Ssl.SSLPeerUnverifiedException") result = "TLS";
                else if (result == "NET" && (name == "System.IO.InvalidDataException" || name == "System.Text.DecoderFallbackException" || name == "System.FormatException")) result = "DATA";
            }
            return result;
        }
        internal static string Message(string code)
        {
            switch (code)
            {
                case "BCL": return "网络接口不兼容 [BCL]";
                case "TLS": return "安全连接失败 [TLS]";
                case "DATA": return "内容格式异常 [DATA]";
                case "TIME": return "连接超时 [TIME]";
                default: return "连接失败 [NET]";
            }
        }
        internal static void Report(Action<string> diagnostics, string stage, string code, Exception error)
        {
            if (diagnostics == null) return;
            try
            {
                string detail = error == null ? "" : error.ToString();
                if (detail.Length > 8192) detail = detail.Substring(0, 8192);
                string summary = error == null ? "" : " type=" + error.GetType().FullName + " message=" + error.Message;
                summary = summary.Replace('\r', ' ').Replace('\n', ' ');
                if (summary.Length > 1024) summary = summary.Substring(0, 1024);
                diagnostics("[CelestialCodex] Network " + code + " stage=" + stage + summary + "\n" + detail);
            }
            catch (Exception) { /* Diagnostic sinks must never replace the original failure. */ }
        }
    }
}
