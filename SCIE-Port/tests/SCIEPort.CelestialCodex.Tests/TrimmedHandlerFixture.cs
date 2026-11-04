using System.Net;

// Simulates the names/field layout verified in the local Android snapshot.
// These types stay in the test executable; no proprietary code is included.
namespace System.Net.Http
{
    internal sealed class SocketsHttpHandler
    {
        internal readonly HttpConnectionSettings _settings = new HttpConnectionSettings();
    }
    internal sealed class HttpConnectionSettings
    {
        internal bool _allowAutoRedirect = true;
        internal bool _useCookies = true;
        internal DecompressionMethods _automaticDecompression = DecompressionMethods.GZip;
    }
}
internal sealed class TrimmedHandler
{
    private readonly System.Net.Http.SocketsHttpHandler _delegatingHandler = new System.Net.Http.SocketsHttpHandler();
    public bool AllowAutoRedirect { get { return _delegatingHandler._settings._allowAutoRedirect; } }
    public bool UseCookies { get { return _delegatingHandler._settings._useCookies; } }
    public DecompressionMethods AutomaticDecompression { get { return _delegatingHandler._settings._automaticDecompression; } }
}
internal sealed class UnknownHandler
{
    public bool AllowAutoRedirect { get { return true; } }
    public bool UseCookies { get { return true; } }
    public DecompressionMethods AutomaticDecompression { get { return DecompressionMethods.None; } }
}
internal sealed class IgnoredSetterHandler
{
    public bool AllowAutoRedirect { get { return true; } set { } }
    public bool UseCookies { get { return true; } set { } }
    public DecompressionMethods AutomaticDecompression { get { return DecompressionMethods.None; } set { } }
}
