using System;
using Game;

namespace SCIEPort.Features.Tanks
{
    /// <summary>Raises the first collision of a tank shell before native rebound is applied.</summary>
    public sealed class SubsystemTankShellBlockBehavior : SubsystemBlockBehavior
    {
        /// <summary>Raised synchronously from the native projectile collision path.</summary>
        public event Action<Projectile, CellFace?, ComponentBody> ShellHit;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { TankShellBlock.Index };

        /// <inheritdoc />
        public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
        {
            Projectile projectile = worldItem as Projectile;
            if (projectile != null) ShellHit?.Invoke(projectile, cellFace, componentBody);
            // Always disintegrate this shell on its first terrain or body collision. The
            // owning tank component has already queued the explosion synchronously.
            return true;
        }
    }
}
