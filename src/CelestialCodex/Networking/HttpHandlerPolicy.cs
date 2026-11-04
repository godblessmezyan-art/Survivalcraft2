using System;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace CelestialCodex.Networking
{
    internal static class HttpHandlerPolicy
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static HttpMessageHandler Create()
        {
            var handler = new HttpClientHandler();
            try { Configure(handler); return handler; }
            catch { handler.Dispose(); throw; }
        }
        // Do not emit direct setter MemberRefs: Android's linker removed all three.
        internal static void Configure(object handler)
        {
            string[] names = { "AllowAutoRedirect", "UseCookies", "AutomaticDecompression" };
            object[] values = { false, false, DecompressionMethods.None };
            var properties = new PropertyInfo[names.Length];
            bool publicSetters = true;
            for (int i = 0; i < names.Length; i++)
            {
                properties[i] = handler.GetType().GetProperty(names[i], BindingFlags.Public | BindingFlags.Instance);
                if (properties[i] == null || properties[i].GetGetMethod() == null) throw Unsupported(names[i]);
                if (properties[i].GetSetMethod() == null) publicSetters = false;
            }
            if (publicSetters)
            {
                for (int i = 0; i < names.Length; i++) properties[i].SetValue(handler, values[i], null);
            }
            else
            {
                // Verified against the locally supplied Android System.Net.Http assembly.
                // Unknown backends fail before sending; never silently allow redirects/cookies.
                object backend = ReadField(handler, "_delegatingHandler");
                if (backend == null || backend.GetType().FullName != "System.Net.Http.SocketsHttpHandler") throw Unsupported("backend");
                object settings = ReadField(backend, "_settings");
                if (settings == null || settings.GetType().FullName != "System.Net.Http.HttpConnectionSettings") throw Unsupported("settings");
                string[] fields = { "_allowAutoRedirect", "_useCookies", "_automaticDecompression" };
                var slots = new FieldInfo[fields.Length];
                for (int i = 0; i < fields.Length; i++)
                {
                    slots[i] = settings.GetType().GetField(fields[i], InstanceFields);
                    if (slots[i] == null || slots[i].IsInitOnly || slots[i].FieldType != values[i].GetType()) throw Unsupported(fields[i]);
                }
                for (int i = 0; i < fields.Length; i++) slots[i].SetValue(settings, values[i]);
            }
            for (int i = 0; i < names.Length; i++)
                if (!values[i].Equals(properties[i].GetValue(handler, null))) throw Unsupported("readback " + names[i]);
        }
        private static object ReadField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, InstanceFields);
            return field == null ? null : field.GetValue(target);
        }
        private static NotSupportedException Unsupported(string member) { return new NotSupportedException("HTTP handler policy unavailable: " + member); }
    }
}
