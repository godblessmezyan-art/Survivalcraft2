using SurvivalcraftRuins.Core;

namespace SurvivalcraftRuins.Tests;

/// <summary>
/// Offline replica of Survivalcraft's electricity wiring rules, ported from the
/// game's own <c>SubsystemElectricity</c>. It answers one question: given the
/// blocks a builder emits, would the game consider a switch and a dispenser
/// part of the same circuit? Without this a trap can only be checked by hand,
/// and the 0.5 temples shipped with circuits that were never connected at all.
///
/// Only the blocks the mod places are modelled. Connection semantics follow the
/// game exactly: the 120-entry connection path table, the 36-entry connector
/// direction table, per-block connector types, and the wire-face merge rule.
/// </summary>
public static class ElectricModel
{
    public enum ConnectorType { Input, Output, InputOutput }

    private readonly record struct Path(int Dx, int Dy, int Dz, int NeighborFace, int ConnectorFace, int NeighborConnectorFace);

    // SubsystemElectricity.m_connectionPathsTable, 20 entries per mounting face
    // (5 connector directions x 4 alternatives, null-terminated per direction).
    private static readonly Path?[] s_paths = BuildPaths();

    // SubsystemElectricity.m_connectorDirectionsTable, 6 entries per mounting
    // face; null means "this connector face is the mounting face itself".
    private static readonly bool[] s_hasDirection =
    {
        false, true, true, true, true, true,
        true, false, true, true, true, true,
        true, true, false, true, true, true,
        true, true, true, false, true, true,
        true, true, true, true, false, true,
        true, true, true, true, true, false,
    };

