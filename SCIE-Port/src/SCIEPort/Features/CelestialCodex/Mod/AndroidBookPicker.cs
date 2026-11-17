using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Engine;

namespace SCIEPort.Features.CelestialCodex.Mod
{
    // Uses only JNI entry points verified in the retained x25 Mono.Android assembly.
    // Framework Fragment/SAF code is an original, embedded DEX, without a second managed assembly.
    internal sealed class AndroidBookPicker
    {
        internal const string Resource = "SCIEPort.CelestialCodex.DocumentPicker.dex";
        private static Jni jni;
        private static IntPtr bridge;
        private static string cacheRoot;
        private static AndroidBookPicker active;
        private static readonly HashSet<string> owned = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action<string> completed;
        private AndroidBookPicker(Action<string> completed) { this.completed=completed; }
        internal static bool Available => typeof(Window).GetProperty("Activity",BindingFlags.Public|BindingFlags.Static)!=null;
        internal static bool TryChoose(Action<string> completed)
        {
            if(!Available) return false;
            if(active!=null){BookFilePicker.LastError="选书正在进行";return false;}
            AndroidBookPicker operation=null;
            try
            {
                object activity=typeof(Window).GetProperty("Activity").GetValue(null,null);
                if(activity==null) throw new InvalidOperationException("Android Activity is unavailable");
                IntPtr handle=(IntPtr)activity.GetType().GetProperty("Handle").GetValue(activity,null);
                if(jni==null) jni=new Jni();
                if(bridge==IntPtr.Zero) Load(handle);
                operation=new AndroidBookPicker(completed);active=operation;
                Window.Frame+=operation.Poll;
                jni.StaticVoid(bridge,"start","(Landroid/app/Activity;)V",handle);
                return true;
            }
            catch(Exception error)
            {
                if(operation!=null)
                {
                    operation.Stop();
                    try{jni.StaticVoid(bridge,"cancel","()V");}catch(Exception){}
                }
                BookFilePicker.LastError="系统选书启动失败";
                Log.Warning("[CelestialCodex] Android picker: "+error);
                return false;
            }
        }
        private static void Load(IntPtr activity)
        {
            string directory=jni.Path(activity,"getCodeCacheDir");
            cacheRoot=jni.Path(activity,"getCacheDir")+"/CelestialCodexPicker/";
            byte[] dex;
            using(Stream input=typeof(AndroidBookPicker).Assembly.GetManifestResourceStream(Resource))
            {
                if(input==null) throw new InvalidDataException("Document picker resource missing");
                dex=Core.BoundedCache.ReadLimited(input,128*1024);
            }
            string hash;
            using(SHA256 sha=SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(dex)).Replace("-","");
            string path=Path.Combine(directory,"scie-codex-"+hash+".dex");
            if(!File.Exists(path))
            {
                // Android 14 requires dynamically loaded code to be read-only before it is written.
                try
                {
                    using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                    {
                        jni.ReadOnly(path);
                        output.Write(dex,0,dex.Length);
                    }
                }
                catch { try{File.Delete(path);}catch(Exception){} throw; }
            }
            else
            {
                using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                using(SHA256 sha=SHA256.Create())
                    if(BitConverter.ToString(sha.ComputeHash(Core.BoundedCache.ReadLimited(input,128*1024))).Replace("-","")!=hash) throw new InvalidDataException("Picker code cache mismatch");
                jni.ReadOnly(path);
            }
            IntPtr loader=IntPtr.Zero,parent=IntPtr.Zero,dexPath=IntPtr.Zero,optPath=IntPtr.Zero,kind=IntPtr.Zero,name=IntPtr.Zero,local=IntPtr.Zero;
            try
            {
                parent=jni.Object(activity,"getClassLoader","()Ljava/lang/ClassLoader;");
                kind=jni.Find("dalvik/system/DexClassLoader");dexPath=jni.Text(path);optPath=jni.Text(directory);
                loader=jni.New(kind,"(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/ClassLoader;)V",dexPath,optPath,IntPtr.Zero,parent);
                name=jni.Text("org.scieport.codex.CodexDocumentPicker");
                local=jni.Object(loader,"loadClass","(Ljava/lang/String;)Ljava/lang/Class;",name);
                bridge=jni.Global(local);
                // Validate all callback surfaces before opening the system UI.
                jni.StaticMethod(bridge,"getState","()I");jni.StaticMethod(bridge,"getPath","()Ljava/lang/String;");
                jni.StaticMethod(bridge,"getError","()Ljava/lang/String;");jni.StaticMethod(bridge,"cancel","()V");
            }
            finally{jni.Delete(loader);jni.Delete(parent);jni.Delete(dexPath);jni.Delete(optPath);jni.Delete(kind);jni.Delete(name);jni.Delete(local);}
        }
        private void Poll()
        {
            string selected=null;
            try
            {
                int state=jni.StaticInt(bridge,"getState");
                if(state==1) return;
                if(state==2)
                {
                    selected=jni.StaticText(bridge,"getPath");
                    string full=Path.GetFullPath(selected);
                    if(!full.StartsWith(cacheRoot,StringComparison.Ordinal)||!File.Exists(full)) throw new InvalidDataException("Unexpected selected file");
                    Core.BookImportService.FileName(full);
                    lock(owned) owned.Add(full);
                    selected=full;
                }
                else if(state==4) BookFilePicker.LastError=jni.StaticText(bridge,"getError")??"选书失败";
            }
            catch(Exception error)
            {
                selected=null;
                BookFilePicker.LastError="选书失败，请重试";Log.Warning("[CelestialCodex] Picker result: "+error);
                try{jni.StaticVoid(bridge,"cancel","()V");}catch(Exception){}
            }
            Stop();completed(selected);
        }
        private void Stop(){Window.Frame-=Poll;if(active==this) active=null;}
        internal static void Cancel(Action<string> callback)
        {
            AndroidBookPicker operation=active;
            if(operation==null||!ReferenceEquals(operation.completed,callback)) return;
            operation.Stop();
            try{jni.StaticVoid(bridge,"cancel","()V");}catch(Exception error){Log.Warning("[CelestialCodex] Picker cancel: "+error.Message);}
        }
        internal static void Release(string path)
        {
            if(path==null) return;
            lock(owned) if(!owned.Remove(path)) return;
            try
            {
                File.Delete(path);
                Directory.Delete(Path.GetDirectoryName(path)); // Only the owned UUID directory; never recursive.
            }
            catch(Exception error){Log.Warning("[CelestialCodex] Picker temporary file: "+error.Message);}
        }

