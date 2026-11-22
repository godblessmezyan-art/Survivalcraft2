#pragma warning disable CS1591

using System;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Geology
{
    public sealed class SubsystemSaltFlatShimmer : Subsystem, IDrawable
    {
        private static readonly int[] Orders = { 24 };
        private const int Radius = 10;
        private const int MaxQuads = 320;
        private SubsystemTerrain terrain;
        private SubsystemWeather weather;
        private SubsystemTimeOfDay timeOfDay;
        private SubsystemSky sky;
        private PrimitivesRenderer3D renderer;

        public int[] DrawOrders => Orders;

        public override void Load(ValuesDictionary valuesDictionary)
        {
            terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            weather = Project.FindSubsystem<SubsystemWeather>(true);
            timeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true);
            sky = Project.FindSubsystem<SubsystemSky>(true);
        }

        public void Draw(Camera camera, int drawOrder)
        {
            float rain = weather.PrecipitationIntensity;
            if (rain <= .06f || !(BlocksManager.Blocks[SaltCrustBlock.Index] is SaltCrustBlock)) return;
            Terrain map = terrain.Terrain;
            int cx = Terrain.ToCell(camera.ViewPosition.X), cz = Terrain.ToCell(camera.ViewPosition.Z);
            FlatBatch3D batch = null;
            Color reflected = Color.White;
            int tiles = 0;
            Vector3 direction = camera.ViewDirection;
            direction.Y = Math.Abs(direction.Y) + .025f;

            for (int z = cz - Radius; z <= cz + Radius && tiles < MaxQuads; z++)
            for (int x = cx - Radius; x <= cx + Radius && tiles < MaxQuads; x++)
            {
                int distance = (x - cx) * (x - cx) + (z - cz) * (z - cz);
                if (distance >= Radius * Radius) continue;
                TerrainChunk chunk = map.GetChunkAtCell(x, z);
                if (chunk == null || chunk.State < TerrainChunkState.Valid) continue;
                int y = chunk.GetTopHeightFast(x & 15, z & 15);
                if (y <= 0 || y >= 254 || y + 1f >= camera.ViewPosition.Y ||
                    map.GetCellContents(x, y, z) != SaltCrustBlock.Index || map.GetCellContents(x, y + 1, z) != 0) continue;
                int temperature = map.GetSeasonalTemperature(x, z);
                if (SubsystemWeather.IsPlaceFrozen(temperature, y)) continue;

                if (batch == null)
                {
                    if (renderer == null) renderer = new PrimitivesRenderer3D();
                    batch = renderer.FlatBatch(0, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.NonPremultiplied);
                    reflected = sky.CalculateSkyColor(direction, timeOfDay.TimeOfDay, rain, temperature);
                }

                float fade = MathUtils.Saturate((1f - distance / (float)(Radius * Radius)) * 3f);
                float grazing = 1f - MathUtils.Saturate(Math.Abs(camera.ViewPosition.Y - y - 1f) / (2f + (float)Math.Sqrt(distance)));
                Color sheen = reflected;
                sheen.A = (byte)(fade * rain * (47f + 96f * grazing));
                float height = y + 1.008f;
                batch.QueueQuad(new Vector3(x, height, z), new Vector3(x + 1, height, z),
                    new Vector3(x + 1, height, z + 1), new Vector3(x, height, z + 1), sheen);
                tiles++;
            }
            if (batch != null) renderer.Flush(camera.ViewProjectionMatrix);
        }
    }
}
