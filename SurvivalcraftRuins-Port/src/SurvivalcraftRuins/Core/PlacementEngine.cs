using System.Collections.Concurrent;

namespace SurvivalcraftRuins.Core;

/// <summary>Abstraction over the terrain generator so placement logic is testable.</summary>
public interface IWorldSampler
{
    int OceanLevel { get; }
    float Height(float x, float z);

    /// <summary>Biome temperature on the game's 0..15 scale.</summary>
    int Temperature(float x, float z);

    /// <summary>Biome humidity on the game's 0..15 scale.</summary>
    int Humidity(float x, float z);
}

public sealed class StructureInstance
{
    public required StructureDef Def { get; init; }
    public required StructureVariant Variant { get; init; }
    public required StructureData Data { get; init; }
    public required int OriginX { get; init; }
    public required int OriginY { get; init; }
    public required int OriginZ { get; init; }
    public required int InstanceSeed { get; init; }

    public int WorldMinX => OriginX + Data.MinX;
    public int WorldMaxX => OriginX + Data.MaxX;
    public int WorldMinZ => OriginZ + Data.MinZ;
    public int WorldMaxZ => OriginZ + Data.MaxZ;

    public int CenterX => (WorldMinX + WorldMaxX) / 2;
    public int CenterZ => (WorldMinZ + WorldMaxZ) / 2;

    public bool IntersectsColumnRange(int minX, int minZ, int maxX, int maxZ)
    {
        return WorldMinX <= maxX && WorldMaxX >= minX && WorldMinZ <= maxZ && WorldMaxZ >= minZ;
    }
}

/// <summary>
/// Deterministic structure placement. The world is divided per-def into square
/// grid cells (GridChunks chunks wide); each cell rolls one candidate instance
/// from a hash of (worldSeed, defSalt, cellX, cellZ), so any chunk can compute
/// every instance that touches it without neighbor chunks existing yet.
/// </summary>
public sealed class PlacementEngine
{
    /// <summary>Upper bound on horizontal structure extent, governs the neighborhood search.</summary>
    public const int MaxFootprint = 72;

    /// <summary>Upper bound on StructureDef.SeparationRadius, governs the keep-out search.</summary>
    public const int MaxSeparation = 160;

    /// <summary>
    /// Deterministic jitter positions tried per cell before the candidate is
    /// given up on. One attempt makes realized density a hostage of the terrain
    /// under a single point: measured against a real world, biome-gated slots
    /// lost 90%+ of their candidates to "that exact spot happened to be sea or
    /// a slope", which is why temples were near-impossible to find. Retrying
    /// inside the same cell keeps the one-per-cell density ceiling and the
    /// central-jitter separation guarantee.
    /// </summary>
    public const int DefaultSiteAttempts = 8;

    private readonly int m_seed;
    private readonly IWorldSampler m_sampler;
    private readonly IReadOnlyList<StructureDef> m_defs;
    private readonly float m_densityScale;
    private readonly int m_siteAttempts;
    private readonly ConcurrentDictionary<(int, int, int), StructureInstance?> m_cache = new();

    public PlacementEngine(
        int seed,
        IWorldSampler sampler,
        IReadOnlyList<StructureDef> defs,
        float densityScale = 1f,
        int siteAttempts = DefaultSiteAttempts)
    {
        m_seed = seed;
        m_sampler = sampler;
        m_defs = defs;
        m_densityScale = CompatMath.Clamp(densityScale, 0f, 4f);
        m_siteAttempts = CompatMath.Clamp(siteAttempts, 1, 32);
    }

    public List<StructureInstance> InstancesIntersecting(int minX, int minZ, int maxX, int maxZ)
    {
        var result = new List<StructureInstance>();
        for (int defIndex = 0; defIndex < m_defs.Count; defIndex++)
        {
            int cellSpan = m_defs[defIndex].GridChunks * 16;
            int cellMinX = FloorDiv(minX - MaxFootprint, cellSpan);
            int cellMaxX = FloorDiv(maxX + MaxFootprint, cellSpan);
            int cellMinZ = FloorDiv(minZ - MaxFootprint, cellSpan);
            int cellMaxZ = FloorDiv(maxZ + MaxFootprint, cellSpan);
            for (int cx = cellMinX; cx <= cellMaxX; cx++)
            {
                for (int cz = cellMinZ; cz <= cellMaxZ; cz++)
                {
                    var instance = GetInstance(defIndex, cx, cz);
                    if (instance != null && instance.IntersectsColumnRange(minX, minZ, maxX, maxZ))
                    {
                        result.Add(instance);
                    }
                }
            }
        }
        return result;
    }