        private sealed class Jni
        {
            private readonly Type api,values,ownership;
            private readonly Dictionary<string,MethodInfo> methods=new Dictionary<string,MethodInfo>();
            internal Jni()
            {
                api=Type.GetType("Android.Runtime.JNIEnv, Mono.Android",true);
                values=Type.GetType("Android.Runtime.JValue, Mono.Android",true);
                ownership=Type.GetType("Android.Runtime.JniHandleOwnership, Mono.Android",true);
                // Refuse a changed or stripped managed bridge before any external UI is started.
                Method("CallObjectMethod",typeof(IntPtr),typeof(IntPtr),values.MakeArrayType());
                Method("CallStaticVoidMethod",typeof(IntPtr),typeof(IntPtr),values.MakeArrayType());
                Method("CallStaticIntMethod",typeof(IntPtr),typeof(IntPtr),values.MakeArrayType());
                Method("CallStaticObjectMethod",typeof(IntPtr),typeof(IntPtr),values.MakeArrayType());
                Method("CallBooleanMethod",typeof(IntPtr),typeof(IntPtr),values.MakeArrayType());
                Method("NewObject",typeof(IntPtr),typeof(IntPtr),values.MakeArrayType());
                Method("GetString",typeof(IntPtr),ownership);
            }
            private MethodInfo Method(string name,params Type[] parameters)
            {
                MethodInfo method;
                if(!methods.TryGetValue(name,out method))
                {
                    method=api.GetMethod(name,BindingFlags.Public|BindingFlags.Static,null,parameters,null);
                    if(method==null) throw new MissingMethodException("JNIEnv."+name);
                    methods.Add(name,method);
                }
                return method;
            }
            private object Invoke(string name,Type[] signature,params object[] args) => Method(name,signature).Invoke(null,args);
            private Array Args(IntPtr[] handles)
            {
                Array array=Array.CreateInstance(values,handles.Length);
                for(int i=0;i<handles.Length;i++) array.SetValue(Activator.CreateInstance(values,new object[]{handles[i]}),i);
                return array;
            }
            internal IntPtr Find(string name) => (IntPtr)Invoke("FindClass",new[]{typeof(string)},name);
            internal IntPtr Text(string text) => (IntPtr)Invoke("NewString",new[]{typeof(string)},text);
            internal IntPtr Global(IntPtr value) => (IntPtr)Invoke("NewGlobalRef",new[]{typeof(IntPtr)},value);
            internal void Delete(IntPtr value){if(value!=IntPtr.Zero) Invoke("DeleteLocalRef",new[]{typeof(IntPtr)},value);}
            internal IntPtr StaticMethod(IntPtr type,string name,string signature) => (IntPtr)Invoke("GetStaticMethodID",new[]{typeof(IntPtr),typeof(string),typeof(string)},type,name,signature);
            private IntPtr Member(IntPtr type,string name,string signature) => (IntPtr)Invoke("GetMethodID",new[]{typeof(IntPtr),typeof(string),typeof(string)},type,name,signature);
            private object Call(string name,IntPtr target,IntPtr method,IntPtr[] args)
                => Invoke(name,new[]{typeof(IntPtr),typeof(IntPtr),values.MakeArrayType()},target,method,Args(args));
            internal IntPtr New(IntPtr type,string signature,params IntPtr[] args) => (IntPtr)Call("NewObject",type,Member(type,"<init>",signature),args);
            internal IntPtr Object(IntPtr target,string name,string signature,params IntPtr[] args)
            {
                IntPtr type=(IntPtr)Invoke("GetObjectClass",new[]{typeof(IntPtr)},target);
                try{return (IntPtr)Call("CallObjectMethod",target,Member(type,name,signature),args);}finally{Delete(type);}
            }
            internal void StaticVoid(IntPtr type,string name,string signature,params IntPtr[] args) => Call("CallStaticVoidMethod",type,StaticMethod(type,name,signature),args);
            internal int StaticInt(IntPtr type,string name) => (int)Call("CallStaticIntMethod",type,StaticMethod(type,name,"()I"),new IntPtr[0]);
            private string String(IntPtr value)
            {
                try{return value==IntPtr.Zero?null:(string)Invoke("GetString",new[]{typeof(IntPtr),ownership},value,Enum.ToObject(ownership,0));}
                finally{Delete(value);}
            }
            internal string StaticText(IntPtr type,string name) => String((IntPtr)Call("CallStaticObjectMethod",type,StaticMethod(type,name,"()Ljava/lang/String;"),new IntPtr[0]));
            internal string Path(IntPtr activity,string name)
            {
                IntPtr file=Object(activity,name,"()Ljava/io/File;");
                try{return String(Object(file,"getAbsolutePath","()Ljava/lang/String;"));}finally{Delete(file);}
            }
            internal void ReadOnly(string path)
            {
                IntPtr kind=Find("java/io/File"),text=Text(path),file=IntPtr.Zero;
                try
                {
                    file=New(kind,"(Ljava/lang/String;)V",text);
                    if(!(bool)Call("CallBooleanMethod",file,Member(kind,"setReadOnly","()Z"),new IntPtr[0])) throw new IOException("Cannot protect picker code");
                }
                finally{Delete(file);Delete(text);Delete(kind);}
            }
        }
    }
}
