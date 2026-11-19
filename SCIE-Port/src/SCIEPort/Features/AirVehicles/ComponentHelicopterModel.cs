using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.AirVehicles
{
    /// <summary>Draws dark exterior panes and opens the glass only for the occupant's cockpit view.</summary>
    public sealed class ComponentHelicopterModel : ComponentSimpleModel
    {
        private ModelBone m_canopyGlass;
        private SubsystemModelsRenderer m_renderer;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_renderer = Project.FindSubsystem<SubsystemModelsRenderer>(true);
        }

        /// <inheritdoc />
        public override void SetModel(Model model)
        {
            base.SetModel(model);
            m_canopyGlass = Model?.FindBone("CanopyGlass", throwIfNotFound: false);
        }

        /// <inheritdoc />
        public override void CalculateIsVisible(Camera camera)
        {
            base.CalculateIsVisible(camera);
            bool inside = GetViewedAircraft(camera) != null;
            if (inside) IsVisibleForCamera = true;

            // The renderer calls this before calculating the bones for each camera, including split views.
            if (m_canopyGlass != null)
                SetBoneTransform(m_canopyGlass.Index, inside ? Matrix.CreateScale(0f) : (Matrix?)null);
        }

        /// <inheritdoc />
        public override void DrawExtras(Camera camera)
        {
            base.DrawExtras(camera);
            ComponentHelicopterDrive aircraft = GetViewedAircraft(camera);
            if (aircraft == null) return;

            Matrix transform = aircraft.CockpitTransform * camera.ViewMatrix;
            Vector3 readings = aircraft.CockpitGaugeReadings;
            FlatBatch3D batch = m_renderer.PrimitivesRenderer.FlatBatch(
                0, DepthStencilState.DepthRead, RasterizerState.CullNoneScissor, BlendState.Opaque);
            foreach (float x in SeatColumns)
            {
                QueueNeedle(batch, transform, x - 0.145f, readings.X, new Color(105, 235, 234));
                QueueNeedle(batch, transform, x, readings.Y, new Color(245, 202, 111));
                QueueNeedle(batch, transform, x + 0.145f, readings.Z, new Color(169, 236, 157));
            }
        }

        private static readonly float[] SeatColumns = { -0.42f, 0.42f };

        private ComponentHelicopterDrive GetViewedAircraft(Camera camera)
        {
            ComponentHelicopterDrive aircraft = HelicopterCockpit.GetAircraft(camera, out _);
            return aircraft != null && aircraft.Entity == Entity ? aircraft : null;
        }

        private static void QueueNeedle(FlatBatch3D batch, Matrix transform, float x, float reading, Color color)
        {
            // These coordinates are the dial faces in New-HelicopterAssets.ps1, relative to VisualRoot.
            Vector3 center = new Vector3(x, 0.0864f, -0.6408f);
            float angle = MathUtils.PI * (1.25f - 1.5f * MathUtils.Saturate(reading));
            float horizontal = MathUtils.Cos(angle);
            float vertical = MathUtils.Sin(angle);
            Vector3 up = new Vector3(0f, 0.8f, -0.6f);
            Vector3 needle = 0.047f * (horizontal * Vector3.UnitX + vertical * up);
            Vector3 width = 0.005f * (-vertical * Vector3.UnitX + horizontal * up);
            batch.QueueTriangle(
                Vector3.Transform(center + needle, transform),
                Vector3.Transform(center + width, transform),
                Vector3.Transform(center - width, transform), color);
        }
    }
}
