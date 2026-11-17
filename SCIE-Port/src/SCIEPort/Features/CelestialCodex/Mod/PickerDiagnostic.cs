using System;
using System.IO;
using System.Reflection;
using System.Text;
using Engine;

namespace SCIEPort.Features.CelestialCodex.Mod
{
    internal static class PickerDiagnostic
    {
        // Persist BEFORE native entry points: a VM abort bypasses managed catches/logging.
        // Only the last fixed stage/error type is stored, never book paths or contents.
        internal static void Write(string stage,Exception error=null)
        {
            try
            {
                FieldInfo field=typeof(ModsManager).GetField("UserDataPath",BindingFlags.Public|BindingFlags.Static);
                string data=field==null?null:field.GetValue(null) as string;
                if(string.IsNullOrEmpty(data)) return;
                string parent=Storage.GetDirectoryName(data);
                if(string.IsNullOrEmpty(parent)) return;
                string root=Storage.CombinePaths(parent,"CelestialCodex");
                Storage.CreateDirectory(root);
                byte[] bytes=Encoding.UTF8.GetBytes(typeof(AndroidBookPicker).Assembly.GetName().Version+" "+DateTime.UtcNow.ToString("o")+" "+stage+
                    (error==null?"":" "+error.GetType().FullName));
                using(Stream output=Storage.OpenFile(Storage.CombinePaths(root,"picker-last.txt"),OpenFileMode.Create))
                {output.Write(bytes,0,bytes.Length);output.Flush();}
            }
            catch(Exception) { } // A diagnostic failure must not prevent choosing a book.
        }
    }
}
