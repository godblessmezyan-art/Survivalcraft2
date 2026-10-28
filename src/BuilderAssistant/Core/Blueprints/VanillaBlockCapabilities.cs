using System;
using System.Collections.Generic;
using BuilderAssistant.Core.Building;

namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>How an authorable vanilla block behaves beyond its CLR type name.</summary>
    internal enum BlockCapabilityKind
    {
        Static = 0,
        Oriented = 1,
        Connected = 2,
        DerivedMultiCell = 3,
        Attached = 4,
    }

    /// <summary>
    /// Game-free metadata for one audited vanilla block. Runtime registration still has
    /// to find the exact CLR type in BlocksManager and verify its base family before the
    /// capability becomes active.
    /// </summary>
    internal sealed class VanillaBlockCapability
    {
        public readonly string TypeName;
        public readonly BlockFamily Family;
        public readonly BlockCapabilityKind Kind;
        public readonly bool SupportsPaint;
        public bool CanPlace { get { return true; } }
        public bool CanCopy { get { return true; } }
        public bool CanRotate { get { return true; } }
        public bool CanMirror { get { return true; } }
        public bool CanSaveState { get { return true; } }
        // Exact terrain restoration only. Arbitrary external subsystem effects are not claimed.
        public bool CanUndoTerrain { get { return Family != BlockFamily.Door && Family != BlockFamily.Stairs && Family != BlockFamily.Fence; } }
        public bool CanUndoAllSubsystems { get { return Kind == BlockCapabilityKind.Static &&
            TypeName != "SpruceLeavesBlock" && TypeName != "TallSpruceLeavesBlock" && TypeName != "WickerLampBlock"; } }

        public bool IsValidData(int data)
        {
            return BlockOrientation.IsValidAuthoredData(Family, data) &&
                (Family != BlockFamily.Cube || SupportsPaint || data == 0);
        }

        public bool TryDecodeTerrainData(int data, out int authored)
        {
            authored = data;
            return IsValidData(data);
        }

        public VanillaBlockCapability(string typeName, BlockFamily family, BlockCapabilityKind kind)
        {
            TypeName = typeName;
            Family = family;
            Kind = kind;
            SupportsPaint = family == BlockFamily.Stairs || family == BlockFamily.Slab || family == BlockFamily.Fence ||
                typeName == "CobblestoneBlock" || typeName == "StoneBrickBlock" || typeName == "PlanksBlock" ||
                typeName == "BricksBlock" || typeName == "BasaltBlock" || typeName == "GraniteBlock" ||
                typeName == "MarbleBlock" || typeName == "SandstoneBlock" || typeName == "ClayBlock";
        }
    }

    /// <summary>
    /// Audited authoring allow-list for the x25.01.17 public Windows assembly. This is
    /// deliberately not a list of every Block subclass: only blocks whose terrain DATA
    /// can be reproduced by a registered family codec appear here. The live adapter turns
    /// this metadata into a runtime registry after checking BlocksManager.
    /// </summary>
    internal static class VanillaBlockCapabilities
    {
        private static readonly Dictionary<string, VanillaBlockCapability> ByTypeName = Build();

        public static IEnumerable<VanillaBlockCapability> All
        {
            get { return ByTypeName.Values; }
        }

        public static int Count
        {
            get { return ByTypeName.Count; }
        }

        public static bool TryGetByTypeName(string typeName, out VanillaBlockCapability capability)
        {
            return ByTypeName.TryGetValue(typeName ?? string.Empty, out capability);
        }

        /// <summary>Resolve either a concrete CLR type name or a logical generator key.</summary>
        public static bool TryGetByMaterialKey(
            string materialKey,
            out VanillaBlockCapability capability,
            out string concreteTypeName)
        {
            capability = null;
            concreteTypeName = null;
            if (string.IsNullOrEmpty(materialKey))
            {
                return false;
            }

            if (ByTypeName.TryGetValue(materialKey, out capability))
            {
                concreteTypeName = materialKey;
                return true;
            }

            MaterialCatalog.Entry entry;
            if (MaterialCatalog.TryGet(materialKey, out entry) &&
                ByTypeName.TryGetValue(entry.TypeName, out capability))
            {
                concreteTypeName = entry.TypeName;
                return true;
            }
            return false;
        }

        private static Dictionary<string, VanillaBlockCapability> Build()
        {
            var d = new Dictionary<string, VanillaBlockCapability>(StringComparer.Ordinal);

            // Cube DATA is type-qualified; only the nine audited PaintedCube subclasses accept paint.
            AddMany(d, BlockFamily.Cube, BlockCapabilityKind.Static, new[]
            {
                "CobblestoneBlock", "StoneBrickBlock", "PlanksBlock", "BricksBlock",
                "GlassBlock", "BasaltBlock", "GraniteBlock", "MarbleBlock",
                "SandstoneBlock", "ClayBlock", "LimestoneBlock", "MalachiteBlock",
                "CoalBlock", "CoalOreBlock", "CopperBlock", "CopperOreBlock",
                "DiamondBlock", "DiamondOreBlock", "GermaniumOreBlock", "IronBlock",
                "IronOreBlock", "SaltpeterOreBlock", "SemiconductorBlock",
                "SulphurOreBlock", "FramedGlassBlock", "WindowBlock", "WickerLampBlock",
                "SpruceLeavesBlock", "TallSpruceLeavesBlock",
            });

            AddMany(d, BlockFamily.Stairs, BlockCapabilityKind.Oriented, new[]
            {
                "WoodenStairsBlock", "CobblestoneStairsBlock", "StoneStairsBlock",
                "StoneBrickStairsBlock", "BrickStairsBlock", "BasaltStairsBlock",
                "MarbleStairsBlock", "SandstoneStairsBlock", "DirtStairsBlock",
            });

            AddMany(d, BlockFamily.Slab, BlockCapabilityKind.Oriented, new[]
            {
                "WoodenSlabBlock", "CobblestoneSlabBlock", "StoneSlabBlock",
                "StoneBrickSlabBlock", "BrickSlabBlock", "BasaltSlabBlock",
                "MarbleSlabBlock", "SandstoneSlabBlock", "DirtSlabBlock",
            });

            AddMany(d, BlockFamily.Fence, BlockCapabilityKind.Connected, new[]
            {
                "WoodenFenceBlock", "StoneFenceBlock", "BrickFenceBlock",
                "BasaltFenceBlock", "IronFenceBlock",
            });

            AddMany(d, BlockFamily.Door, BlockCapabilityKind.DerivedMultiCell, new[]
            {
                "WoodenDoorBlock", "IronDoorBlock", "CellDoorBlock",
            });

            // WoodBlock SetCutFace emits 0=Y, 1=Z, 2=X; alias 3 is never authored.
            AddMany(d, BlockFamily.Log, BlockCapabilityKind.Oriented, new[]
            {
                "OakWoodBlock", "BirchWoodBlock", "SpruceWoodBlock",
                "MimosaWoodBlock", "PoplarWoodBlock",
            });

            AddMany(d, BlockFamily.Ladder, BlockCapabilityKind.Attached, new[]
            {
                "WoodenLadderBlock", "IronLadderBlock",
            });
            // Torch particle lifecycle is paired by the native added/removed behavior.
            AddMany(d, BlockFamily.Torch, BlockCapabilityKind.Attached, new[] { "TorchBlock" });
            return d;
        }

        private static void AddMany(
            Dictionary<string, VanillaBlockCapability> target,
            BlockFamily family,
            BlockCapabilityKind kind,
            string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                target.Add(name, new VanillaBlockCapability(name, family, kind));
            }
        }
    }
}
