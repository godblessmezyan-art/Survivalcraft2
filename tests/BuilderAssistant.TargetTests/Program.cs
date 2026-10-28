using System;
using System.IO;
using System.Reflection;
using BuilderAssistant.Core.Blueprints;
using Game;

internal static class TargetTests
{
    private static int _checks;
    private static void Check(bool pass, string name)
    {
        _checks++;
        if (!pass) throw new InvalidOperationException(name);
    }
    private static int Main(string[] args)
    {
        string gameDir = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            string path = Path.Combine(gameDir, name == "Survivalcraft" ? "Survivalcraft.exe" : name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        try { Run(); Console.WriteLine("Target static API: " + _checks + " checks passed; no game process or world loaded."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    // Keep target-referencing JIT work after installing the assembly resolver.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Run()
    {
        foreach (VanillaBlockCapability capability in VanillaBlockCapabilities.All)
        {
            Type type = typeof(Block).Assembly.GetType("Game." + capability.TypeName,true);
            Type expected;
            switch (capability.Family)
            {
                case BlockFamily.Cube: expected = typeof(CubeBlock); break;
                case BlockFamily.Stairs: expected = typeof(StairsBlock); break;
                case BlockFamily.Slab: expected = typeof(SlabBlock); break;
                case BlockFamily.Fence: expected = typeof(FenceBlock); break;
                case BlockFamily.Door: expected = typeof(DoorBlock); break;
                case BlockFamily.Log: expected = typeof(WoodBlock); break;
                case BlockFamily.Ladder: expected = typeof(LadderBlock); break;
                case BlockFamily.Torch: expected = typeof(TorchBlock); break;
                default: throw new InvalidOperationException("Unexpected family");
            }
            Check(expected.IsAssignableFrom(type) && type.GetField("Index").IsLiteral, "registry runtime family/index " + capability.TypeName);
            if (capability.Family == BlockFamily.Cube)
                Check(capability.SupportsPaint == typeof(PaintedCubeBlock).IsAssignableFrom(type), "registry paint qualification " + capability.TypeName);
        }
        for (int color = -1; color < 16; color++)
        {
            int? c = color == -1 ? (int?)null : color;
            int encoded, geometry; int? decodedColor;
            Check(BlockOrientation.TryEncodePaint(BlockFamily.Cube,0,c,out encoded) &&
                BlockOrientation.TryDecodePaint(BlockFamily.Cube,encoded,out geometry,out decodedColor) &&
                geometry == 0 && decodedColor == c && encoded == PaintedCubeBlock.SetColor(0,c), "production cube paint encoder and decoder");
            int cube = PaintedCubeBlock.SetColor(0, c);
            Check(cube == (c.HasValue ? 1 + 2 * color : 0) && PaintedCubeBlock.GetColor(cube) == c &&
                BlockOrientation.IsValidAuthoredData(BlockFamily.Cube,cube), "PaintedCube paint bits");
            for (int d = 0; d < 24; d++)
            {
                int actual = StairsBlock.SetColor(d,c);
                Check(BlockOrientation.TryEncodePaint(BlockFamily.Stairs,d,c,out encoded) && encoded == actual &&
                    BlockOrientation.TryDecodePaint(BlockFamily.Stairs,actual,out geometry,out decodedColor) &&
                    geometry == d && decodedColor == c, "production stair paint encoder and decoder");
                Check(actual == d + (c.HasValue ? 32 + 64 * color : 0) && StairsBlock.GetColor(actual) == c &&
                    StairsBlock.GetRotation(actual) == (d & 3) && BlockOrientation.IsValidAuthoredData(BlockFamily.Stairs,actual), "Stairs paint and geometry");
            }
            for (int d = 0; d < 2; d++)
            {
                int actual = SlabBlock.SetColor(d,c);
                Check(BlockOrientation.TryEncodePaint(BlockFamily.Slab,d,c,out encoded) && encoded == actual &&
                    BlockOrientation.TryDecodePaint(BlockFamily.Slab,actual,out geometry,out decodedColor) &&
                    geometry == d && decodedColor == c, "production slab paint encoder and decoder");
                Check(actual == d + (c.HasValue ? 2 + 4 * color : 0) && SlabBlock.GetColor(actual) == c &&
                    SlabBlock.GetIsTop(actual) == (d == 1), "Slab paint bits");
            }
            for (int d = 0; d < 16; d++)
            {
                int actual = FenceBlock.SetColor(d,c);
                Check(BlockOrientation.TryEncodePaint(BlockFamily.Fence,d,c,out encoded) && encoded == actual &&
                    BlockOrientation.TryDecodePaint(BlockFamily.Fence,actual,out geometry,out decodedColor) &&
                    geometry == d && decodedColor == c, "production fence paint encoder and decoder");
                Check(actual == d + (c.HasValue ? 16 + 32 * color : 0) && FenceBlock.GetColor(actual) == c &&
                    FenceBlock.GetVariant(actual) == d, "Fence paint bits");
                Check(FenceGateBlock.SetColor(d,c) == actual && FenceGateBlock.GetColor(actual) == c, "Gate paint bits");
            }
        }
        int[] cutFaces = { 4,0,1 };
        for (int axis = 0; axis < 3; axis++)
        {
            Check(WoodBlock.GetCutFace(axis) == cutFaces[axis], "log axis GetCutFace");
            Check(WoodBlock.SetCutFace(0,cutFaces[axis]) == axis, "log axis SetCutFace");
            var oldVector = CellFace.FaceToPoint3(cutFaces[axis]);
            var newVector = CellFace.FaceToPoint3(WoodBlock.GetCutFace(BlockOrientation.Rotate(BlockFamily.Log,axis,1)));
            Check(Math.Abs(newVector.X) == Math.Abs(oldVector.Z) && Math.Abs(newVector.Z) == Math.Abs(oldVector.X), "geometric log axis quarter turn");
        }
        for (int face = 0; face < 4; face++)
        {
            Check(LadderBlock.GetFace(LadderBlock.SetFace(0,face)) == face, "ladder face bits");
            Check(IvyBlock.GetFace(IvyBlock.SetFace(0,face)) == face, "ivy face bits");
            var old = CellFace.FaceToPoint3(face);
            var rotated = CellFace.FaceToPoint3(BlockOrientation.Rotate(BlockFamily.Ladder,face,1));
            var mirrored = CellFace.FaceToPoint3(BlockOrientation.MirrorX(BlockFamily.Ladder,face));
            Check(rotated.X == -old.Z && rotated.Z == old.X, "face rotation follows actual CellFace vectors");
            Check(mirrored.X == -old.X && mirrored.Z == old.Z, "face reflection follows actual CellFace vectors");
        }
        for (int d = 0; d < 16; d++)
        {
            Check(TrapdoorBlock.GetRotation(d) == (d & 3) && TrapdoorBlock.GetOpen(d) == ((d & 4) != 0) &&
                TrapdoorBlock.GetUpsideDown(d) == ((d & 8) != 0), "trapdoor state bits");
            Check(FenceGateBlock.GetRotation(d) == (d & 3) && FenceGateBlock.GetOpen(d) == ((d & 4) != 0) &&
                FenceGateBlock.GetRightHanded(d) == ((d & 8) == 0), "gate state bits and inverse handedness");
            Check(CarpetBlock.GetColor(d) == d && CarpetBlock.SetColor(0,d) == d, "carpet color bits");
        }
        // The native fixed classes add no DATA accessor/state; do not invent window orientation.
        Check(typeof(WindowBlock).BaseType == typeof(AlphaTestCubeBlock) && typeof(FramedGlassBlock).BaseType == typeof(AlphaTestCubeBlock), "window and framed-glass base ABI");
        foreach (string type in new[] { "WoodenLadderBlock", "IronLadderBlock", "WoodenTrapdoorBlock", "CellTrapdoorBlock", "WoodenFenceGateBlock", "IronFenceGateBlock", "TorchBlock", "CarpetBlock", "IvyBlock" })
            Check(typeof(Block).Assembly.GetType("Game." + type,true).GetField("Index").IsLiteral, "concrete type/index " + type);
    }
}
