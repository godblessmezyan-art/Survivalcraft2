using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Engine;
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
        CheckArthropods(gd, mod);
        CheckLargeWildlife(gd, mod);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var runtime=mod.GetType("SCIEPort.Features.Creatures.CreatureWorldSpawn",true);
        var spawn=new SubsystemCreatureSpawn();
        for(int repeat=0;repeat<3;repeat++) for(int i=0;i<8;i++) runtime.GetMethod("Register",flags).Invoke(null,new object[]{spawn,i});
        Check(spawn.m_creatureTypes.Count==8,"world registration idempotent");
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

    static void CheckArthropods(GameDatabase gd, Assembly mod)
    {
        const string prefix = "SCIEPort.Features.Creatures.";
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        foreach (string species in new[] { "SandScorpion", "FiddlerCrab" })
        {
            bool scorpion = species == "SandScorpion";
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(gd.Database.FindDatabaseObject("SCIEPort" + species, gd.EntityTemplateType, true));
            var model = values.GetValue<ValuesDictionary>("ArthropodModel");
            Check(model.GetValue<string>("Class") == prefix + "ComponentWildArthropodModel", species + " model component");
            Check(model.GetValue<string>("ModelName") == "Models/" + species, species + " native asset");
            Check(model.GetValue<string>("TextureOverride") == "Textures/WildArthropods", species + " atlas");
            Check(values.GetValue<ValuesDictionary>("Arthropod").GetValue<bool>("IsScorpion") == scorpion, species + " behavior type");
            Check(values.GetValue<ValuesDictionary>("Body").GetValue<Vector3>("BoxSize").Y < .44f, species + " low physical body");
            Check(values.GetValue<ValuesDictionary>("Locomotion").GetValue<float>("FlySpeed") == 0f, species + " ground locomotion");
            if (!scorpion)
                Check(values.GetValue<ValuesDictionary>("Loot").GetValue<ValuesDictionary>("Loot").GetValue<string>("Crab") ==
                    "CrabBaitBlock;1;1", "crab drops one bait");
        }

        var loader = (ModLoader)Activator.CreateInstance(mod.GetType("SCIEPort.Mod.SCIEPortLoader", true));
        Check(loader.GetMaxInstancesCount() >= 14, "native shader can draw every scorpion bone");
        var packet = (Game.NetWork.IPackage)Activator.CreateInstance(mod.GetType(prefix + "ArthropodStingPackage", true));
        var nativePackages = typeof(Game.NetWork.IPackage).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(Game.NetWork.IPackage).IsAssignableFrom(t))
            .Select(t => (Game.NetWork.IPackage)Activator.CreateInstance(t));
        Check(packet.ID == 232 && nativePackages.All(p => p.ID != packet.ID), "native collision-free sting cue ID");
        packet.Handle(null, null, true);

        var habitat = mod.GetType(prefix + "ArthropodHabitat", true);
        var suitable = habitat.GetMethod("Suitable", flags);
        using (var terrain = new Terrain())
        {
            var chunk = terrain.AllocateChunk(0, 0);
            BlocksManager.Blocks[AirBlock.Index] = new AirBlock { IsCollidable = false };
            BlocksManager.Blocks[SandBlock.Index] = new SandBlock { IsCollidable = true };
            BlocksManager.Blocks[WaterBlock.Index] = new WaterBlock { IsCollidable = false };
            BlocksManager.Blocks[GraniteBlock.Index] = new GraniteBlock { IsCollidable = true };
            var p = new Point3(7, 65, 7);
            chunk.SetCellValueFast(p.X, p.Y - 1, p.Z, Terrain.MakeBlockValue(SandBlock.Index));
            chunk.SetShaftValueFast(p.X, p.Z, Terrain.ReplaceTopHeight(Terrain.ReplaceTemperature(0, 12), 64));
            Func<int, bool> habitatAt = species => (bool)suitable.Invoke(null, new object[] {terrain, species, p});
            Check(habitatAt(4), "warm exposed sand supports scorpion");
            Check(!habitatAt(5), "inland sand excludes crab");
            chunk.SetCellValueFast(8, 64, 7, Terrain.MakeBlockValue(WaterBlock.Index));
            chunk.SetCellValueFast(8, 63, 7, Terrain.MakeBlockValue(SandBlock.Index));
            Check(habitatAt(5), "shore beside shallow water supports crab");
            chunk.SetCellValueFast(8, 63, 7, Terrain.MakeBlockValue(WaterBlock.Index));
            Check(!habitatAt(5), "deep water does not provide crab refuge");
            chunk.SetCellValueFast(p.X, p.Y - 1, p.Z, Terrain.MakeBlockValue(GraniteBlock.Index));
            Check(!habitatAt(4), "bare granite is not a sand habitat");
            chunk.SetCellValueFast(p.X, p.Y - 1, p.Z, Terrain.MakeBlockValue(SandBlock.Index));
            chunk.SetCellValueFast(p.X, p.Y + 1, p.Z, Terrain.MakeBlockValue(GraniteBlock.Index));
            Check(!habitatAt(4) && !habitatAt(5), "covered site rejects both wildlife");
        }

        var strike = mod.GetType(prefix + "ComponentWildArthropod", true).GetMethod("CanSting", flags);
        Func<Vector3, bool> hit = delta => (bool)strike.Invoke(null, new object[] {delta, new Vector2(1, 0)});
        Check(hit(new Vector3(1.35f, .08f, .12f)), "locked strike line can hit");
        Check(!hit(new Vector3(1.35f, 0, .55f)), "side step escapes strike");
        Check(!hit(new Vector3(1.92f, 0, 0)) && !hit(new Vector3(-.2f, 0, 0)), "backing away or passing behind escapes strike");
        var bait = mod.GetType("SCIEPort.Features.Fishing.SubsystemFishing", true).GetMethod("IsBaitValue", flags);
        Func<int, bool> baitValue = value => (bool)bait.Invoke(null, new object[] {value});
        Check(baitValue(Terrain.MakeBlockValue(887)) && !baitValue(Terrain.MakeBlockValue(887, 0, 1)), "crab bait only accepts DATA 0");
        Check(baitValue(Terrain.MakeBlockValue(SeedsBlock.Index, 0, (int)SeedsBlock.SeedType.Rye)) &&
            baitValue(Terrain.MakeBlockValue(SeedsBlock.Index, 0, (int)SeedsBlock.SeedType.WildRye)), "existing rye baits preserved");
    }

    private static void CheckLargeWildlife(GameDatabase gd, Assembly mod)
    {
        const string prefix = "SCIEPort.Features.Creatures.";
        foreach (string species in new[] { "MossbackTurtle", "CloudwingRay" })
        {
            bool ray = species == "CloudwingRay";
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(gd.Database.FindDatabaseObject("SCIEPort" + species, gd.EntityTemplateType, true));
            var model = values.GetValue<ValuesDictionary>("LargeWildlifeModel");
            Check(model.GetValue<string>("Class") == prefix + "ComponentLargeWildlifeModel", species + " model binding");
            Check(model.GetValue<string>("ModelName") == "Models/" + species &&
                model.GetValue<string>("TextureOverride") == "Textures/LargeWildlife", species + " material and model");
            Check(values.GetValue<ValuesDictionary>("LargeWildlife").GetValue<string>("Class") == prefix + "Component" + species,
                species + " behavior binding");
            var movement = values.GetValue<ValuesDictionary>("Locomotion");
            Check(ray ? movement.GetValue<float>("FlySpeed") > 0 && movement.GetValue<float>("WalkSpeed") == 0
                : movement.GetValue<float>("FlySpeed") == 0 && movement.GetValue<float>("WalkSpeed") > 0, species + " locomotion");
            var category = values.GetValue<ValuesDictionary>("Creature").GetValue<CreatureCategory>("Category");
            Check(category == (ray ? CreatureCategory.Bird : CreatureCategory.LandOther), species + " neutral native category");
            var body = values.GetValue<ValuesDictionary>("Body");
            var box = body.GetValue<Vector3>("BoxSize");
            Check(ray ? box.X < 2f && box.Z > 2f : box.X > 2.3f && box.Y > 1.6f &&
                body.GetValue<float>("MaxSmoothRiseHeight") >= 1f, species + " contact body matches core anatomy");
        }

        var block = (Block)Activator.CreateInstance(mod.GetType(prefix + "LargeWildlifeSpawnBlock", true));
        int[] creative = block.GetCreativeValues().ToArray();
        Check(creative.SequenceEqual(new[] { Terrain.MakeBlockValue(891), Terrain.MakeBlockValue(891, 0, 1) }),
            "one block exposes both creative variants");
        Check(block.GetDisplayName(null, creative[0]) != block.GetDisplayName(null, creative[1]), "distinct spawner labels");
        var modelClass = mod.GetType(prefix + "ComponentLargeWildlifeModel", true);
        Check(typeof(IDrawable).IsAssignableFrom(modelClass), "ray can queue a native shadow even outside creature frustum");
        var packet = (Game.NetWork.IPackage)Activator.CreateInstance(mod.GetType(prefix + "LargeWildlifePosePackage", true));
        var nativePackages = typeof(Game.NetWork.IPackage).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(Game.NetWork.IPackage).IsAssignableFrom(t))
            .Select(t => (Game.NetWork.IPackage)Activator.CreateInstance(t));
        Check(packet.ID == 230 && nativePackages.All(p => p.ID != packet.ID), "large wildlife distinct native packet ID");
        packet.Handle(null, null, true);

        const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        var habitat = mod.GetType(prefix + "LargeWildlifeHabitat", true);
        var suitable = habitat.GetMethod("Suitable", Flags);
        var foothold = habitat.GetMethod("TurtleFooting", Flags);
        var perch = habitat.GetMethod("TryPerch", Flags);
        var air = habitat.GetMethod("TryFlightFloor", Flags);
        var spawn = habitat.GetMethod("SpawnPosition", Flags);
        BlocksManager.Blocks[AirBlock.Index] = new AirBlock { IsCollidable = false };
        BlocksManager.Blocks[GrassBlock.Index] = new GrassBlock { IsCollidable = true };
        BlocksManager.Blocks[GraniteBlock.Index] = new GraniteBlock { IsCollidable = true };
        BlocksManager.Blocks[WaterBlock.Index] = new WaterBlock { IsCollidable = false };

        using (var terrain = new Terrain())
        {
            var chunk = terrain.AllocateChunk(0, 0);
            chunk.State = TerrainChunkState.Valid;
            for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
            {
                bool wet = x >= 9;
                chunk.SetCellValueFast(x, wet ? 78 : 79, z, Terrain.MakeBlockValue(GrassBlock.Index));
                if (wet) chunk.SetCellValueFast(x, 79, z, Terrain.MakeBlockValue(WaterBlock.Index));
                int climate = Terrain.ReplaceHumidity(Terrain.ReplaceTemperature(0, 10), 9);
                chunk.SetShaftValueFast(x, z, Terrain.ReplaceTopHeight(climate, 79));
            }
            Point3 point = new Point3(6, 80, 7);
            var tiles = new object[] { terrain, new Vector3(8.5f, 80, 7.5f), true, 0 };
            Check((bool)suitable.Invoke(null, new object[] { terrain, false, point }), "turtle selects walkable wetland");
            Check((bool)foothold.Invoke(null, tiles) && (int)tiles[3] == 80, "broad turtle can step along one-block mixed bank");
            Check(!(bool)foothold.Invoke(null, new object[] { terrain, new Vector3(8.5f, 80, 7.5f), false, 0 }),
                "dry planting rejects water in shell footprint");
            for (int x = 9; x < 16; x++)
            for (int z = 0; z < 16; z++)
            {
                chunk.SetCellValueFast(x, 79, z, 0);
                chunk.SetCellValueFast(x, 78, z, 0);
                chunk.SetCellValueFast(x, 76, z, Terrain.MakeBlockValue(GrassBlock.Index));
                chunk.SetCellValueFast(x, 77, z, Terrain.MakeBlockValue(WaterBlock.Index));
            }
            Check(!(bool)suitable.Invoke(null, new object[] { terrain, false, point }), "turtle rejects a cliff separating the shore");
            Check(!(bool)foothold.Invoke(null, tiles), "wide shell cannot stride over a cliff");
        }

        using (var terrain = new Terrain())
        {
            var chunk = terrain.AllocateChunk(0, 0);
            chunk.State = TerrainChunkState.Valid;
            for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
            {
                chunk.SetCellValueFast(x, 79, z, Terrain.MakeBlockValue(GrassBlock.Index));
                chunk.SetShaftValueFast(x, z, Terrain.ReplaceTopHeight(Terrain.ReplaceHumidity(0, 8), 79));
            }
            Point3 point = new Point3(7, 80, 7);
            var center = new Vector3(7.5f, 80, 7.5f);
            var airArgs = new object[] { terrain, center, 0f };
            Check((bool)suitable.Invoke(null, new object[] { terrain, true, point }), "ray selects open highland");
            Check((bool)air.Invoke(null, airArgs) && (float)airArgs[2] == 80f, "ray corridor follows the highest sampled surface");
            Check((bool)perch.Invoke(null, new object[] { terrain, center, new Vector3() }), "ray lands in a clear wing footprint");
            Check(!(bool)air.Invoke(null, new object[] { terrain, new Vector3(15.5f, 82, 15.5f), 0f }), "ray avoids unloaded wingspan");
            Vector3 launch = (Vector3)spawn.Invoke(null, new object[] { point, true });
            Check(launch.Y == 87f && launch.X == 7.5f && launch.Z == 7.5f, "natural ray begins in air over selected land");
            chunk.SetCellValueFast(11, 80, 7, Terrain.MakeBlockValue(GraniteBlock.Index));
            chunk.SetShaftValueFast(11, 7, Terrain.ReplaceTopHeight(0, 80));
            Check(!(bool)perch.Invoke(null, new object[] { terrain, center, new Vector3() }), "ray rejects a wingtip obstruction");
        }
    }
}
