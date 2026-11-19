using Engine;
using Game;
using GameEntitySystem;
using SCIEPort.Features.Vehicles;

namespace SCIEPort.Features.AirVehicles
{
    internal static class HelicopterCockpit
    {
        public static void UpdateViews(Project project)
        {
            SubsystemGameWidgets views = project?.FindSubsystem<SubsystemGameWidgets>(false);
            if (views == null) return;

            // The native Views update has already run. Keep its FppCamera instance so camera
            // cycling, terrain visibility, control input and first-person body culling stay native.
            foreach (GameWidget widget in views.GameWidgets)
            {
                if (widget.ActiveCamera is FppCamera camera) UpdateView(camera);
            }
        }

        internal static void UpdateView(FppCamera camera)
        {
            ComponentHelicopterDrive aircraft = GetAircraft(camera, out VehicleSeatRole seat);
            if (aircraft == null) return;

            Vector2 look = camera.GameWidget.Target.ComponentLocomotion.LookAngles;
            Matrix view = GetEyePose(seat, look) * aircraft.CockpitTransform;
            camera.SetupPerspectiveCamera(view.Translation, view.Forward, view.Up);
        }

        internal static ComponentHelicopterDrive GetAircraft(Camera camera, out VehicleSeatRole seat)
        {
            seat = VehicleSeatRole.None;
            GameWidget widget = camera.GameWidget;
            if (!(camera is FppCamera) || camera != widget.ActiveCamera || camera.Eye.HasValue ||
                !(widget.Target is ComponentPlayer player) || player.ComponentInput?.IsControlledByVr != false)
            {
                return null;
            }

            ComponentHelicopterDrive aircraft = player.ComponentBody.ParentBody?.Entity.FindComponent<ComponentHelicopterDrive>(false);
            if (aircraft == null) return null;

            seat = aircraft.GetCockpitSeat(player);
            return seat != VehicleSeatRole.None ? aircraft : null;
        }

        private static Matrix GetEyePose(VehicleSeatRole seat, Vector2 look)
        {
            // Relative to the DAE VisualRoot, just in front of each seatback.
            Matrix eye = Matrix.CreateFromYawPitchRoll(-look.X, look.Y, 0f);
            eye.Translation = new Vector3(seat == VehicleSeatRole.Driver ? -0.42f : 0.42f, 0.33f, -0.08f);
            return eye;
        }
    }
}
