namespace SurvivalcraftRuins.Core;

public enum AnchorKind
{
    /// <summary>Flat-ish dry land; foundation columns are filled below the footprint.</summary>
    Land,
    /// <summary>On the beach/cliff line, close to open water (lighthouses).</summary>
    Coast,
    /// <summary>On the sea floor in shallow open water (shipwrecks).</summary>
    Offshore,
    /// <summary>On the sea floor in deep open water; sits on pillars driven down to the bed (monuments).</summary>
    DeepOcean,
    /// <summary>Floating on the ocean surface (ships).</summary>
    Ocean,
    /// <summary>Floating high above the terrain (sky dungeons).</summary>
    Sky,
}

/// <summary>
/// Inclusive extent of a structure in relative coordinates. Declaring it lets
/// placement reject a site before paying to realize the structure, which
/// matters for the big ones.
/// </summary>
public readonly record struct StructureBounds(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    public int SizeX => MaxX - MinX + 1;
    public int SizeY => MaxY - MinY + 1;
    public int SizeZ => MaxZ - MinZ + 1;

    public static StructureBounds From(StructureData data)
    {
        return new StructureBounds(data.MinX, data.MinY, data.MinZ, data.MaxX, data.MaxY, data.MaxZ);
    }
}

/// <summary>Per-block weathering applied deterministically at write time.</summary>
public sealed class WeatherProfile
{
    public static readonly WeatherProfile None = new() { DecayChance = 0f };

    /// <summary>Chance a non-air block is dropped entirely (collapse holes).</summary>
    public float DecayChance { get; init; }

    /// <summary>Chance a block listed in Swaps is replaced by its weathered version.</summary>
    public float SwapChance { get; init; }

    public IReadOnlyDictionary<string, string> Swaps { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Temperature/humidity window a structure needs, both on the game's 0..15
/// scale (see ITerrainContentsGenerator.CalculateTemperature/CalculateHumidity).
/// Deserts sit hot and dry, jungles hot and wet.
/// </summary>
public sealed class BiomeFilter
{
    // Windows measured against the game's own noise over a 36 km² world:
    // this desert covers ~14% of the map (3.6% of it dry land) and this jungle
    // ~14% (3.4% land). Humidity 6 is the widest dry window that stays barren —
    // PlantsManager gives oaks zero probability at humidity <= 6 and spruces
    // zero at temperature >= 10 — so temples still land in treeless country.
    public static readonly BiomeFilter Desert = new() { MinTemperature = 9, MaxHumidity = 6 };
    public static readonly BiomeFilter Jungle = new() { MinTemperature = 9, MinHumidity = 10 };

    public int MinTemperature { get; init; }
    public int MaxTemperature { get; init; } = 15;
    public int MinHumidity { get; init; }
    public int MaxHumidity { get; init; } = 15;

    public bool Matches(int temperature, int humidity)
    {
        return temperature >= MinTemperature && temperature <= MaxTemperature
            && humidity >= MinHumidity && humidity <= MaxHumidity;
    }
}

/// <summary>
/// Guards for a structure, budgeted by loot rather than by how many markers the
/// template happens to carry: the total scales with chest count and is then
/// spread over the available guard posts. Structures with no spec stay safe.
/// </summary>
public sealed class GuardSpec
{
    /// <summary>Entity template names from Database.xml, e.g. "Wolf_Gray". One is picked per guard.</summary>
    public required IReadOnlyList<string> Creatures { get; init; }

    /// <summary>Guards contributed by each chest in the structure.</summary>
    public float PerChest { get; init; } = 1f;

    /// <summary>Guards present regardless of chest count.</summary>
    public int BaseCount { get; init; }

    public required int MinTotal { get; init; }
    public required int MaxTotal { get; init; }

    /// <summary>Player distance that springs a post.</summary>
    public float TriggerRadius { get; init; } = 10f;

    /// <summary>Guards swim: spawn points look for water instead of air.</summary>
    public bool Aquatic { get; init; }

    /// <summary>
    /// Post guards at the structure's own ambush markers rather than at its
    /// chests. Standing with the loot is the right default, but not where the
    /// loot sits on a trap trigger: guards in the desert vault kept treading on
    /// the pressure plate and blowing the charge before the player arrived.
    /// </summary>
    public bool PostsAtMarkers { get; init; }

    /// <summary>
    /// Substitute used when the world has supernatural creatures disabled
    /// (werewolves). Null means the guard is simply skipped.
    /// </summary>
    public string? MundaneSubstitute { get; init; }

    public int TotalFor(int chestCount)
    {
        int total = (int)CompatMath.Round(chestCount * PerChest) + BaseCount;
        return CompatMath.Clamp(total, MinTotal, MaxTotal);
    }
}

/// <summary>
/// One concrete structure that can appear in a slot. All variants of a slot
/// share placement rules (grid, chance, anchor); everything else is per-variant.
/// </summary>
public sealed class StructureVariant
{
    public required string Name { get; init; }

    /// <summary>Realizes the structure. Must be deterministic for a given instance seed.</summary>
    public required Func<int, StructureData> Provider { get; init; }

    /// <summary>
    /// Loot pool names assigned to chests in template order (sorted by Y, then Z,
    /// then X). When there are more chests than entries the last entry repeats.
    /// </summary>
    public required IReadOnlyList<string> ChestPools { get; init; }

    /// <summary>Loot pool loaded into every dispenser (arrow traps); null leaves them empty.</summary>
    public string? DispenserPool { get; init; }

    /// <summary>
    /// Exact extent of everything Provider produces, when known ahead of time.
    /// Placement then tests the site first and only builds on a hit, which keeps
    /// a 58-block monument from being assembled once per candidate cell.
    /// Must match the built data exactly; CoreTests enforces that.
    /// </summary>
    public StructureBounds? Bounds { get; init; }

    public WeatherProfile Weather { get; init; } = WeatherProfile.None;

    /// <summary>Guards defending the loot (null leaves the structure unguarded).</summary>
    public GuardSpec? Guards { get; init; }

    /// <summary>How many blocks the structure origin sits below the anchor surface.</summary>
    public int SinkDepth { get; init; }

    /// <summary>Block used to fill foundation columns under Land/Coast structures.</summary>
    public string FoundationBlock { get; init; } = "DirtBlock";
}

/// <summary>
/// A placement slot: one candidate per grid cell, realized as one of several
/// variants picked by hash — variety without extra density.
/// </summary>
public sealed class StructureDef
{
    public required string Name { get; init; }

    /// <summary>Placement grid cell size in chunks; one candidate instance per cell.</summary>
    public required int GridChunks { get; init; }

    /// <summary>Chance [0..1] that a grid cell actually contains an instance.</summary>
    public required float Chance { get; init; }

    public required AnchorKind Anchor { get; init; }

    /// <summary>Max height difference across footprint corners for Land/Coast placement.</summary>
    public float MaxSlope { get; init; } = 4f;

    /// <summary>
    /// Keep-out radius in blocks, measured centre to centre against structures
    /// from higher-priority slots. The larger of the two radii wins, so a big
    /// landmark pushes small huts away without needing a matching radius there.
    /// </summary>
    public int SeparationRadius { get; init; } = 48;

    /// <summary>Temperature/humidity window; null places the structure in any biome.</summary>
    public BiomeFilter? Biome { get; init; }

    public required IReadOnlyList<StructureVariant> Variants { get; init; }

    public int Salt => DeterministicHash.StringSalt(Name);
}
