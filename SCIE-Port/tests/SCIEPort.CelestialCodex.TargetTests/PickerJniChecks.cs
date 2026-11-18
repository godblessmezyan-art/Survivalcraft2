using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// A strict reference table models the verified x25 Xamarin ownership contract.
// It exercises the actual compiled reflection bridge, without a phone or JNI VM.
internal static class PickerJniChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    internal static void Run(Assembly mod,Action<bool,string> check)
    {
        Type picker=mod.GetType("SCIEPort.Features.CelestialCodex.Mod.AndroidBookPicker",true);
        Type wrapper=picker.GetNestedType("Jni",BindingFlags.NonPublic);
        object jni=Activator.CreateInstance(wrapper,F,null,new object[]{typeof(StrictJni),typeof(Value),typeof(Ownership)},null);
        FieldInfo slot=picker.GetField("jni",F),bridge=picker.GetField("bridge",F);
        object saved=slot.GetValue(null),savedBridge=bridge.GetValue(null),savedRoot=picker.GetField("cacheRoot",F).GetValue(null);
        string root=Path.Combine(Path.GetTempPath(),"codex-jni-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);StrictJni.Root=root;slot.SetValue(null,jni);
        try
        {
            StrictJni.Reset();IntPtr global=StrictJni.FindClass("java/io/File");
            bool rejected=false;try{StrictJni.DeleteLocalRef(global);}catch(InvalidOperationException){rejected=true;}
            check(rejected,"negative control: old DeleteLocalRef(FindClass) violates retained Xamarin global-reference contract");
            StrictJni.DeleteGlobalRef(global);

            // Run the complete DEX preparation/class-loader path with BOTH new and cached files.
            for(int cached=0;cached<2;cached++)
            {
                StrictJni.Reset();bridge.SetValue(null,IntPtr.Zero);
                picker.GetMethod("Load",F).Invoke(null,new object[]{StrictJni.Activity});
                IntPtr retained=(IntPtr)bridge.GetValue(null);
                check(retained!=IntPtr.Zero&&StrictJni.IsGlobal(retained),"picker class retained as one global ref");
                check(StrictJni.Live==1&&StrictJni.BadDeletes==0,"new/cached DEX load releases all other local/global refs correctly");
                int operations=StrictJni.Calls;StrictJni.DeleteGlobalRef(retained);bridge.SetValue(null,IntPtr.Zero);
                // Inject a failure at EACH native boundary, including callback preflight.
                for(int fail=1;fail<=operations;fail++)
                {
                    if(cached==0) foreach(string file in Directory.GetFiles(root)) File.Delete(file);
                    StrictJni.Reset();StrictJni.FailAt=fail;
                    bool failed=false;try{picker.GetMethod("Load",F).Invoke(null,new object[]{StrictJni.Activity});}catch(TargetInvocationException){failed=true;}
                    check(failed,"injected JNI failure is propagated: "+fail);
                    check(StrictJni.Live==0&&StrictJni.BadDeletes==0,"all owned refs released on native boundary failure: "+fail);
                    check((IntPtr)bridge.GetValue(null)==IntPtr.Zero,"failed class validation never publishes a cached bridge: "+fail);
                }
            }
            StrictJni.Reset();
            bool zeroRejected=false;
            try{wrapper.GetMethod("Object",F).Invoke(jni,new object[]{IntPtr.Zero,"getClassLoader","()Ljava/lang/ClassLoader;",new IntPtr[0]});}
            catch(TargetInvocationException){zeroRejected=true;}
            check(zeroRejected&&StrictJni.Calls==0,"null Activity/object rejected before any native dereference");
            StrictJni.NullMethod=true;bool methodRejected=false;
            try{wrapper.GetMethod("Object",F).Invoke(jni,new object[]{StrictJni.Activity,"missing","()V",new IntPtr[0]});}
            catch(TargetInvocationException){methodRejected=true;}
            check(methodRejected&&StrictJni.Live==0&&StrictJni.ObjectCalls==0,"null JNI method rejected before invocation and class local ref released");
        }
        finally
        {
            slot.SetValue(null,saved);bridge.SetValue(null,savedBridge);picker.GetField("cacheRoot",F).SetValue(null,savedRoot);
            foreach(string file in Directory.GetFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }
    public struct Value { public IntPtr Handle;public Value(IntPtr handle){Handle=handle;} }
    public enum Ownership { DoNotTransfer=0 }
    public static class StrictJni
    {
        sealed class Entry {internal bool Global;internal string Text;}
        static readonly Dictionary<IntPtr,Entry> refs=new Dictionary<IntPtr,Entry>();
        static readonly Dictionary<IntPtr,string> methods=new Dictionary<IntPtr,string>();
        static int next;
        public static string Root;
        public static int Calls,FailAt,BadDeletes,ObjectCalls;
        public static bool NullMethod;
        public static readonly IntPtr Activity=new IntPtr(1);
        public static int Live=>refs.Count;
        public static bool IsGlobal(IntPtr value)=>refs[value].Global;
        public static void Reset(){refs.Clear();methods.Clear();next=10;Calls=FailAt=BadDeletes=ObjectCalls=0;NullMethod=false;}
        static void Gate(){Calls++;if(Calls==FailAt) throw new IOException("Injected native boundary failure");}
        static Entry Ref(IntPtr value){if(value==Activity) return new Entry {Text="activity"};return refs[value];}
        static IntPtr Add(bool global,string text){var value=new IntPtr(++next);refs.Add(value,new Entry {Global=global,Text=text});return value;}
        static void Delete(IntPtr value,bool global)
        {
            if(value==IntPtr.Zero) return;
            Entry entry;
            if(!refs.TryGetValue(value,out entry)||entry.Global!=global){BadDeletes++;throw new InvalidOperationException("Wrong JNI reference kind or double delete");}
            refs.Remove(value);
        }
        public static IntPtr FindClass(string name){Gate();return Add(true,name);}
        public static IntPtr NewString(string text){Gate();return Add(false,text);}
        public static IntPtr NewGlobalRef(IntPtr value){Gate();return Add(true,Ref(value).Text);}
        public static void DeleteGlobalRef(IntPtr value){Delete(value,true);}
        public static void DeleteLocalRef(IntPtr value){Delete(value,false);}
        public static IntPtr GetObjectClass(IntPtr value){Gate();Ref(value);return Add(false,"object-class");}
        static IntPtr Method(IntPtr type,string name){Gate();Ref(type);if(NullMethod) return IntPtr.Zero;var id=new IntPtr(++next);methods.Add(id,name);return id;}
        public static IntPtr GetMethodID(IntPtr type,string name,string signature)=>Method(type,name);
        public static IntPtr GetStaticMethodID(IntPtr type,string name,string signature)=>Method(type,name);
        public static string GetString(IntPtr value,Ownership ownership){Gate();if(ownership!=Ownership.DoNotTransfer) throw new Exception("Unexpected ownership transfer");return Ref(value).Text;}
        public static IntPtr NewObject(IntPtr type,IntPtr method,Value[] args){Gate();return Add(false,Ref(type).Text);}
        public static IntPtr CallObjectMethod(IntPtr target,IntPtr method,Value[] args)
        {
            Gate();ObjectCalls++;Ref(target);
            switch(methods[method])
            {
                case "getCodeCacheDir":case "getCacheDir":return Add(false,Root);
                case "getAbsolutePath":return Add(false,Ref(target).Text);
                case "getClassLoader":return Add(false,"parent-loader");
                case "loadClass":return Add(false,"picker-class");
                default:throw new Exception("Unexpected object method");
            }
        }
        public static bool CallBooleanMethod(IntPtr target,IntPtr method,Value[] args){Gate();Ref(target);return true;}
        public static void CallStaticVoidMethod(IntPtr type,IntPtr method,Value[] args){Gate();Ref(type);}
        public static int CallStaticIntMethod(IntPtr type,IntPtr method,Value[] args){Gate();Ref(type);return 1;}
        public static IntPtr CallStaticObjectMethod(IntPtr type,IntPtr method,Value[] args){Gate();Ref(type);return IntPtr.Zero;}
    }
}
