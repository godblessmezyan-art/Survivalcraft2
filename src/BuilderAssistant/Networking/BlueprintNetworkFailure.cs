using System;
using BuilderAssistant.Core.Json;

namespace BuilderAssistant.Networking
{
    internal static class BlueprintNetworkFailure
    {
        internal static string Code(Exception error)
        {
            string result = "ERR";
            for (int i = 0; error != null && i < 8; i++, error = error.InnerException)
            {
                string name = error.GetType().FullName;
                if (name == "System.MissingMethodException" || name == "System.MissingFieldException" || name == "System.TypeLoadException" || name == "System.NotImplementedException" || name == "System.NotSupportedException") return "BCL";
                if (error is OperationCanceledException) return "TIME";
                if (name == "System.Security.Authentication.AuthenticationException" || name == "Mono.Security.Interface.TlsException" || name == "Javax.Net.Ssl.SSLHandshakeException" || name == "Javax.Net.Ssl.SSLPeerUnverifiedException") result = "TLS";
                else if (result != "TLS" && (error is System.IO.InvalidDataException || error is JsonParseException || name == "System.Text.DecoderFallbackException" || name == "System.FormatException")) result = "DATA";
                else if (result == "ERR" && (name == "System.Net.Http.HttpRequestException" || name == "System.Net.Sockets.SocketException" || name == "System.IO.IOException" || name == "System.IO.EndOfStreamException")) result = "NET";
            }
            return result;
        }
        internal static string Message(Exception error, string code)
        {
            switch (code)
            {
                case "BCL": return "运行库接口不兼容 [BCL]";
                case "TLS": return "安全连接失败 [TLS]";
                case "TIME": return "连接超时 [TIME]";
                case "NET": return "无法连接蓝图库 [NET]";
                case "DATA": return (error is System.IO.InvalidDataException ? error.Message : "蓝图库内容格式异常") + " [DATA]";
                default: return "蓝图库请求失败 [ERR]";
            }
        }
        internal static void Report(Action<string> diagnostics, string stage, string code, Exception error)
        {
            if (diagnostics == null) return;
            try
            {
                string detail = error.ToString();
                if (detail.Length > 8192) detail = detail.Substring(0, 8192);
                diagnostics("[BuilderAssistant] Cloud " + code + " stage=" + stage + "\n" + detail);
            }
            catch (Exception) { /* Logging must not replace the original failure. */ }
        }
    }
}