    public StructureInstance? GetInstance(int defIndex, int cellX, int cellZ)
    {
        return m_cache.GetOrAdd((defIndex, cellX, cellZ), key => ComputeInstance(key.Item1, key.Item2, key.Item3));
    }

    private StructureInstance? ComputeInstance(int defIndex, int cellX, int cellZ)
    {
        var def = m_defs[defIndex];
        uint hash = DeterministicHash.Mix(m_seed, def.Salt, cellX, cellZ);
        if (DeterministicHash.ToUnitFloat(hash) >= def.Chance * m_densityScale)
        {
            return null;
        }
        int instanceSeed = (int)DeterministicHash.Mix(hash, 0x5EEDu);
        var variant = def.Variants[(int)(DeterministicHash.Mix(hash, 0x7A11u) % (uint)def.Variants.Count)];

        // Variants that declare their extent get sited before they are built,
        // so a rejected candidate costs nothing.
        StructureData? data = null;
        StructureBounds bounds;
        if (variant.Bounds is { } declared)
        {
            bounds = declared;
        }
        else
        {
            try
            {
                data = variant.Provider(instanceSeed);
            }
            catch (Exception)
            {
                return null;
            }
            bounds = StructureBounds.From(data);
        }

        // Jitter within the middle half of the cell only. Two instances of the
        // same slot in neighbouring cells then always keep at least a quarter
        // cell of clear ground between them, without either one having to know
        // the other exists (which would recurse).
        int cellSpan = def.GridChunks * 16;
        int rangeX = Math.Max(1, (cellSpan - bounds.SizeX) / 2);
        int rangeZ = Math.Max(1, (cellSpan - bounds.SizeZ) / 2);
        int marginX = Math.Max(1, (cellSpan - bounds.SizeX - rangeX) / 2);
        int marginZ = Math.Max(1, (cellSpan - bounds.SizeZ - rangeZ) / 2);

        // Attempt 0 uses the original salts, so a one-attempt engine places
        // exactly what earlier versions did (pre-0.6 worlds rely on that).
        int ox = 0, oz = 0, oy = 0;
        bool sited = false;
        for (int attempt = 0; attempt < m_siteAttempts && !sited; attempt++)
        {
            uint spin = attempt == 0 ? hash : DeterministicHash.Mix(hash, 0x51E0u + (uint)attempt);
            ox = cellX * cellSpan + marginX - bounds.MinX + (int)(DeterministicHash.Mix(spin, 11u) % (uint)rangeX);
            oz = cellZ * cellSpan + marginZ - bounds.MinZ + (int)(DeterministicHash.Mix(spin, 13u) % (uint)rangeZ);

            if (def.Biome != null)
            {
                float bx = ox + (bounds.MinX + bounds.MaxX) * 0.5f;
                float bz = oz + (bounds.MinZ + bounds.MaxZ) * 0.5f;
                if (!def.Biome.Matches(m_sampler.Temperature(bx, bz), m_sampler.Humidity(bx, bz)))
                {
                    continue;
                }
            }

            int? anchor = ComputeAnchorY(def, variant, bounds, ox, oz);
            if (anchor != null)
            {
                oy = anchor.Value;
                sited = true;
            }
        }
        if (!sited)
        {
            return null;
        }

        if (data == null)
        {
            try
            {
                data = variant.Provider(instanceSeed);
            }
            catch (Exception)
            {
                return null;
            }
        }

        var instance = new StructureInstance
        {
            Def = def,
            Variant = variant,
            Data = data,
            OriginX = ox,
            OriginY = oy,
            OriginZ = oz,
            InstanceSeed = instanceSeed,
        };

        // Higher-priority defs (lower index) win on overlap and on the keep-out
        // radius; suppression is deterministic because it only ever consults
        // strictly lower indices, so the dependency graph stays acyclic.
        for (int higher = 0; higher < defIndex; higher++)
        {
            int span = m_defs[higher].GridChunks * 16;
            int reach = MaxFootprint + Math.Max(def.SeparationRadius, m_defs[higher].SeparationRadius);
            int hMinX = FloorDiv(instance.WorldMinX - reach, span);
            int hMaxX = FloorDiv(instance.WorldMaxX + reach, span);
            int hMinZ = FloorDiv(instance.WorldMinZ - reach, span);
            int hMaxZ = FloorDiv(instance.WorldMaxZ + reach, span);
            for (int cx = hMinX; cx <= hMaxX; cx++)
            {
                for (int cz = hMinZ; cz <= hMaxZ; cz++)
                {
                    var other = GetInstance(higher, cx, cz);
                    if (other == null)
                    {
                        continue;
                    }
                    if (other.IntersectsColumnRange(
                            instance.WorldMinX, instance.WorldMinZ, instance.WorldMaxX, instance.WorldMaxZ))
                    {
                        return null;
                    }
                    int keepOut = Math.Max(def.SeparationRadius, other.Def.SeparationRadius);
                    long dx = instance.CenterX - other.CenterX;
                    long dz = instance.CenterZ - other.CenterZ;
                    if (dx * dx + dz * dz < (long)keepOut * keepOut)
                    {
                        return null;
                    }
                }
            }
        }
        return instance;
    }

