using System;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace SCIEPort.Features.Blueprints.Networking
{
    // Independent SCIEPort adaptation of the verified 0.25.2 HTTP policy; no cross-project reference.
    internal static class BlueprintHttpPolicy
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static HttpMessageHandler Create()
        {
            var handler = new HttpClientHandler();
            try { Configure(handler); return handler; }
            catch { handler.Dispose(); throw; }
        }

        internal static void Configure(object handler)
        {
            string[] names = { "AllowAutoRedirect", "UseCookies", "AutomaticDecompression" };
            object[] expected = { false, false, DecompressionMethods.None };
            var properties = new PropertyInfo[names.Length];
            bool settersAvailable = true;
            for (int i = 0; i < names.Length; i++)
            {
                properties[i] = handler.GetType().GetProperty(names[i], BindingFlags.Public | BindingFlags.Instance);
                if (properties[i] == null || properties[i].PropertyType != expected[i].GetType() || properties[i].GetGetMethod() == null)
                    throw Unavailable(names[i]);
                if (properties[i].GetSetMethod() == null) settersAvailable = false;
            }
            if (settersAvailable)
            {
                // Reflection avoids direct MemberRefs to setters removed by Android's linker.
                for (int i = 0; i < names.Length; i++) properties[i].SetValue(handler, expected[i], null);
            }
            else
            {
                // Only this layout is evidenced by the locally supplied Android BCL snapshot.
                object backend = FieldValue(handler, "_delegatingHandler");
                if (backend == null || backend.GetType().FullName != "System.Net.Http.SocketsHttpHandler") throw Unavailable("backend");
                object settings = FieldValue(backend, "_settings");
                if (settings == null || settings.GetType().FullName != "System.Net.Http.HttpConnectionSettings") throw Unavailable("settings");
                string[] slots = { "_allowAutoRedirect", "_useCookies", "_automaticDecompression" };
                var fields = new FieldInfo[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    fields[i] = settings.GetType().GetField(slots[i], Fields);
                    if (fields[i] == null || fields[i].IsInitOnly || fields[i].FieldType != expected[i].GetType()) throw Unavailable(slots[i]);
                }
                for (int i = 0; i < slots.Length; i++) fields[i].SetValue(settings, expected[i]);
            }
            // Fail before sending if the runtime did not honor all three policies.
            for (int i = 0; i < names.Length; i++)
                if (!expected[i].Equals(properties[i].GetValue(handler, null))) throw Unavailable("readback " + names[i]);
        }

        private static object FieldValue(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, Fields);
            return field == null ? null : field.GetValue(target);
        }
        private static NotSupportedException Unavailable(string name) { return new NotSupportedException("Blueprint HTTP policy unavailable: " + name); }

        internal static bool IsJson(string value)
        {
            if (value == null) return false;
            int separator = value.IndexOf(';');
            return string.Equals((separator < 0 ? value : value.Substring(0, separator)).Trim(), "application/json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