    private static Path?[] BuildPaths()
    {
        // Transcribed from the game, grouped by mounting face.
        int[][] raw =
        {
            // mounting face 0
            new[] { 0, 1, -1, 4, 4, 0, 0, 1, 0, 0, 4, 5, 0, 1, -1, 2, 4, 5, 0, 0, 0, 5, 4, 2 },
            new[] { -1, 0, -1, 3, 3, 0, -1, 0, 0, 0, 3, 1, -1, 0, -1, 2, 3, 1, 0, 0, 0, 1, 3, 2 },
            new[] { 0, -1, -1, 5, 5, 0, 0, -1, 0, 0, 5, 4, 0, -1, -1, 2, 5, 4, 0, 0, 0, 4, 5, 2 },
            new[] { 1, 0, -1, 1, 1, 0, 1, 0, 0, 0, 1, 3, 1, 0, -1, 2, 1, 3, 0, 0, 0, 3, 1, 2 },
            new[] { 0, 0, -1, 2, 2, 0 },
            // mounting face 1
            new[] { -1, 1, 0, 4, 4, 1, 0, 1, 0, 1, 4, 5, -1, 1, 0, 3, 4, 5, 0, 0, 0, 5, 4, 3 },
            new[] { -1, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0, 2, -1, 0, 1, 3, 0, 2, 0, 0, 0, 2, 0, 3 },
            new[] { -1, -1, 0, 5, 5, 1, 0, -1, 0, 1, 5, 4, -1, -1, 0, 3, 5, 4, 0, 0, 0, 4, 5, 3 },
            new[] { -1, 0, -1, 2, 2, 1, 0, 0, -1, 1, 2, 0, -1, 0, -1, 3, 2, 0, 0, 0, 0, 0, 2, 3 },
            new[] { -1, 0, 0, 3, 3, 1 },
            // mounting face 2
            new[] { 0, 1, 1, 4, 4, 2, 0, 1, 0, 2, 4, 5, 0, 1, 1, 0, 4, 5, 0, 0, 0, 5, 4, 0 },
            new[] { 1, 0, 1, 1, 1, 2, 1, 0, 0, 2, 1, 3, 1, 0, 1, 0, 1, 3, 0, 0, 0, 3, 1, 0 },
            new[] { 0, -1, 1, 5, 5, 2, 0, -1, 0, 2, 5, 4, 0, -1, 1, 0, 5, 4, 0, 0, 0, 4, 5, 0 },
            new[] { -1, 0, 1, 3, 3, 2, -1, 0, 0, 2, 3, 1, -1, 0, 1, 0, 3, 1, 0, 0, 0, 1, 3, 0 },
            new[] { 0, 0, 1, 0, 0, 2 },
            // mounting face 3
            new[] { 1, 1, 0, 4, 4, 3, 0, 1, 0, 3, 4, 5, 1, 1, 0, 1, 4, 5, 0, 0, 0, 5, 4, 1 },
            new[] { 1, 0, -1, 2, 2, 3, 0, 0, -1, 3, 2, 0, 1, 0, -1, 1, 2, 0, 0, 0, 0, 0, 2, 1 },
            new[] { 1, -1, 0, 5, 5, 3, 0, -1, 0, 3, 5, 4, 1, -1, 0, 1, 5, 4, 0, 0, 0, 4, 5, 1 },
            new[] { 1, 0, 1, 0, 0, 3, 0, 0, 1, 3, 0, 2, 1, 0, 1, 1, 0, 2, 0, 0, 0, 2, 0, 1 },
            new[] { 1, 0, 0, 1, 1, 3 },
            // mounting face 4
            new[] { 0, -1, -1, 2, 2, 4, 0, 0, -1, 4, 2, 0, 0, -1, -1, 5, 2, 0, 0, 0, 0, 0, 2, 5 },
            new[] { -1, -1, 0, 3, 3, 4, -1, 0, 0, 4, 3, 1, -1, -1, 0, 5, 3, 1, 0, 0, 0, 1, 3, 5 },
            new[] { 0, -1, 1, 0, 0, 4, 0, 0, 1, 4, 0, 2, 0, -1, 1, 5, 0, 2, 0, 0, 0, 2, 0, 5 },
            new[] { 1, -1, 0, 1, 1, 4, 1, 0, 0, 4, 1, 3, 1, -1, 0, 5, 1, 3, 0, 0, 0, 3, 1, 5 },
            new[] { 0, -1, 0, 5, 5, 4 },
            // mounting face 5
            new[] { 0, 1, -1, 2, 2, 5, 0, 0, -1, 5, 2, 0, 0, 1, -1, 4, 2, 0, 0, 0, 0, 0, 2, 4 },
            new[] { 1, 1, 0, 1, 1, 5, 1, 0, 0, 5, 1, 3, 1, 1, 0, 4, 1, 3, 0, 0, 0, 3, 1, 4 },
            new[] { 0, 1, 1, 0, 0, 5, 0, 0, 1, 5, 0, 2, 0, 1, 1, 4, 0, 2, 0, 0, 0, 2, 0, 4 },
            new[] { -1, 1, 0, 3, 3, 5, -1, 0, 0, 5, 3, 1, -1, 1, 0, 4, 3, 1, 0, 0, 0, 1, 3, 4 },
            new[] { 0, 1, 0, 4, 4, 5 },
        };
        var paths = new Path?[120];
        for (int group = 0; group < raw.Length; group++)
        {
            int[] values = raw[group];
            for (int i = 0; i < 4; i++)
            {
                int at = i * 6;
                paths[group * 4 + i] = at + 5 < values.Length
                    ? new Path(values[at], values[at + 1], values[at + 2], values[at + 3], values[at + 4], values[at + 5])
                    : null;
            }
        }
        return paths;
    }

    public static int OppositeFace(int face) => face switch
    {
        0 => 2, 1 => 3, 2 => 0, 3 => 1, 4 => 5, _ => 4,
    };

    /// <summary>A block that participates in wire domains rather than owning an element.</summary>
    public static bool IsWire(string block) =>
        block == "WireBlock" || block.StartsWith("WireThrough", StringComparison.Ordinal);

    /// <summary>Mirrors each block's own GetConnectorType.</summary>
    public static ConnectorType? ConnectorTypeOf(string block, int data, int face, int connectorFace)
    {
        switch (block)
        {
            case "WireBlock":
                return (data & 0x3F & (1 << face)) != 0 ? ConnectorType.InputOutput : null;

            case var w when w.StartsWith("WireThrough", StringComparison.Ordinal):
            {
                int wired = WiredFace(data);
                return (face == wired || face == OppositeFace(wired)) && connectorFace == OppositeFace(face)
                    ? ConnectorType.InputOutput
                    : null;
            }

            // Mounted output devices: the element lives on one face only.
            case "PressurePlateBlock":
                return MountedConnector((data >> 1) & 7, face, connectorFace, ConnectorType.Output);
            case "SwitchBlock":
                return MountedConnector((data >> 1) & 7, face, connectorFace, ConnectorType.Output);
            case "ButtonBlock":
                return MountedConnector((data >> 1) & 7, face, connectorFace, ConnectorType.Output);

            // Mounted input devices.
            case "DetonatorBlock":
                return MountedConnector(data & 7, face, connectorFace, ConnectorType.Input);

            // Full cubes that take input from any side.
            case "DispenserBlock":
                return ConnectorType.Input;

            // Kegs only listen on their top face.
            case var keg when keg.EndsWith("GunpowderKegBlock", StringComparison.Ordinal):
                return face == 4 ? ConnectorType.Input : null;

            case "PistonBlock":
                return ConnectorType.Input;

            default:
                return null;
        }
    }

