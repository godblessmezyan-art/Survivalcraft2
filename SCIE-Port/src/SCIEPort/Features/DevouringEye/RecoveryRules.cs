using System;
using System.Collections.Generic;

namespace SCIEPort.Features.DevouringEye
{
    /// <summary>Explicit lossless V1 mappings; no catch-all based on AutoDespawn or block ID ranges.</summary>
    public static class RecoveryRules
    {
        /// <summary>Only template admitted by EntityRecovery V1.</summary>
        public const string TankTemplate = "SCIEPortT34Tank";
        /// <summary>Exact project-owned tank template identity.</summary>
        public static readonly Guid TankGuid = new Guid("54904f9f-93bc-4e54-9a7d-4e0a8d3e4d5a");
        private static readonly HashSet<string> Painted = new HashSet<string> { "GraniteBlock", "CobblestoneBlock", "PlanksBlock", "SandstoneBlock", "MarbleBlock", "BasaltBlock", "BricksBlock", "ClayBlock" };
        private static readonly HashSet<string> Wood = new HashSet<string> { "OakWoodBlock", "BirchWoodBlock", "SpruceWoodBlock", "MimosaWoodBlock", "PoplarWoodBlock" };

        /// <summary>Checks only known data bits; callers additionally require exact native assembly identity.</summary>
        public static bool BlockMapped(string type, int data)
        {
            if (Painted.Contains(type)) return data == 0 || (data >= 1 && data <= 31 && (data & 1) != 0);
            if (Wood.Contains(type)) return data >= 0 && data <= 2;
            return (type == "DirtBlock" || type == "GlassBlock") && data == 0;
        }

        /// <summary>Coordinate budget, not a claim about a server's protected-region configuration.</summary>
        public static bool InBounds(int x, int y, int z) => x > -1000000 && x < 1000000 && z > -1000000 && z < 1000000 && y >= 1 && y <= 254;

        /// <summary>V1 has no remote-client command channel or implicit permission for other local players.</summary>
        public static bool AllowExecutor(bool client, bool main, bool server, bool administrator) => !client && main && (!server || administrator);

        /// <summary>Creative inventory virtual catalogue entries and occupied slots are never treated as capacity.</summary>
        public static bool CanFit(bool creative, int beforeCount, int beforeValue, int output, int capacity)
            => beforeCount < capacity && (creative ? beforeCount == 0 : beforeCount == 0 || beforeValue == output);

        /// <summary>Unknown/forbidden foreground objects always occlude a legal object behind them.</summary>
        public static int Nearest(float terrain, float entity, float moving)
        {
            if (moving <= terrain && moving <= entity) return 2;
            return entity <= terrain ? 1 : 0;
        }
    }

    /// <summary>Transaction adapter; all methods execute on the game thread.</summary>
    public interface IRecoverySteps
    {
        /// <summary>Checks authority, identity, passengers, region, data and inventory space.</summary>
        void Validate();
        /// <summary>Persists a before-image and a verified native save checkpoint.</summary>
        void Prepare();
        /// <summary>Removes exactly one source and verifies absence.</summary>
        void Remove();
        /// <summary>Grants and verifies exactly one result (or one creative slot value).</summary>
        void Grant();
        /// <summary>Saves and verifies the native world, then seals the journal.</summary>
        void Commit();
        /// <summary>Restores both inventory and source; failure must lock further operations.</summary>
        void Rollback();
    }

    /// <summary>Single-flight transaction. A failed verification never advances to the next stage.</summary>
    public sealed class RecoveryTransaction
    {
        /// <summary>True while executing one operation.</summary>
        public bool Busy { get; private set; }
        /// <summary>Uncertain rollback disables this world session.</summary>
        public bool Locked { get; private set; }

        /// <summary>Returns a user-readable failure or null on success; never retries or auto-grants.</summary>
        public string Execute(IRecoverySteps steps)
        {
            if (Busy || Locked) return "回收忙碌或已锁定，请检查恢复日志。";
            Busy = true;
            bool prepared = false;
            try
            {
                steps.Validate();
                steps.Prepare();
                prepared = true;
                steps.Remove();
                steps.Grant();
                steps.Commit();
                return null;
            }
            catch (Exception error)
            {
                if (prepared)
                {
                    try { steps.Rollback(); }
                    catch (Exception rollback) { Locked = true; return "恢复失败，已锁定回收；保留日志和快照。" + error.Message + " / " + rollback.Message; }
                }
                return "拒绝/取消回收：" + error.Message;
            }
            finally { Busy = false; }
        }

        /// <summary>Used when a previous session left an unsealed journal.</summary>
        public void Lock() { Locked = true; }
    }
}
