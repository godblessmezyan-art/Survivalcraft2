using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

public static class WildlifeNativeProbe
{
    static int checks;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception("Wildlife: " + label); }
    public static int Run(XElement xml, Assembly mod)
    {
        var db = XmlDatabaseSerializer.LoadDatabase(xml);
        var gd = new GameDatabase(db);
        var nativeEgg = new EggBlock();
        var eggRoot=new Guid("300ff557-775f-4c7c-a88a-26655369f00b");
        // Traverse the same effective egg declarations as native EggBlock.Initialize,
        // without its graphics calls. This catches sparse tables and inherited collisions.
        int ownEggs=0;
        foreach(var entry in gd.Database.Root.GetExplicitNestingChildren(gd.ParameterSetType,false))
        {
            if(entry.EffectiveInheritanceRoot.Guid!=eggRoot) continue;
            int index=entry.GetNestedValue<int>("EggTypeIndex");
            if(index<0) continue;
            if(index>=nativeEgg.m_eggTypes.Count) nativeEgg.m_eggTypes.Count=index+1;
            nativeEgg.m_eggTypes[index]=new EggBlock.EggType {EggTypeIndex=index,TemplateName=entry.NestingParent.Name};
            if(index==50) {ownEggs++;Check(entry.NestingParent.Name=="SCIEPortChicken","egg 50 belongs to chicken");}
        }
        Check(ownEggs==1,"one effective chicken egg declaration");
        for(int i=0;i<nativeEgg.m_eggTypes.Count;i++) Check(nativeEgg.m_eggTypes[i]!=null,"dense native egg table "+i);
        foreach (string name in new[] { "Fox", "Rabbit", "Chicken", "Cat" })
        {
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(db.FindDatabaseObject("SCIEPort"+name, gd.EntityTemplateType, true));
            var model = values.GetValue<ValuesDictionary>("FourLeggedModel");
            Check(model.GetValue<string>("ModelName")=="Models/"+name,"model "+name);
            Check(model.GetValue<string>("TextureOverride")=="Textures/WildlifePalette","palette "+name);
            Check(model.GetValue<string>("Class")=="Game.Component"+(name=="Chicken"?"BirdModel":"FourLeggedModel"),"animation "+name);
            var egg = values.GetValue<ValuesDictionary>("CreatureEggData");
            Check(egg.GetValue<int>("EggTypeIndex")== (name=="Chicken"?50:-1),"unique egg "+name);
            Check(!egg.GetValue<bool>("ShowEgg"),"no vanilla creative egg migration "+name);
            var eat = values.GetValue<ValuesDictionary>("EatPickableBehavior").GetValue<ValuesDictionary>("FoodFactors");
            Check(eat.GetValue<float>(name=="Cat" || name=="Fox"?"Fish":"Grass")>0,"native food "+name);
            if (name=="Chicken")
            {
                var lay=values.GetValue<ValuesDictionary>("LayEggBehavior");
                Check(lay.GetValue<string>("Class")==typeof(ComponentLayEggBehavior).FullName,"native laying component");
                Check(Math.Abs(lay.GetValue<float>("LayFrequency")-1f/480)<1e-7,"480 second mean opportunity");
                foreach (string field in new[]{"FlyAnimationSpeed","PeckAnimationSpeed","WalkAnimationSpeed","WalkBobHeight"}) Check(model.GetValue<float>(field)>=0,"bird setting "+field);
                Check(egg.GetValue<float>("NutritionalValue")>0,"egg edible");
            }
            if (name=="Cat") Check(values.GetValue<ValuesDictionary>("Creature").GetValue<CreatureCategory>("Category")==CreatureCategory.LandPredator,"cat excluded from fox prey mask");
        }
        Check(nativeEgg.GetEggTypeByCreatureTemplateName("SCIEPortChicken").EggTypeIndex==50,"native lookup resolves chicken");
        int data=EggBlock.SetIsLaid(EggBlock.SetEggType(0,50),true);
        Check(nativeEgg.GetEggType(data).TemplateName=="SCIEPortChicken" && !EggBlock.GetIsCooked(data),"native laid-egg data roundtrip");
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var runtime=mod.GetType("SCIEPort.Features.Creatures.CreatureWorldSpawn",true);
        var spawn=new SubsystemCreatureSpawn();
        for(int repeat=0;repeat<3;repeat++) for(int i=0;i<4;i++) runtime.GetMethod("Register",flags).Invoke(null,new object[]{spawn,i});
        Check(spawn.m_creatureTypes.Count==4,"world registration idempotent");
        var old=Game.NetWork.CommonLib.WorkType;
        try
        {
            Game.NetWork.CommonLib.WorkType=Game.NetWork.WorkType.Client;
            foreach(var type in spawn.m_creatureTypes) Check(type.SpawnSuitabilityFunction(type,new Engine.Point3())==0,"client suitability rejected before world access");
            foreach(var type in spawn.m_creatureTypes) Check(type.SpawnFunction(type,new Engine.Point3())==0,"client spawn rejected before world access");
        }
        finally {Game.NetWork.CommonLib.WorkType=old;}
        return checks;
    }
}