    private static ConnectorType? MountedConnector(int mountingFace, int face, int connectorFace, ConnectorType type)
    {
        if (face != mountingFace)
        {
            return null;
        }
        return s_hasDirection[6 * mountingFace + connectorFace] ? type : null;
    }

    /// <summary>WireThroughBlock.GetWiredFace: 0 -> +Z, 1 -> +X, anything else -> +Y.</summary>
    public static int WiredFace(int data) => (data & 3) switch { 0 => 0, 1 => 1, _ => 4 };

    /// <summary>PistonBlock packs the face it pushes toward in bits 3..5.</summary>
    public static int PistonFace(int data) => (data >> 3) & 7;

    /// <summary>The face a mounted device is attached to, or null for cube-like blocks.</summary>
    public static int? MountingFace(string block, int data) => block switch
    {
        "PressurePlateBlock" or "SwitchBlock" or "ButtonBlock" => (data >> 1) & 7,
        "DetonatorBlock" => data & 7,
        var keg when keg.EndsWith("GunpowderKegBlock", StringComparison.Ordinal) => 4,
        _ => null,
    };

    /// <summary>WireBlock.GetConnectedWireFacesMask — which faces in one cell share a domain.</summary>
    public static int ConnectedWireFacesMask(string block, int data, int face)
    {
        if (block.StartsWith("WireThrough", StringComparison.Ordinal))
        {
            int wired = WiredFace(data);
            return face == wired || face == OppositeFace(wired)
                ? (1 << wired) | (1 << OppositeFace(wired))
                : 0;
        }
        if (block != "WireBlock")
        {
            return 0;
        }
        int bitmask = data & 0x3F;
        if ((bitmask & (1 << face)) == 0)
        {
            return 0;
        }
        int opposite = OppositeFace(face);
        int result = 1 << face;
        bool any = false;
        for (int i = 0; i < 6; i++)
        {
            if (i != face && i != opposite && (bitmask & (1 << i)) != 0)
            {
                result |= 1 << i;
                any = true;
            }
        }
        if (any && (bitmask & (1 << opposite)) != 0)
        {
            result |= 1 << opposite;
        }
        return result;
    }

    /// <summary>Blocks whose element occupies all six faces of its cell.</summary>
    public static bool IsCubeDevice(string block) => block is "DispenserBlock" or "PistonBlock";

    /// <summary>
    /// The circuit graph of a structure: every element face gets a component id,
    /// and two devices can talk to each other exactly when their ids match.
    /// </summary>
    public sealed class Circuit
    {
        private readonly Dictionary<(int, int, int, int), int> m_ids = new();
        private readonly List<int> m_parent = new();
        public Dictionary<(int X, int Y, int Z), (string Block, int Data)> World { get; } = new();

        internal int Find((int, int, int, int) node)
        {
            if (!m_ids.TryGetValue(node, out int id))
            {
                m_ids[node] = id = m_parent.Count;
                m_parent.Add(id);
            }
            return Root(id);
        }

        private int Root(int id)
        {
            while (m_parent[id] != id)
            {
                m_parent[id] = m_parent[m_parent[id]];
                id = m_parent[id];
            }
            return id;
        }

        internal void Union((int, int, int, int) a, (int, int, int, int) b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra != rb)
            {
                m_parent[rb] = ra;
            }
        }

        private int? Component((int, int, int, int) node)
        {
            return m_ids.TryGetValue(node, out int id) ? Root(id) : null;
        }

        /// <summary>Component id of a device at a cell, or null if it has no element there.</summary>
        public int? ComponentAt(int x, int y, int z)
        {
            if (!World.TryGetValue((x, y, z), out var cell))
            {
                return null;
            }
            if (MountingFace(cell.Block, cell.Data) is { } face)
            {
                return Component((x, y, z, face));
            }
            return IsCubeDevice(cell.Block) ? Component((x, y, z, 0)) : null;
        }

