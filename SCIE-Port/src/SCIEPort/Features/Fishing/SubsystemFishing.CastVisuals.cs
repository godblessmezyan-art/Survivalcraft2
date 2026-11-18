#pragma warning disable CS1591
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Fishing
{
    public sealed partial class SubsystemFishing
    {
        private const float FollowThroughDuration = 0.48f;

        private void ApplyCastView(ComponentPlayer player, View view, FishingCatchPackage packet, double now)
        {
            if (view.Kind != packet.Kind) view.PhaseAt = now;
            view.Kind = packet.Kind;
            view.Seen = now;
            if (packet.Kind == FishingCatchPackage.FlightOrigin)
                view.Flight.Origin = FishingCastArc.Unpack(packet.Cell);
            else if (packet.Kind == FishingCatchPackage.Flight)
            {
                view.Flight.Velocity = FishingCastArc.Unpack(packet.Cell);
                view.Flight.Duration = packet.Count * 0.01f;
                view.FlightAt = now;
                view.CastForce = view.Flight.Force;
                if (now - view.CastAt > FollowThroughDuration) view.CastAt = now;
                m_audio?.PlaySound("Audio/Throw", 0.45f, MathUtils.Lerp(0.1f, -0.14f, view.CastForce),
                    player.ComponentCreatureModel.EyePosition, 8f, false);
            }
        }

        private static void ApplyRodPose(ComponentPlayer player, View view, double now)
        {
            bool pullingBack = view.Kind == FishingCatchPackage.Windup && (view.Drawing || !IsLocal(player));
            float releasedFor = (float)(now - view.CastAt);
            if ((!pullingBack && releasedFor >= FollowThroughDuration)
                || Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue) != FishingRodBlock.Index)
                return;

            float load = FishingCastArc.Charge(now - view.PhaseAt);
            float raise;
            float pitch;
            float side;
            if (pullingBack)
            {
                raise = Smooth((float)(now - view.PhaseAt) / 0.24f) * MathUtils.Lerp(0.64f, 1f, load);
                pitch = 1.08f * raise;
                side = 0.18f * raise;
                if (view.Drawing && view.Aim.LengthSquared() > 0f)
                    player.ComponentAimingSights?.ShowAimingSights(player.ComponentCreatureModel.EyePosition, view.Aim);
            }
            else
            {
                float strength = MathUtils.Lerp(0.65f, 1f, view.CastForce);
                // Sweep forward hard, then recover with one small spring-back.
                float flick = Smooth(releasedFor / 0.10f);
                float recovery = Smooth((releasedFor - 0.10f) / (FollowThroughDuration - 0.10f));
                float rebound = MathUtils.Sin(recovery * MathUtils.PI) * 0.22f;
                pitch = (MathUtils.Lerp(0.96f, -0.72f, flick) * (1f - recovery) + rebound) * strength;
                raise = MathUtils.Lerp(0.8f, -0.22f, flick) * (1f - recovery) * strength;
                side = MathUtils.Lerp(0.18f, -0.2f, flick) * (1f - recovery) * strength;
            }

            ComponentCreatureModel model = player.ComponentCreatureModel;
            model.AimHandAngleOrder = raise * 3f;
            model.InHandItemRotationOrder = new Vector3(raise * 2.45f, -side * 0.35f, 0);
            model.InHandItemOffsetOrder = new Vector3(0, -0.18f * raise, 0);
            var firstPerson = player.Entity.FindComponent<ComponentFirstPersonModel>(false);
            if (firstPerson == null) return;
            firstPerson.ItemOffsetOrder = new Vector3(0.18f * side, 0.15f * raise, 0.07f * raise);
            firstPerson.ItemRotationOrder = new Vector3(pitch, 0.15f * side, -side);
        }

        private static float Smooth(float t)
        {
            t = MathUtils.Saturate(t);
            return t * t * (3f - 2f * t);
        }

        private static void DrawPreview(FlatBatch3D batch, FishingCastArc arc, bool fishable)
        {
            if (arc.Duration <= 0.07f) return;
            Color color = fishable ? new Color(181, 237, 220, 185) : new Color(244, 188, 127, 170);
            const int steps = 20;
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                Vector3 start = arc.At(arc.Duration * t);
                Vector3 end = arc.At(arc.Duration * (t + 0.024f));
                batch.QueueLine(start, end, color);
            }

            if (!fishable) return;
            Vector3 center = arc.End + Vector3.UnitY * 0.027f;
            const int edges = 16;
            const float radius = 0.25f;
            for (int i = 0; i < edges; i++)
            {
                float a = i * (2f * MathUtils.PI / edges);
                float b = (i + 1) * (2f * MathUtils.PI / edges);
                batch.QueueLine(center + new Vector3(MathUtils.Cos(a), 0, MathUtils.Sin(a)) * radius,
                    center + new Vector3(MathUtils.Cos(b), 0, MathUtils.Sin(b)) * radius, color);
            }
        }

        private static void DrawFlight(Camera camera, FlatBatch3D batch, ComponentPlayer player, View view, double now)
        {
            float time = MathUtils.Clamp((float)(now - view.FlightAt), 0f, view.Flight.Duration);
            Vector3 tip = RodTip(camera, player);
            Vector3 bobber = view.Flight.At(time);
            float sag = MathUtils.Min(0.65f, Vector3.Distance(tip, bobber) * 0.055f);
            var line = new Color(238, 231, 197, 218);
            Vector3 previous = tip;
            for (int i = 1; i <= 6; i++)
            {
                float part = i / 6f;
                Vector3 point = Vector3.Lerp(tip, bobber, part) - Vector3.UnitY * (4f * part * (1f - part) * sag);
                batch.QueueLine(previous, point, line);
                previous = point;
            }
            DrawBobber(camera, batch, bobber, 0.5f * MathUtils.Sin(time * 16f));
        }

        private static void DrawBobber(Camera camera, FlatBatch3D batch, Vector3 position, float roll = 0f)
        {
            Vector3 right = Vector3.Cross(Vector3.UnitY, camera.ViewPosition - position);
            right = right.LengthSquared() > 0.001f ? Vector3.Normalize(right) : Vector3.UnitX;
            float sin = MathUtils.Sin(roll);
            float cos = MathUtils.Cos(roll);
            Vector3 up = Vector3.UnitY * cos + right * sin;
            Vector3 side = (right * cos - Vector3.UnitY * sin) * 0.062f;
            BobberBand(batch, position, side, up, -0.045f, 0.036f, new Color(244, 223, 182));
            BobberBand(batch, position, side, up, 0.036f, 0.12f, new Color(215, 70, 49));
        }

        private static Vector3 RodTip(Camera camera, ComponentPlayer player)
        {
            ComponentCreatureModel model = player.ComponentCreatureModel;
            Block rod = BlocksManager.Blocks[FishingRodBlock.Index];
            int value = Terrain.MakeBlockValue(FishingRodBlock.Index);
            Vector3 eye = model.EyePosition;
            if (camera.GameWidget.IsEntityFirstPersonTarget(player.Entity))
            {
                var firstPerson = player.Entity.FindComponent<ComponentFirstPersonModel>(false);
                if (firstPerson != null && Terrain.ExtractContents(firstPerson.m_value) == FishingRodBlock.Index)
                {
                    // Draw order 1 has already produced the animated native model transform for this camera.
                    Matrix world = firstPerson.m_drawBlockEnvironmentData.InWorldMatrix;
                    if (Vector3.DistanceSquared(eye, world.Translation) < 6f)
                        return Vector3.Transform(FishingRodBlock.ModelTip * rod.GetFirstPersonScale(value), world);
                }
            }
            else if (model is ComponentHumanModel human &&
                     Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue) == FishingRodBlock.Index)
            {
                Matrix world = human.m_drawBlockEnvironmentData.InWorldMatrix;
                if (Vector3.DistanceSquared(eye, world.Translation) < 8f)
                    return Vector3.Transform(FishingRodBlock.ModelTip * rod.InHandScale, world);
            }

            Vector3 rotation = rod.GetFirstPersonRotation(value) * (MathUtils.PI / 180f);
            Matrix hand = Matrix.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z);
            Vector3 end = Vector3.Transform(FishingRodBlock.ModelTip * rod.GetFirstPersonScale(value), hand)
                + rod.GetFirstPersonOffset(value);
            return eye + Vector3.Transform(end, model.EyeRotation);
        }
    }
}
