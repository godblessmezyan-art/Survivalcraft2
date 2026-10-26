using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Engine;
using Game;

namespace SCIEPort.Features.PortableFortress
{
    /// <summary>Detail level for the fortress presentation pass.</summary>
    public enum FortressQuality
    {
        /// <summary>Android low tier: fewer glass panes and no glow sprites.</summary>
        Low = 0,
        /// <summary>Android mid tier.</summary>
        Medium = 1,
        /// <summary>Desktop and high-end Android.</summary>
        High = 2
    }

    /// <summary>
    /// Runtime switches for the Portable Fortress. Both values are persisted through
    /// <c>SCIEPortLoader.SaveSettings</c> so an Android build can be flipped without a rebuild.
    /// Diagnostics is off by default: the tracing below runs on every tick while enabled.
    /// </summary>
    internal static class FortressOptions
    {
        /// <summary>Logs the entrance walkthrough trace. Off in normal play.</summary>
        public static bool Diagnostics;

        /// <summary>Presentation tier; drives the glazing and glow budget.</summary>
        public static FortressQuality Quality = FortressQuality.High;

        /// <summary>True when the given tier wants an effect.</summary>
        public static bool Allow(FortressQuality minimum)
        {
            return Quality >= minimum;
        }
    }

    /// <summary>
    /// Entrance / traversal tracer. It reports what the engine actually did to the player's feet:
    /// world and local position, speed, which proxy is under or against the body, and whether the
    /// bounded rise assist fired. It never changes movement; it only observes.
    /// </summary>
    internal static class FortressDiagnostics
    {
        private const double TraceInterval = 0.10;

        private static readonly Dictionary<string, double> s_lastTrace = new Dictionary<string, double>();

        /// <summary>
        /// Writes one trace line for a player inside or near a fortress. Silent unless
        /// <see cref="FortressOptions.Diagnostics"/> is set.
        /// </summary>
        public static void TracePlayer(
            string instanceId,
            FortressRecord record,
            ComponentBody body,
            float localX,
            float localZ,
            bool assistEngaged,
            float targetOffset,
            double now)
        {
            if (!FortressOptions.Diagnostics || record == null || body == null)
            {
                return;
            }

            double last;
            if (s_lastTrace.TryGetValue(instanceId, out last) && now - last < TraceInterval)
            {
                return;
            }

            s_lastTrace[instanceId] = now;

            Vector3 position = body.Position;
            float feetOffset = position.Y - record.BaseY;
            int underIndex;
            int againstIndex;
            DescribeProximity(localX, localZ, feetOffset, out underIndex, out againstIndex);
            string zone = ZoneName(localX, localZ, feetOffset);

            var text = new StringBuilder(160);
            text.Append("[SCIEPort Fortress] enter zone=").Append(zone);
            text.Append(" world=").Append(F3(position.X)).Append(',').Append(F3(position.Y)).Append(',').Append(F3(position.Z));
            text.Append(" local=").Append(F3(localX)).Append(',').Append(F3(localZ));
            text.Append(" feet=").Append(F3(feetOffset));
            text.Append(" vy=").Append(F3(body.Velocity.Y));
            text.Append(" under=").Append(underIndex < 0 ? "none" : underIndex + ":" + PortableFortressGeometry.Segments[underIndex].Name);
            text.Append(" against=").Append(againstIndex < 0 ? "none" : againstIndex + ":" + PortableFortressGeometry.Segments[againstIndex].Name);
            text.Append(" assist=").Append(assistEngaged ? "raise->" + F3(targetOffset) : "none");
            Log.Information(text.ToString());
        }

        /// <summary>Forgets the throttle state; called when an instance disappears.</summary>
        public static void Forget(string instanceId)
        {
            if (!string.IsNullOrEmpty(instanceId))
            {
                s_lastTrace.Remove(instanceId);
            }
        }

        private static string ZoneName(float localX, float localZ, float feetOffset)
        {
            if (localZ >= PortableFortressGeometry.EntrySillMinZ)
            {
                return "entrance";
            }

            if (localZ >= PortableFortressGeometry.StairMinZ && localZ <= PortableFortressGeometry.StairMaxZ
                && localX >= PortableFortressGeometry.StairMinX && localX <= PortableFortressGeometry.StairMaxX)
            {
                return "stair";
            }

            if (localX >= PortableFortressGeometry.ShaftMinX && localX <= PortableFortressGeometry.ShaftMaxX
                && localZ >= PortableFortressGeometry.ShaftMinZ && localZ <= PortableFortressGeometry.ShaftMaxZ)
            {
                return "shaft";
            }

            if (feetOffset > PortableFortressGeometry.DeckTop + 0.1f)
            {
                return feetOffset > PortableFortressGeometry.RoofTop - 0.1f ? "roof" : "level2";
            }

            return "level1";
        }

        /// <summary>
        /// Finds the proxy directly under the feet and the first proxy the body would collide with
        /// horizontally. Both are searched over the frozen segment table, index = segment index.
        /// </summary>
        private static void DescribeProximity(float localX, float localZ, float feetOffset, out int underIndex, out int againstIndex)
        {
            underIndex = -1;
            againstIndex = -1;
            float bestTop = float.NegativeInfinity;
            float bestDepth = float.PositiveInfinity;

            IReadOnlyList<FortressSegment> segments = PortableFortressGeometry.Segments;
            for (int i = 0; i < segments.Count; i++)
            {
                FortressBox box = segments[i].Box;
                if (localX < box.MinX - 0.30f || localX > box.MaxX + 0.30f || localZ < box.MinZ - 0.30f || localZ > box.MaxZ + 0.30f)
                {
                    continue;
                }

                bool insideFootprint = localX >= box.MinX && localX <= box.MaxX && localZ >= box.MinZ && localZ <= box.MaxZ;
                if (insideFootprint && box.MaxY <= feetOffset + 0.06f && box.MaxY > bestTop)
                {
                    bestTop = box.MaxY;
                    underIndex = i;
                }

                // A body lip: the top is above the feet but the box still overlaps the stance band.
                if (insideFootprint && box.MaxY > feetOffset + 0.06f && box.MinY < feetOffset + 1.75f)
                {
                    float depth = box.MaxY - feetOffset;
                    if (depth < bestDepth)
                    {
                        bestDepth = depth;
                        againstIndex = i;
                    }
                }
            }
        }

        private static string F3(float value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
