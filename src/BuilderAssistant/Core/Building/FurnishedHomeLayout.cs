using System.Collections.Generic;
using BuilderAssistant.Core.Furniture;

namespace BuilderAssistant.Core.Building
{
    /// <summary>Deterministic two-room placement recipe; all coordinates are blueprint-relative.</summary>
    internal sealed class FurnishedHomeLayout
    {
        public readonly int Width, Length, PartitionZ;
        public readonly CellPos Entrance, LivingTarget, BedroomTarget;
        public readonly IReadOnlyList<FurniturePlacement> Furniture;
        // One two-block-high approach cell per furniture bounding box, in the same order.
        public readonly IReadOnlyList<CellPos> Access;

        private FurnishedHomeLayout(int width,int length,int partition,bool mirrored)
        {
            Width=width; Length=length; PartitionZ=partition;
            Entrance=new CellPos((width-1)/2,1,1);
            LivingTarget=new CellPos(Entrance.X,1,partition-1);
            BedroomTarget=new CellPos(Entrance.X,1,partition+1);
            var furniture=new List<FurniturePlacement>(); var access=new List<CellPos>();
            Add(furniture,access,"ReadingDesk_A",1,1,0,new CellPos(1,1,2),mirrored);
            Add(furniture,access,"WoodChair_A",2,2,2,new CellPos(3,1,3),mirrored);
            Add(furniture,access,"Bench_A",width-4,1,0,new CellPos(width-3,1,3),mirrored);
            Add(furniture,access,"LampStand_A",width-2,partition-1,0,new CellPos(width-3,1,partition-1),mirrored);
            Add(furniture,access,"Bed_A",1,length-4,2,new CellPos(3,1,length-3),mirrored);
            Add(furniture,access,"Cabinet_A",width-3,length-2,0,new CellPos(width-3,1,length-3),mirrored);
            Add(furniture,access,"TallBookshelf_A",width-2,partition+1,1,new CellPos(width-3,1,partition+2),mirrored);
            Furniture=furniture.AsReadOnly(); Access=access.AsReadOnly();
        }

        private void Add(List<FurniturePlacement> items,List<CellPos> access,string id,int x,int z,int rotation,CellPos approach,bool mirror)
        {
            FurnitureTemplate template; FurnitureTemplateRegistry.Default.TryGet(id,out template);
            int sx=(rotation&1)==0?template.SizeX:template.SizeZ;
            if(mirror) { x=Width-x-sx;approach=new CellPos(Width-1-approach.X,approach.Y,approach.Z); }
            items.Add(new FurniturePlacement(id,new CellPos(x,1,z),rotation,mirrorX:mirror));
            access.Add(approach);
        }

        public static FurnishedHomeLayout Create(BuildingSpec spec)
        {
            var rng=new DeterministicRandom(spec.Seed);
            // At least five clear interior rows on either side of the partition.
            int partition=6+rng.NextInt(spec.Length-12);
            return new FurnishedHomeLayout(spec.Width,spec.Length,partition,rng.NextInt(2)==1);
        }
    }
}
