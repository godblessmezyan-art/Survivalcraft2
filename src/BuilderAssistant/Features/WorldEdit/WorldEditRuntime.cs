using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Safety;
using BuilderAssistant.Features.Generators;
using Engine;
using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Main-thread coordinator that connects the pure build engine to the live
    /// world. It is driven from SubsystemBlockBehavior.OnUse (a tap on the main
    /// thread) and the GuiUpdate tick (also main thread), so terrain is never
    /// touched from a background thread. Every AGENTS.md safety rule is enforced
    /// downstream by BuildPlanner / BuildTask; this class only adds the explicit
    /// two-tap confirmation, the single-player gate, and the Android undo/cancel
    /// entry point.
    /// </summary>
    internal static class WorldEditRuntime
    {
        private const string Prefix = "[BuilderAssistant] ";

        private static VanillaBlockPalette _palette;
        private static readonly TransactionHistory _history = new TransactionHistory();

        // The one in-flight build, if any. Advanced a few cells per GuiUpdate.
        private static BuildTask _activeTask;
        private static GameTerrainGateway _activeGateway;

        // Explicit two-tap confirmation of the drop point.
        private static bool _hasPending;
        private static CellPos _pendingOrigin;

        /// <summary>
        /// Called from the BlocksInitalized hook and whenever a new world session
        /// re-initializes blocks. Rebuilds the palette against the fresh
        /// BlocksManager and drops any stale selection, history and terrain handle
        /// so a new world never inherits an old world undo records.
        /// </summary>
        public static void Initialize()
        {
            if (_palette == null)
            {
                _palette = new VanillaBlockPalette();
            }
            else
            {
                _palette.Reset();
            }

            ResetSession();
        }

        /// <summary>Drop any world-bound state (active build, selection, undo stack).</summary>
        public static void ResetSession()
        {
            _activeTask = null;
            _activeGateway = null;
            _hasPending = false;
            _pendingOrigin = default(CellPos);
            _history.Clear();
        }

        /// <summary>
        /// Build-tool tap given the interaction ray already computed by the
        /// behavior layer. First tap arms a drop point from the aimed cell, the
        /// second tap confirms and starts construction there. Returns true so the
        /// tap is consumed and never digs the block underneath.
        /// </summary>
        public static bool OnBuildTool(ComponentMiner miner, Ray3 ray)
        {
            ComponentGui gui = miner != null && miner.ComponentPlayer != null
                ? miner.ComponentPlayer.ComponentGui
                : null;

            if (!IsSinglePlayer(gui))
            {
                return true;
            }

            if (_palette == null)
            {
                Notify(gui, "世界尚未初始化完毕，请稍候再试。");
                return true;
            }

            if (_activeTask != null && !_activeTask.IsFinished)
            {
                Notify(gui, "施工进行中，请耐心等待或使用撤销工具取消。");
                return true;
            }

            TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
            if (!hit.HasValue)
            {
                Notify(gui, "请将准星对准地面方块后再点击。");
                return true;
            }

            CellPos surface = new CellPos(hit.Value.CellFace.X, hit.Value.CellFace.Y, hit.Value.CellFace.Z);

            if (!_hasPending)
            {
                _pendingOrigin = ComputeOrigin(surface);
                _hasPending = true;
                Notify(gui, "落点已选定 " + _pendingOrigin + "。再次点击建造工具以确认施工。");
                return true;
            }

            CellPos origin = _pendingOrigin;
            _hasPending = false;
            BeginBuild(miner, gui, origin);
            return true;
        }

        /// <summary>
        /// Undo-tool tap. Cancels an in-flight build (rolling it back) first;
        /// otherwise undoes the most recent completed transaction. Undo only
        /// restores cells whose current value still matches what that build
        /// wrote, so later player edits are never clobbered.
        /// </summary>
        public static bool OnUndoTool(ComponentMiner miner)
        {
            ComponentGui gui = miner != null && miner.ComponentPlayer != null
                ? miner.ComponentPlayer.ComponentGui
                : null;

            if (!IsSinglePlayer(gui))
            {
                return true;
            }

            if (_activeTask != null && !_activeTask.IsFinished)
            {
                GameTerrainGateway gateway = _activeGateway;
                if (gateway == null && miner.m_subsystemTerrain != null)
                {
                    gateway = new GameTerrainGateway(miner.m_subsystemTerrain);
                }

                bool cancelled = gateway != null && _activeTask.Cancel(gateway);
                _activeTask = null;
                _activeGateway = null;
                _hasPending = false;
                Notify(gui, cancelled
                    ? "已取消施工并回滚已放置的方块。"
                    : "施工无法继续（区块未加载），已放弃当前任务。");
                return true;
            }

            if (_hasPending)
            {
                _hasPending = false;
                Notify(gui, "已清除待确认的落点。");
                return true;
            }

            if (!_history.HasUndoable)
            {
                Notify(gui, "没有可撤销的建造。");
                return true;
            }

            if (miner.m_subsystemTerrain == null)
            {
                Notify(gui, "地形尚未就绪，无法撤销。");
                return true;
            }

            GameTerrainGateway undoGateway = new GameTerrainGateway(miner.m_subsystemTerrain);
            int restored = _history.UndoLast(undoGateway);
            if (restored < 0)
            {
                Notify(gui, "撤销失败：最新一次建造的目标区块已卸载。");
            }
            else
            {
                Notify(gui, "已撤销上一次建造，恢复 " + restored + " 个方块。");
            }
            return true;
        }

        /// <summary>
        /// Advance the active build by up to MaxBlocksPerBatch cells. Called once
        /// per frame from GuiUpdate on the main thread.
        /// </summary>
        public static void Tick(ComponentGui gui)
        {
            if (_activeTask == null)
            {
                return;
            }

            if (_activeGateway == null)
            {
                _activeTask = null;
                return;
            }

            if (!_activeTask.IsFinished)
            {
                _activeTask.Advance(_activeGateway, BuildSafetyLimits.MaxBlocksPerBatch);
            }

            if (_activeTask.IsFinished)
            {
                FinalizeActive(gui);
            }
        }

        private static void BeginBuild(ComponentMiner miner, ComponentGui gui, CellPos origin)
        {
            if (miner.m_subsystemTerrain == null)
            {
                Notify(gui, "地形尚未就绪，无法施工。");
                return;
            }

            Blueprint blueprint = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            GameTerrainGateway gateway = new GameTerrainGateway(miner.m_subsystemTerrain);

            BuildPlan plan = BuildPlanner.Plan(blueprint, origin, _palette, gateway, false);
            if (!plan.Success)
            {
                Notify(gui, "规划失败，未改动世界：" + plan.FailureReason);
                return;
            }

            _activeGateway = gateway;
            _activeTask = new BuildTask(blueprint.Name, origin, plan.Changes, false);
            Notify(gui, "开始建造 " + blueprint.Name + "，共 " + blueprint.CellCount + " 个方块。");
        }

        private static void FinalizeActive(ComponentGui gui)
        {
            BuildTask task = _activeTask;
            if (task == null)
            {
                return;
            }

            switch (task.State)
            {
                case BuildTaskState.Completed:
                    _history.Record(task);
                    Notify(gui, "建造完成：" + task.Name + " " + task.AppliedCount + "/" + task.PlannedCount +
                        " 方块。用撤销工具可整体回退。");
                    break;
                case BuildTaskState.Failed:
                    Notify(gui, "施工已中止并自动回滚：" + task.FailureReason);
                    break;
                case BuildTaskState.Cancelled:
                    Notify(gui, "施工已取消并回滚。");
                    break;
                default:
                    break;
            }

            _activeTask = null;
            _activeGateway = null;
        }

        private static CellPos ComputeOrigin(CellPos surface)
        {
            Blueprint bp = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            int ox = surface.X - (bp.SizeX - 1) / 2;
            int oz = surface.Z - (bp.SizeZ - 1) / 2;
            int oy = surface.Y + 1; // sit the structure on top of the aimed block
            return new CellPos(ox, oy, oz);
        }

        private static bool IsSinglePlayer(ComponentGui gui)
        {
            if (Game.NetWork.CommonLib.WorkType != Game.NetWork.WorkType.Local)
            {
                Notify(gui, "自动建造目前仅在单人模式下启用（测试阶段）。");
                return false;
            }
            return true;
        }

        private static void Notify(ComponentGui gui, string message)
        {
            if (gui != null)
            {
                gui.DisplaySmallMessage(Prefix + message, Color.White, false, false);
            }
        }
    }
}
