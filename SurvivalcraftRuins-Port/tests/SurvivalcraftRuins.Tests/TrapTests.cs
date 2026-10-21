using SurvivalcraftRuins.Builders;
using SurvivalcraftRuins.Core;
using Xunit;
using Xunit.Abstractions;

namespace SurvivalcraftRuins.Tests;

/// <summary>
/// The trap circuits, checked against a port of the game's own wiring rules
/// (see <see cref="ElectricModel"/>). Play testing found both temples shipped
/// with dead circuits, and that failure is invisible in the block list: every
/// block was the right block, they simply were not connected to each other.
/// </summary>
public class TrapTests
{
    private readonly ITestOutputHelper m_output;

    public TrapTests(ITestOutputHelper output)
    {
        m_output = output;
    }

    private static (int X, int Y, int Z) FaceOffset(int face) => face switch
    {
        0 => (0, 0, 1),
        1 => (1, 0, 0),
        2 => (0, 0, -1),
        3 => (-1, 0, 0),
        4 => (0, 1, 0),
        _ => (0, -1, 0),
    };

    private ElectricModel.Circuit Analyze(StructureData data)
    {
        var circuit = ElectricModel.Analyze(data);
        foreach (var (position, cell) in circuit.World.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.Z).ThenBy(p => p.Key.X))
        {
            if (ElectricModel.MountingFace(cell.Block, cell.Data) != null || ElectricModel.IsCubeDevice(cell.Block))
            {
                m_output.WriteLine($"{cell.Block,-24} ({position.X},{position.Y},{position.Z}) data {cell.Data}"
                    + $" -> circuit {circuit.ComponentAt(position.X, position.Y, position.Z)}");
            }
        }
        return circuit;
    }

    [Fact]
    public void DesertPlateFiresTheCharge()
    {
        var data = DesertTempleBuilder.Build(1);
        var circuit = Analyze(data);
        Assert.True(circuit.Connected((10, -11, 10), (10, -13, 10)),
            "the floor plate must reach the detonator through the wire-through slab");

        // The detonator only pops itself; the kegs go up because they touch it.
        int touching = 0;
        for (int face = 0; face < 6; face++)
        {
            var (dx, dy, dz) = FaceOffset(face);
            if (circuit.World.TryGetValue((10 + dx, -13 + dy, 10 + dz), out var neighbor)
                && neighbor.Block.EndsWith("GunpowderKegBlock", StringComparison.Ordinal))
            {
                touching++;
            }
        }
        Assert.True(touching >= 4, $"detonator should be packed in kegs, touches {touching}");
    }

    [Fact]
    public void JungleTrapPlatesReachTheirDispensers()
    {
        var data = JungleTempleBuilder.Build(1);
        var circuit = Analyze(data);

        (int, int, int)[] westPlates = { (2, -3, 8), (3, -3, 8) };
        foreach (var plate in westPlates)
        {
            Assert.True(circuit.Connected(plate, (2, -3, 0)), $"west plate {plate} is not wired to its dispenser");
        }

        (int, int, int)[] eastPlates = { (7, -3, 3), (7, -3, 4), (7, -3, 5) };
        foreach (var plate in eastPlates)
        {
            Assert.True(circuit.Connected(plate, (10, -3, 4)), $"east plate {plate} is not wired to its dispenser");
        }
    }

    /// <summary>A dispenser aimed into a wall shoots the wall.</summary>
    [Fact]
    public void JungleDispensersFaceOpenSpace()
    {
        var data = JungleTempleBuilder.Build(1);
        var circuit = ElectricModel.Analyze(data);
        foreach (var (position, cell) in circuit.World)
        {
            if (cell.Block != "DispenserBlock")
            {
                continue;
            }
            Assert.True((cell.Data & 8) != 0, $"dispenser at {position} is not in shoot mode");
            var (dx, dy, dz) = FaceOffset(cell.Data & 7);
            var target = (position.X + dx, position.Y + dy, position.Z + dz);
            // Wire and plates lie flat on the floor and do not stop a bolt.
            bool clear = !circuit.World.TryGetValue(target, out var ahead)
                || ahead.Block is "AirBlock" or "WireBlock" or "PressurePlateBlock" or "IvyBlock";
            Assert.True(clear, $"dispenser at {position} fires into {ahead.Block} at {target}");
        }
    }

    /// <summary>
    /// Exactly one lever is live; the other two are the puzzle. Both door
    /// pistons have to be on that lever's circuit or the door only half opens.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99)]
    [InlineData(2026)]
    public void JungleLeverPuzzleHasOneLiveLever(int seed)
    {
        var data = JungleTempleBuilder.Build(seed);
        var circuit = ElectricModel.Analyze(data);
        int? pistons = circuit.ComponentAt(7, -3, 11);
        Assert.NotNull(pistons);
        Assert.Equal(pistons, circuit.ComponentAt(7, -2, 11));

        int live = 0;
        for (int x = 8; x <= 10; x++)
        {
            if (circuit.ComponentAt(x, -2, 13) == pistons)
            {
                live++;
            }
        }
        Assert.Equal(1, live);
    }

    /// <summary>
    /// Every wire has to lie against a solid block. A wire whose support is
    /// missing still conducts, but it hangs in mid air and reads as a bug.
    /// </summary>
    [Theory]
    [InlineData("desert")]
    [InlineData("jungle")]
    public void WiresLieAgainstSolidBlocks(string which)
    {
        var data = which == "desert" ? DesertTempleBuilder.Build(1) : JungleTempleBuilder.Build(1);
        var circuit = ElectricModel.Analyze(data);
        foreach (var (position, cell) in circuit.World)
        {
            if (cell.Block != "WireBlock")
            {
                continue;
            }
            for (int face = 0; face < 6; face++)
            {
                if ((cell.Data & 0x3F & (1 << face)) == 0)
                {
                    continue;
                }
                var (dx, dy, dz) = FaceOffset(face);
                var support = (position.X - dx, position.Y - dy, position.Z - dz);
                Assert.True(circuit.World.TryGetValue(support, out var block), $"wire at {position} face {face} has nothing at {support}");
                Assert.False(block.Block is "AirBlock" or "WireBlock", $"wire at {position} face {face} clings to {block.Block}");
            }
        }
    }

    /// <summary>
    /// If the pistons misbehave in game the hidden chest must still be
    /// reachable, so the panel they slide aside is an ordinary diggable block.
    /// </summary>
    [Fact]
    public void HiddenNichePanelCanBeDugOut()
    {
        var circuit = ElectricModel.Analyze(JungleTempleBuilder.Build(1));
        for (int y = -3; y <= -2; y++)
        {
            Assert.Equal("StoneBrickBlock", circuit.World[(9, y, 11)].Block);
            Assert.Equal("AirBlock", circuit.World[(8, y, 11)].Block);
            Assert.Equal("AirBlock", circuit.World[(10, y, 11)].Block);
        }
    }

    /// <summary>
    /// The wrong levers must not quietly share the door's circuit, and the
    /// trapped one has to reach its own dispenser: the point of the puzzle is
    /// that pulling the wrong lever does something visible.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99)]
    [InlineData(2026)]
    public void JungleTrappedLeverIsSeparateFromTheDoor(int seed)
    {
        var data = JungleTempleBuilder.Build(seed);
        var circuit = ElectricModel.Analyze(data);
        int? door = circuit.ComponentAt(7, -3, 11);

        int trapped = 0;
        foreach (var (position, cell) in circuit.World)
        {
            if (cell.Block != "DispenserBlock" || position.Z != 14)
            {
                continue;
            }
            trapped++;
            int? dispenser = circuit.ComponentAt(position.X, position.Y, position.Z);
            Assert.NotEqual(door, dispenser);
            Assert.Equal(dispenser, circuit.ComponentAt(position.X, -2, 13));
        }
        Assert.Equal(1, trapped);
    }

    /// <summary>
    /// Guards must not be posted on top of a trigger. A jaguar pacing over the
    /// desert plate blew the charge before the player was anywhere near it.
    /// </summary>
    [Theory]
    [InlineData("desert")]
    [InlineData("jungle")]
    public void GuardsAreNotPostedOnTraps(string which)
    {
        var data = which == "desert" ? DesertTempleBuilder.Build(1) : JungleTempleBuilder.Build(1);
        var variant = TestRegistry.Defs()
            .SelectMany(d => d.Variants)
            .Single(v => v.Name == which + "_temple");
        var posts = GuardPlanner.Plan(variant, data);
        Assert.NotEmpty(posts);

        var triggers = new List<(int X, int Y, int Z)>();
        foreach (var cell in data.Cells)
        {
            if (data.Palette[cell.PaletteIndex].BlockName == "PressurePlateBlock")
            {
                triggers.Add((cell.X, cell.Y, cell.Z));
            }
        }
        Assert.NotEmpty(triggers);
        foreach (var post in posts)
        {
            foreach (var trigger in triggers)
            {
                int dx = post.X - trigger.X, dy = post.Y - trigger.Y, dz = post.Z - trigger.Z;
                // Either a different storey, or far enough that a guard would
                // have to cross the whole room to reach the trigger.
                Assert.True(Math.Abs(dy) >= 2 || dx * dx + dz * dz > 16,
                    $"guard post ({post.X},{post.Y},{post.Z}) can wander onto the trigger at {trigger}");
            }
        }
    }
}