        public bool Connected((int X, int Y, int Z) a, (int X, int Y, int Z) b)
        {
            int? ia = ComponentAt(a.X, a.Y, a.Z);
            int? ib = ComponentAt(b.X, b.Y, b.Z);
            return ia != null && ia == ib;
        }
    }

    /// <summary>Builds the circuit graph for everything a structure contains.</summary>
    public static Circuit Analyze(StructureData data)
    {
        var circuit = new Circuit();
        foreach (var cell in data.Cells)
        {
            var entry = data.Palette[cell.PaletteIndex];
            circuit.World[(cell.X, cell.Y, cell.Z)] = (entry.BlockName, entry.Data);
        }

        // Seed every face that carries an element.
        var faces = new List<(int X, int Y, int Z, int Face)>();
        foreach (var (position, cell) in circuit.World)
        {
            if (IsWire(cell.Block))
            {
                for (int face = 0; face < 6; face++)
                {
                    if (ConnectedWireFacesMask(cell.Block, cell.Data, face) != 0)
                    {
                        faces.Add((position.X, position.Y, position.Z, face));
                    }
                }
            }
            else if (MountingFace(cell.Block, cell.Data) is { } mounting)
            {
                faces.Add((position.X, position.Y, position.Z, mounting));
            }
            else if (IsCubeDevice(cell.Block))
            {
                for (int face = 0; face < 6; face++)
                {
                    faces.Add((position.X, position.Y, position.Z, face));
                }
            }
        }
        foreach (var f in faces)
        {
            circuit.Find((f.X, f.Y, f.Z, f.Face));
        }

        // A cube device is one element, so all six of its faces are one node.
        foreach (var (position, cell) in circuit.World)
        {
            if (IsCubeDevice(cell.Block))
            {
                for (int face = 1; face < 6; face++)
                {
                    circuit.Union((position.X, position.Y, position.Z, 0), (position.X, position.Y, position.Z, face));
                }
            }
        }

        // Wire faces sharing a cell merge per the game's own rule.
        foreach (var (position, cell) in circuit.World)
        {
            if (!IsWire(cell.Block))
            {
                continue;
            }
            for (int face = 0; face < 6; face++)
            {
                int mask = ConnectedWireFacesMask(cell.Block, cell.Data, face);
                for (int other = 0; other < 6; other++)
                {
                    if (other != face && (mask & (1 << other)) != 0)
                    {
                        circuit.Union((position.X, position.Y, position.Z, face), (position.X, position.Y, position.Z, other));
                    }
                }
            }
        }

        // Then the actual connections between cells.
        foreach (var f in faces)
        {
            foreach (var neighbor in ConnectedNeighbors(circuit.World, f.X, f.Y, f.Z, f.Face))
            {
                circuit.Union((f.X, f.Y, f.Z, f.Face), neighbor);
            }
        }
        return circuit;
    }

    /// <summary>SubsystemElectricity.GetAllConnectedNeighbors.</summary>
    public static List<(int X, int Y, int Z, int Face)> ConnectedNeighbors(
        IReadOnlyDictionary<(int, int, int), (string Block, int Data)> world, int x, int y, int z, int mountingFace)
    {
        var result = new List<(int, int, int, int)>();
        if (!world.TryGetValue((x, y, z), out var self))
        {
            return result;
        }
        for (int direction = 0; direction < 5; direction++)
        {
            for (int i = 0; i < 4; i++)
            {
                var path = s_paths[20 * mountingFace + 4 * direction + i];
                if (path == null)
                {
                    break;
                }
                var p = path.Value;
                var own = ConnectorTypeOf(self.Block, self.Data, mountingFace, p.ConnectorFace);
                if (own == null)
                {
                    break;
                }
                var key = (x + p.Dx, y + p.Dy, z + p.Dz);
                if (!world.TryGetValue(key, out var other))
                {
                    continue;
                }
                var otherType = ConnectorTypeOf(other.Block, other.Data, p.NeighborFace, p.NeighborConnectorFace);
                if (otherType == null)
                {
                    continue;
                }
                // Two inputs or two outputs never connect.
                bool compatible = (own.Value != ConnectorType.Input || otherType.Value != ConnectorType.Input)
                    && (own.Value != ConnectorType.Output || otherType.Value != ConnectorType.Output);
                if (compatible)
                {
                    result.Add((key.Item1, key.Item2, key.Item3, p.NeighborFace));
                }
            }
        }
        return result;
    }
}
