using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

public static class PyramidGuardianProbe
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const int Full = (1 << 25) - 1;
    private static Type siteType, layoutType, runtimeType, stateType, guardianType;
    private static int checks;

    public static void Run(Assembly mod, GameDatabase database, PyramidNativeProbe.Fixture fixture, string assets)
    {
        const string prefix = "SCIEPort.Features.Pyramid.";
        siteType = mod.GetType(prefix + "PyramidSite", true);
        layoutType = mod.GetType(prefix + "PyramidLayout", true);
        runtimeType = mod.GetType(prefix + "SubsystemPyramid", true);
        stateType = mod.GetType(prefix + "PyramidState", true);
        guardianType = mod.GetType(prefix + "PyramidGuardians", true);

        var caches = (IDictionary)typeof(ContentManager).GetField("Caches", Static).GetValue(null);
        var log = new ConsoleLogSink();
        Log.AddLogSink(log);
        using (var png = File.OpenRead(Path.Combine(assets, "Textures/PyramidGuardians.png")))
        {
            var image = Engine.Media.Image.Load(png);
            Check(image.Width == 256 && image.Height == 256, "native atlas import");
        }
        // The headless placeholder owns no GPU object or render-thread dispatcher.
        var texture = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
        GC.SuppressFinalize(texture);
        caches.Add("Textures/PyramidGuardians", new List<object> { texture });
        try
        {
            Template(database, assets, caches, "Mummy", "木乃伊", 160f, 12f, 2.6f, 20f, 10f,
                new[] { "CottonWadBlock;1;2", "CopperIngotBlock;1;1;0.3" });
            Template(database, assets, caches, "Pharaoh", "法老", 480f, 24f, 3.8f, 24f, 35f,
                new[] { "DiamondChunkBlock;1;2", "IronIngotBlock;2;4" });
            Lifecycle(database, fixture, WorkType.Local);
            Lifecycle(database, fixture, WorkType.Server);
            Lifecycle(database, fixture, WorkType.Local, true);
            Lifecycle(database, fixture, WorkType.Server, true);
            Decor();
        }
        finally
        {
            foreach (string name in new[] { "Mummy", "Pharaoh" })
            {
                caches.Remove("Models/Pyramid" + name);
                DatabaseManager.m_valueDictionaries.Remove("SCIEPort" + name);
            }
            caches.Remove("Textures/PyramidGuardians");
            CommonLib.WorkType = WorkType.Local;
            Log.RemoveLogSink(log);
        }
        Console.WriteLine("Pyramid guardian templates, meshes, combat health and encounter lifecycle: " + checks + " native checks passed.");
    }

    private static void Template(GameDatabase database, string assets, IDictionary caches,
        string name, string display, float health, float damage, float speed, float chaseTime, float fireResilience, string[] loot)
    {
        var values = new ValuesDictionary();
        var template = database.Database.FindDatabaseObject("SCIEPort" + name, database.EntityTemplateType, true);
        values.PopulateFromDatabaseObject(template);
        DatabaseManager.m_valueDictionaries.Add(template.Name, values);
        var modelValues = values.GetValue<ValuesDictionary>("HumanModel");
        var creature = values.GetValue<ValuesDictionary>("Creature");
        var chase = values.GetValue<ValuesDictionary>("ChaseBehavior");
        Check(modelValues.GetValue<string>("Class") == typeof(ComponentHumanModel).FullName &&
            modelValues.GetValue<string>("ModelName") == "Models/Pyramid" + name &&
            modelValues.GetValue<string>("TextureOverride") == "Textures/PyramidGuardians" &&
            creature.GetValue<string>("DisplayName") == display && creature.GetValue<bool>("ConstantSpawn") &&
            creature.GetValue<CreatureCategory>("Category") == CreatureCategory.LandPredator,
            name + " native animation and creature profile");
        Check(values.GetValue<ValuesDictionary>("Health").GetValue<float>("AttackResilience") == health &&
            values.GetValue<ValuesDictionary>("Miner").GetValue<float>("AttackPower") == damage &&
            values.GetValue<ValuesDictionary>("Locomotion").GetValue<float>("WalkSpeed") == speed &&
            values.GetValue<ValuesDictionary>("Health").GetValue<float>("FireResilience") == fireResilience &&
            chase.GetValue<CreatureCategory>("AutoChaseMask") == 0 && chase.GetValue<float>("DayChaseRange") > 0f &&
            chase.GetValue<float>("NightChaseRange") == chase.GetValue<float>("DayChaseRange") &&
            chase.GetValue<float>("DayChaseTime") == chaseTime && chase.GetValue<float>("NightChaseTime") == chaseTime,
            name + " combat settings");
        var lootTable = values.GetValue<ValuesDictionary>("Loot").GetValue<ValuesDictionary>("Loot");
        Check(lootTable.Values.Count() == loot.Length && loot.All(entry => lootTable.Values.Contains(entry)), name + " loot table");
        var egg = values.GetValue<ValuesDictionary>("CreatureEggData");
        Check(egg.GetValue<int>("EggTypeIndex") == -1 && !egg.GetValue<bool>("ShowEgg") &&
            !values.GetValue<ValuesDictionary>("Spawn").GetValue<bool>("AutoDespawn"), name + " resident only");

        ModelData data;
        using (var dae = File.OpenRead(Path.Combine(assets, "Models/Pyramid" + name + ".dae")))
            data = ModelData.Load(dae);
        var model = new Model();
        foreach (var bone in data.Bones)
            model.NewBone(bone.Name, bone.Transform, bone.ParentBoneIndex < 0 ? null : model.Bones[bone.ParentBoneIndex]);
        Check(model.RootBone.Name == "Body" && data.Bones.Count == 6 && data.Meshes.Count == 6 &&
            new[] { "Head", "Hand1", "Hand2", "Leg1", "Leg2" }.All(bone => model.FindBone(bone).ParentBone == model.RootBone),
            name + " six-bone native biped");
        int triangles = 0;
        var size = values.GetValue<ValuesDictionary>("Body").GetValue<Vector3>("BoxSize");
        foreach (var mesh in data.Meshes)
        {
            Vector3 pivot = data.Bones[mesh.ParentBoneIndex].Transform.Translation;
            Vector3 min = mesh.BoundingBox.Min + pivot, max = mesh.BoundingBox.Max + pivot;
            Check(min.Y >= -0.001f && max.Y <= size.Y && min.X >= -size.X / 2 && max.X <= size.X / 2 &&
                min.Z >= -size.Z / 2 && max.Z <= size.Z / 2, name + " geometry fits collision size");
            foreach (var part in mesh.MeshParts) triangles += part.IndicesCount / 3;
        }
        Check(triangles > 500 && triangles <= 1500, name + " triangle budget");
        caches.Add("Models/Pyramid" + name, new List<object> { model });
    }

    private static void Lifecycle(GameDatabase db, PyramidNativeProbe.Fixture fixture, WorkType role, bool maze = false)
    {
        CommonLib.WorkType = role;
        Project project;
        SubsystemBlockBehavior palace;
        var terrain = fixture(db, TerrainGenerationMode.Continent, out project, out palace, 711);
        var sky = new SubsystemSky();
        foreach (Subsystem subsystem in new Subsystem[]
        {
            new SubsystemAudio(), new SubsystemAmbientSounds(), new SubsystemBlockBehaviors(),
            new SubsystemMovingBlocks(), new SubsystemNoise(), new SubsystemParticles(), new SubsystemPathfinding(),
            new SubsystemPickables(), new SubsystemPlayerStats(), sky, new SubsystemSoundMaterials(),
            new SubsystemTimeOfDay(), new SubsystemWaterBlockBehavior(), new SubsystemModelsRenderer { m_subsystemSky = sky }
        })
            TidePalaceProbe.Add(project, subsystem);

        // Use the real project component loading to surface missing template dependencies.
        foreach (string name in new[] { "SCIEPortMummy", "SCIEPortPharaoh" })
            DatabaseManager.CreateEntity(project, name, true).Dispose();
        if (!maze)
        {
            CheckHealth(project, "SCIEPortMummy", 160f, "Excalibur.ExcaliburRules", "ImpactPower", 1);
            CheckHealth(project, "SCIEPortPharaoh", 480f, "Gungnir.GungnirRules", "JudgmentPower", 1);
        }

        var runtime = (SubsystemBlockBehavior)Create(runtimeType);
        TidePalaceProbe.Add(project, runtime);
        var origin = new Point2(-80, -80);
        var site = maze ? Create(siteType, origin, 72, 0, 953, -7798, -6, true) : Create(siteType, origin, 72, 0, 953);
        var row = new ValuesDictionary();
        row.SetValue("Origin", origin);
        row.SetValue("Base", 72);
        row.SetValue("Order", 0);
        row.SetValue("Reward", 953);
        row.SetValue("Slices", Full - 1);
        if (maze)
        {
            row.SetValue("MazeSeed", -7798);
            row.SetValue("ApproachFloor", -6);
            row.SetValue("Tomb", true);
        }
        var records = new ValuesDictionary();
        records.SetValue("Pyramid0", row);
        var oldSave = new ValuesDictionary();
        oldSave.SetValue("Pyramids", records);
        runtime.Load(oldSave);
        var state = State(runtime);
        Check(Spawned(state) == 0, role + " pyramid starts with no spawned guards");
        var positionMethod = guardianType.GetMethod("SpawnPosition", Static);
        Vector3[] points = Enumerable.Range(0, maze ? 9 : 3)
            .Select(i => (Vector3)positionMethod.Invoke(null, new[] { site, (object)i })).ToArray();
        for (int i = 0; i < points.Length; i++) WriteRoom(terrain.Terrain, site, points[i]);
        if (maze)
        {
            var spawn = Terrain.ToCell(points[0]);
            int spawnSlice = (int)siteType.GetMethod("ChunkBit", Instance)
                .Invoke(site, new object[] { new Point2(spawn.X, spawn.Z) });
            TidePalaceProbe.Put(state, "Slices", Full & ~spawnSlice);
            Check(spawn.X - origin.X == 37 && spawn.Z - origin.Y == 20, "first mummy guards the entry gallery");
        }

        var player = TidePalaceProbe.MakePlayer(project);
        var playerBody = new ComponentBody { m_entity = player.Entity };
        TidePalaceProbe.Put(player, "<ComponentBody>k__BackingField", playerBody);
        var players = project.FindSubsystem<SubsystemPlayers>(true);
        players.m_playersData.Add(player.PlayerData);
        players.UpdateComponentPlayers();
        var time = project.FindSubsystem<SubsystemTime>(true);
        Action tick = () => { time.m_gameTime += .5; ((IUpdateable)runtime).Update(.5f); };
        Vector3 approach = points[0] - new Vector3(0, 0, maze ? 1 : 4);
        playerBody.Position = approach;
        tick();
        Check(Spawned(state) == 0 && project.Entities.Count == 0, role + " ungenerated room never spawns");
        TidePalaceProbe.Put(state, "Slices", maze ? Full - 1 : Full);
        playerBody.Position = points[0] + new Vector3(0, 20, 0);
        tick();
        Check(Spawned(state) == 0, role + " surface above the room is excluded");
        playerBody.Position = approach;
        CommonLib.WorkType = WorkType.Client;
        tick();
        Check(Spawned(state) == 0 && project.Entities.Count == 0, "client cannot create a guard");
        CommonLib.WorkType = role;

        var p = Terrain.ToCell(points[0]);
        terrain.Terrain.SetCellValueFast(p.X, p.Y + 1, p.Z, GraniteBlock.Index);
        tick();
        Check(Spawned(state) == 0, role + " obstruction defers the spawn");
        terrain.Terrain.SetCellValueFast(p.X, p.Y + 1, p.Z, 0);
        var bodies = project.FindSubsystem<SubsystemBodies>(true);
        playerBody.BoxSize = new Vector3(.8f, 1.8f, .8f);
        playerBody.Position = points[0];
        bodies.AddBody(playerBody);
        tick();
        Check(Spawned(state) == 0, role + " occupied point waits for player to move");
        playerBody.Position = approach;
        tick();
        Check(Spawned(state) == 1 && project.Entities.Count == 1, role + " main hall creates one resident");
        Entity first = project.Entities.Single();
        Check(first.ValuesDictionary.DatabaseObject.Name == "SCIEPortMummy" &&
            first.FindComponent<ComponentHumanModel>(true) != null &&
            first.FindComponent<ComponentBody>(true).Position == points[0], role + " native mummy entity at the selected point");
        project.RemoveEntity(first, true);
        tick();
        Check(project.Entities.Count == 0, role + " removed creature does not reappear on a later update");
        if (maze)
        {
            for (int i = 1; i < points.Length; i++)
            {
                playerBody.Position = points[i] - new Vector3(0, 0, 1);
                tick();
                Check(Spawned(state) == (1 << (i + 1)) - 1, role + " reaches distinct maze encounter " + i);
            }
            Check(project.Entities.Count(entity => entity.ValuesDictionary.DatabaseObject.Name == "SCIEPortMummy") == 7 &&
                project.Entities.Count(entity => entity.ValuesDictionary.DatabaseObject.Name == "SCIEPortPharaoh") == 1,
                role + " maze spawned eight mummies including the removed one, and one pharaoh without a seal");
        }
        else
        {
            playerBody.Position = points[1] + new Vector3(0, 0, 4);
            tick();
            Check(Spawned(state) == 3 && project.Entities.Single().ValuesDictionary.DatabaseObject.Name == "SCIEPortMummy",
                role + " separate maze resident");

            playerBody.Position = points[2] - new Vector3(0, 1, 5);
            tick();
            Check(Spawned(state) == 3, role + " sealed vault never spawns the pharaoh");
            for (int seal = 0; seal < 3; seal++) stateType.GetMethod("Press", Instance).Invoke(state, new object[] { seal });
            tick();
            Check(Spawned(state) == 7 && project.Entities.Count(entity => entity.ValuesDictionary.DatabaseObject.Name == "SCIEPortPharaoh") == 1,
                role + " opened vault creates exactly one pharaoh");
        }

        var save = new ValuesDictionary();
        runtime.Save(save);
        runtime = (SubsystemBlockBehavior)Create(runtimeType);
        TidePalaceProbe.Add(project, runtime);
        runtime.Load(save);
        Check(Spawned(State(runtime)) == (maze ? 511 : 7), role + " once-only flags survive loading");
        foreach (Entity entity in project.Entities.ToArray()) project.RemoveEntity(entity, true);
        tick();
        Check(project.Entities.Count == 0, role + " consumed points do not recreate removed creatures after reload");
    }

    private static void CheckHealth(Project project, string template, float maximum,
        string ruleType, string powerName, int survivedHits)
    {
        var rules = guardianType.Assembly.GetType("SCIEPort.Features." + ruleType, true);
        float power = (float)rules.GetField(powerName, Static).GetRawConstantValue();
        Entity entity = DatabaseManager.CreateEntity(project, template, true);
        var health = entity.FindComponent<ComponentHealth>(true);
        Check(health.Health == 1f && health.AttackResilience == maximum, template + " starts at its new maximum");
        for (int i = 0; i < survivedHits; i++)
            health.Injure(power / health.AttackResilience, null, false, powerName);
        Check(health.Health > 0f && Math.Abs(health.Health * maximum - (maximum - power * survivedHits)) < .001f,
            template + " survives the expected native injury from " + powerName);
        health.Injure(power / health.AttackResilience, null, false, powerName);
        Check(health.Health == 0f, template + " still dies to accumulated damage");
        entity.Dispose();

        // Native saves persist the health fraction and resolve resilience from the entity template at load.
        var values = new ValuesDictionary();
        values.PopulateFromDatabaseObject(DatabaseManager.FindEntityValuesDictionary(template, true).DatabaseObject);
        values.GetValue<ValuesDictionary>("Health").SetValue("Health", .5f);
        Entity restored = project.CreateEntity(values);
        var restoredHealth = restored.FindComponent<ComponentHealth>(true);
        Check(restoredHealth.Health == .5f && restoredHealth.AttackResilience == maximum,
            template + " legacy half-health save keeps its fraction at the new maximum");
        restored.Dispose();
    }

    private static void WriteRoom(Terrain terrain, object site, Vector3 position)
    {
        Point3 p = Terrain.ToCell(position);
        var chunk = terrain.GetChunkAtCell(p.X, p.Z) ?? terrain.AllocateChunk(p.X >> 4, p.Z >> 4);
        chunk.State = TerrainChunkState.Valid;
        var layout = Create(layoutType, site, chunk.Origin);
        for (int y = p.Y - 1; y <= p.Y + 2; y++)
        {
            int value = (int)layoutType.GetMethod("At", Instance).Invoke(layout, new object[] { p.X & 15, y - 72, p.Z & 15 });
            Check(y < p.Y ? value > 0 : value == 0, "generated guardian floor and three-cell headroom " + p);
            terrain.SetCellValueFast(p.X, y, p.Z, value);
        }
    }

    private static void Decor()
    {
        var origin = new Point2(-80, -80);
        object site = Create(siteType, origin, 72, 0, 953, 68251, -3, true);
        var seen = new HashSet<int>();
        var layouts = new object[5, 5];
        var perLevel = new int[3];
        for (int sx = 0; sx < 5; sx++)
        for (int sz = 0; sz < 5; sz++)
        {
            object layout = Create(layoutType, site,
                new Point2(origin.X + sx * 16, origin.Y + sz * 16));
            layouts[sx, sz] = layout;
            var cells = (int[])layoutType.GetField("cells", Instance).GetValue(layout);
            for (int i = 0; i < cells.Length; i++)
            {
                int value = cells[i];
                if (Terrain.ExtractContents(value) != 886) continue;
                int y = -14 + i % 51;
                perLevel[y >= -2 ? 0 : y >= -8 ? 1 : 2]++;
                seen.Add(Terrain.ExtractData(value) & 31); // kind
            }
        }
        for (int kind = 0; kind < 9; kind++)
            Check(seen.Contains(kind), "Egyptian decor kind " + kind + " placed in the tomb layout");
        Check(seen.Count == 9, "all nine decor kinds appear (found " + seen.Count + ")");
        for (int level = 0; level < 3; level++)
            Check(perLevel[level] >= 48, "floor " + level + " carries a decor pass (" + perLevel[level] + " cells)");
        Console.WriteLine("Pyramid decor per floor: " + perLevel[0] + " / " + perLevel[1] + " / " + perLevel[2] + " cells.");

        Func<int, int, int, int> at = (x, y, z) =>
        {
            int sx = (x - origin.X) >> 4, sz = (z - origin.Y) >> 4;
            return (int)layoutType.GetMethod("At", Instance).Invoke(layouts[sx, sz],
                new object[] { x - origin.X - sx * 16, y, z - origin.Y - sz * 16 });
        };
        for (int level = 0; level < 3; level++)
        for (int cell = 0; cell < 64; cell++)
        {
            int x = 19 + 6 * (cell % 8), y = -6 * level, z = 20 + 6 * (cell / 8);
            Check(at(origin.X + x, y + 1, origin.Y + z) == 0,
                "decor leaves chamber centre " + x + "," + (y + 1) + "," + z + " clear");
        }

        // The older layout has no maze seed, but must still carry the same nine kinds.
        object legacy = Create(siteType, origin, 72, 0, 953);
        var legacySeen = new HashSet<int>();
        for (int sx = 0; sx < 5; sx++)
        for (int sz = 0; sz < 5; sz++)
        {
            object layout = Create(layoutType, legacy,
                new Point2(origin.X + sx * 16, origin.Y + sz * 16));
            var cells = (int[])layoutType.GetField("cells", Instance).GetValue(layout);
            foreach (int value in cells)
            {
                if (Terrain.ExtractContents(value) == 886)
                    legacySeen.Add(Terrain.ExtractData(value) & 31);
            }
        }
        for (int kind = 0; kind < 9; kind++)
            Check(legacySeen.Contains(kind), "legacy layout carries Egyptian decor kind " + kind);
    }

    private static object Create(Type type, params object[] args) { return Activator.CreateInstance(type, Instance, null, args, null); }
    private static object State(SubsystemBlockBehavior runtime)
    {
        return ((IDictionary)runtimeType.GetField("sites", Instance).GetValue(runtime))[new Point2(-1, -1)];
    }
    private static int Spawned(object state)
    {
        return (int)stateType.GetProperty("GuardiansSpawned", Instance).GetValue(state);
    }

    private static void Check(bool result, string failure)
    {
        checks++;
        if (!result) throw new InvalidOperationException("Pyramid guardians: " + failure);
    }
}
