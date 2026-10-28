namespace BuilderAssistant.Core.Furniture
{
    internal sealed class FurniturePlacement
    {
        public string TemplateId { get; private set; }
        public CellPos Position { get; private set; }
        // Geometry turns: 1 maps (dx,dz) to (-dz,dx), independently of block DATA.
        public int Rotation { get; private set; }
        public bool MirrorX { get; private set; }
        public string Variant { get; private set; }
        public string Color { get; private set; }

        public FurniturePlacement(string templateId, CellPos position, int rotation = 0,
            string variant = "default", string color = "natural", bool mirrorX = false)
        {
            TemplateId = templateId; Position = position; Rotation = rotation;
            Variant = variant; Color = color; MirrorX = mirrorX;
        }
    }
}