    private int? ComputeAnchorY(StructureDef def, StructureVariant variant, StructureBounds data, int ox, int oz)
    {
        float x0 = ox + data.MinX, x1 = ox + data.MaxX;
        float z0 = oz + data.MinZ, z1 = oz + data.MaxZ;
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        float hCenter = m_sampler.Height(cx, cz);
        float h00 = m_sampler.Height(x0, z0);
        float h01 = m_sampler.Height(x0, z1);
        float h10 = m_sampler.Height(x1, z0);
        float h11 = m_sampler.Height(x1, z1);
        float hMin = CompatMath.Min(CompatMath.Min(h00, h01), CompatMath.Min(h10, h11));
        float hMax = CompatMath.Max(CompatMath.Max(h00, h01), CompatMath.Max(h10, h11));
        int ocean = m_sampler.OceanLevel;

        int oy;
        switch (def.Anchor)
        {
            case AnchorKind.Land:
                if (hMin <= ocean + 1 || hMax - hMin > def.MaxSlope)
                {
                    return null;
                }
                oy = (int)CompatMath.Round(hCenter) + 1 - variant.SinkDepth;
                break;

            case AnchorKind.Coast:
            {
                if (hCenter <= ocean || hCenter > ocean + 4 || hMax - hMin > def.MaxSlope)
                {
                    return null;
                }
                // Require open water nearby so the tower actually faces the sea.
                bool nearWater = false;
                for (int i = 0; i < 8 && !nearWater; i++)
                {
                    float angle = i * CompatMath.Pi / 4f;
                    float sx = cx + CompatMath.Cos(angle) * 20f;
                    float sz = cz + CompatMath.Sin(angle) * 20f;
                    if (m_sampler.Height(sx, sz) < ocean - 2)
                    {
                        nearWater = true;
                    }
                }
                if (!nearWater)
                {
                    return null;
                }
                oy = (int)CompatMath.Round(hCenter) + 1 - variant.SinkDepth;
                break;
            }

            case AnchorKind.Ocean:
            {
                // Floating hull: open water at least 4 deep across the footprint.
                if (ocean - hMax < 4f)
                {
                    return null;
                }
                oy = ocean + 1 - variant.SinkDepth;
                break;
            }

            case AnchorKind.Sky:
            {
                // Hover 16-30 blocks above the tallest terrain under the footprint.
                float ground = CompatMath.Max(hMax, ocean);
                uint variation = DeterministicHash.Mix((uint)ox, (uint)oz) % 15u;
                oy = (int)ground + 16 + (int)variation;
                int ceiling = 250 - data.MaxY;
                if (oy > ceiling)
                {
                    oy = ceiling;
                }
                if (oy < (int)ground + 8)
                {
                    return null;
                }
                break;
            }

            case AnchorKind.Offshore:
            {
                float depthCenter = ocean - hCenter;
                float depthShallowest = ocean - hMax;
                if (depthCenter < 4f || depthCenter > 14f || depthShallowest < 2f)
                {
                    return null;
                }
                oy = (int)CompatMath.Round(hCenter) + 1 - variant.SinkDepth;
                break;
            }

            case AnchorKind.DeepOcean:
            {
                // Deep water across the whole footprint. The roof hangs just
                // below the surface and the structure drives its own pillars
                // down, so an uneven bed is fine as long as it is deep enough.
                if (ocean - hMax < 18f)
                {
                    return null;
                }
                oy = ocean - data.MaxY - 3 - variant.SinkDepth;
                break;
            }

            default:
                return null;
        }

        if (oy + data.MinY < 2 || oy + data.MaxY > 250)
        {
            return null;
        }
        return oy;
    }

    public static int FloorDiv(int a, int b)
    {
        int q = a / b;
        if (a % b != 0 && (a < 0) != (b < 0))
        {
            q--;
        }
        return q;
    }
}
