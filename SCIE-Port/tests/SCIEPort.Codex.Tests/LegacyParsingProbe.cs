using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

public static class LegacyParsingProbe
{
    static int checks;static Type parser;
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception("Legacy parsing: "+label);}
    static object Invoke(string name,object[] args){return parser.GetMethod(name,F).Invoke(null,args);}
    static void UInt32Case(string text)
    {
        uint expected;bool accepted=uint.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out expected);object[] args={text,uint.MaxValue};bool actual=(bool)Invoke("TryUInt32",args);
        Check(actual==accepted&&(uint)args[1]==expected,"decimal success/failure, exact value and empty failure out: "+text);
    }
    public static void Run(Assembly mod)
    {
        parser=mod.GetType("SCIEPort.Compatibility.LegacyParsing",true);
        foreach(string text in new[]{null,""," ","0","255","256","4294967295","4294967296","+255","-0","-1"," \t255\r\n","1.0","1e2","0xff","++1","1 2","１２","999999999999999999999999","0000000000000255"})UInt32Case(text);
        for(uint i=0;i<256;i++)UInt32Case(i.ToString(CultureInfo.InvariantCulture));
        var random=new Random(311);for(int i=0;i<1000;i++){uint value=unchecked((uint)random.Next()+(uint)random.Next());UInt32Case(value.ToString(CultureInfo.InvariantCulture));}
        foreach(string text in new[]{null,"","true","false","TRUE","FaLsE"," \tTrue\r\n","1","yes","tru e"})
        {
            bool expected;bool ok=bool.TryParse(text,out expected);object[] args={text,true};Check((bool)Invoke("TryBoolean",args)==ok&&(bool)args[1]==expected,"boolean value / malformed settings preserve behavior");
        }
        foreach(string text in new[]{null,"","0","64","2147483647","2147483648","-1"," 12","+1","1e2"})
        {
            int expected;bool ok=int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out expected);object[] args={text,NumberStyles.None,CultureInfo.InvariantCulture,123};
            Check((bool)Invoke("TryInt32",args)==ok&&(int)args[3]==expected,"strict portal limit parsing and overflow remain unchanged");
        }
        foreach(string text in new[]{null,"","0","-123.75","1e-20","1e20","1e1000","NaN","Infinity","-Infinity","1,25","oops"," \t+3.5\r\n"})
        {
            float expected;bool ok=float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out expected);object[] args={text,NumberStyles.Float,CultureInfo.InvariantCulture,123f};bool actual=(bool)Invoke("TrySingle",args);
            Check(actual==ok&&((float)args[3]).Equals(expected),"portal floating format / range remain unchanged: "+text);
        }
        foreach(string name in new[]{"StormhammerEffectQuality","FortressQuality"})
        {
            var type=mod.GetTypes().Single(t=>t.Name==name);var method=parser.GetMethod("TryEnum",F).MakeGenericMethod(type);
            foreach(string text in new[]{"low","mEdIum","HIGH","2","-1","Low,High","unknown","",null})
            {
                object expected=Activator.CreateInstance(type);bool ok=true;try{expected=Enum.Parse(type,text,true);}catch(ArgumentException){ok=false;}
                object[] args={text,true,Activator.CreateInstance(type)};Check((bool)method.Invoke(null,args)==ok&&args[2].Equals(expected),"quality enum names/numbers/invalid values remain unchanged");
            }
        }
        Settings(mod);
        Console.WriteLine("Legacy decimal/boolean/portal-number/enum/native settings: "+checks+" checks passed (desktop; Android retest pending).");
    }
    static void Settings(Assembly mod)
    {
        string[][] fields={
            new[]{"FourSaintsArmorRenderer","PreviewEnabled","SCIEPortFourSaintsArmorV3Preview"},new[]{"FourSaintsArmorRenderer","Diagnostics","SCIEPortFourSaintsArmorDiagnostics"},
            new[]{"QinglongCrownRenderer","PreviewEnabled","SCIEPortQinglongCrownV3Preview"},new[]{"QinglongCrownRenderer","Diagnostics","SCIEPortQinglongCrownDiagnostics"},
            new[]{"StormhammerOptions","ReducedFlashes","SCIEPortReducedLightningFlashes"},new[]{"StormhammerOptions","CameraShake","SCIEPortStormhammerCameraShake"},new[]{"StormhammerOptions","Diagnostics","SCIEPortStormhammerDiagnostics"},
            new[]{"FortressOptions","Diagnostics","SCIEPortFortressDiagnostics"},new[]{"StormhammerOptions","Quality","SCIEPortStormhammerVfxQuality"},new[]{"FortressOptions","Quality","SCIEPortFortressQuality"}};
        var saved=new Dictionary<FieldInfo,object>();var expected=new Dictionary<FieldInfo,object>();var xml=new XElement("Settings");
        for(int i=0;i<fields.Length;i++)
        {
            var row=fields[i];var field=mod.GetTypes().Single(t=>t.Name==row[0]).GetField(row[1],F);saved[field]=field.GetValue(null);
            object value=field.FieldType==typeof(bool)?(object)(i%2==0):Enum.ToObject(field.FieldType,1);expected[field]=value;xml.SetAttributeValue(row[2],field.FieldType==typeof(bool)?((bool)value?" True ":"fAlSe"):"medium");
        }
        try
        {
            var loader=Activator.CreateInstance(mod.GetType("SCIEPort.Mod.SCIEPortLoader",true));var load=loader.GetType().GetMethod("LoadSettings");load.Invoke(loader,new object[]{xml});
            foreach(var pair in expected)Check(pair.Key.GetValue(null).Equals(pair.Value),"actual loader applies boolean/quality setting "+pair.Key.Name);
            foreach(var row in fields)xml.SetAttributeValue(row[2],"invalid-value");load.Invoke(loader,new object[]{xml});
            foreach(var pair in expected)Check(pair.Key.GetValue(null).Equals(pair.Value),"malformed actual loader setting retains its prior value");
        }
        finally{foreach(var pair in saved)pair.Key.SetValue(null,pair.Value);}
    }
}
