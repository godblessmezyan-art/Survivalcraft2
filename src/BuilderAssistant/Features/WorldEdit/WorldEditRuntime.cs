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
    /// <remarks>
    /// v0.6.0: the whole footprint is now pre-checked on the main thread. A 9x11
    /// cottage can straddle several chunks, and a single crosshair hit being loaded
    /// does not mean the entire site is ready. If the only problem is chunks the
    /// engine has not streamed in yet (recoverable), we wait a bounded number of
    /// frames and re-plan instead of failing the first tap — but we NEVER write
    /// during the wait, and out-of-bounds/height errors still abort immediately.
    /// </remarks>
    internal static class WorldEditRuntime
    {
        private const string Prefix = "[BuilderAssistant] ";

        /// <summary>
        /// Upper bound on GuiUpdate frames the preflight wait may consume before it
        /// gives up. At typical 30-60 fps this is a few seconds — long enough for a
        /// neighbouring chunk to load, short enough to feel responsive on a phone.
        /// It is a bounded wait, not a blind write.
        /// </summary>
        private const int MaxPreflightFrames = 150;

        private static VanillaBlockPalette _palette;
        private static readonly TransactionHistory _history = new TransactionHistory();

        // The one in-flight build, if any. Advanced a few cells per GuiUpdate.
        private static BuildTask _activeTask;
        private static GameTerrainGateway _activeGateway;

        // Bounded main-thread preflight wait (waiting for chunks, writing nothing).
        private static GameTerrainGateway _preflightGateway;
        private static Blueprint _preflightBlueprint;
        private static CellPos _preflightOrigin;
        private static int _preflightFramesLeft;

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

        /// <summary>Drop any world-bound state (active build, selection, preflight, undo stack).</summary>
        public static void ResetSession()
        {
            _activeTask = null;
            _activeGateway = null;
            ClearPreflight();
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

            if (_preflightFramesLeft > 0)
            {
                Notify(gui, "正在等待建筑范围的区块就绪，请稍候…");
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

            // Waiting on chunks during preflight? Nothing has been written yet, so a
            // cancel here is simply aborting the pending build intent.
            if (_activeTask == null && _preflightFramesLeft > 0)
            {
                ClearPreflight();
                Notify(gui, "已取消等待中的建造（尚未改动世界）。");
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
                _hasPending = false;

                if (cancelled && _activeTask.PendingRollbackCount > 0)
                {
                    // Some placed cells sit in chunks that are momentarily uneditable;
                    // keep the task alive so Tick retries restoring them, and be honest
                    // that the rollback is not yet complete.
                    Notify(gui, "已请求取消，但有 " + _activeTask.PendingRollbackCount +
                        " 个方块因区块未就绪暂未回滚，将在区块恢复后自动重试。");
                }
                else
                {
                    Notify(gui, cancelled
                        ? "已取消施工并回滚已放置的方块。"
                        : "施工无法继续（区块未加载），已放弃当前任务。");
                    _activeTask = null;
                    _activeGateway = null;
                }
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
            int pendingUndo;
            int restored = _history.UndoLast(undoGateway, out pendingUndo);
            if (restored < 0)
            {
                Notify(gui, "撤销失败：最新一次建造的目标区块已卸载。");
            }
            else if (pendingUndo > 0)
            {
                Notify(gui, "已撤销，恢复 " + restored + " 个方块；" + pendingUndo +
                    " 个因区块未加载暂未恢复，区块就绪后可再次点击撤销完成。");
            }
            else
            {
                Notify(gui, "已撤销上一次建造，恢复 " + restored + " 个方块。");
            }
            return true;
        }

        /// <summary>
        /// Per-frame main-thread pump: advance the active build, keep retrying a
        /// deferred rollback, or make progress on the bounded preflight wait.
        /// </summary>
        public static void Tick(ComponentGui gui)
        {
            if (_activeTask != null)
            {
                if (_activeGateway == null)
                {
                    _activeTask = null;
                    return;
                }

                if (!_activeTask.IsFinished)
                {
                    _activeTask.Advance(_activeGateway, BuildSafetyLimits.MaxBlocksPerBatch);
                    if (_activeTask.IsFinished)
                    {
                        FinalizeActive(gui);
                    }
                    return;
                }

                // Finished (Failed/Cancelled) but kept around because a rollback was
                // deferred. Retry restoring the pending cells; clear once it is clean.
                if (_activeTask.PendingRollbackCount > 0)
                {
                    int still = _activeTask.RetryRollback(_activeGateway);
                    if (still == 0)
                    {
                        Notify(gui, "延迟的方块已随区块恢复完成回滚。");
                        _activeTask = null;
                        _activeGateway = null;
                    }
                }
                else
                {
                    _activeTask = null;
                    _activeGateway = null;
                }
                return;
            }

            if (_preflightFramesLeft > 0)
            {
                AdvancePreflight(gui);
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
            if (plan.Success)
            {
                StartBuild(plan, blueprint, gateway, origin);
                Notify(gui, "开始建造 " + blueprint.Name + "，共 " + blueprint.CellCount + " 个方块。");
                return;
            }

            // Whole-site pre-check failed. If it is only that some chunks are not
            // ready yet, wait a bounded number of frames and re-plan; otherwise abort
            // with the now-detailed diagnostic. We never write during the wait.
            if (plan.AllEditabilityFailuresRecoverable)
            {
                _preflightGateway = gateway;
                _preflightBlueprint = blueprint;
                _preflightOrigin = origin;
                _preflightFramesLeft = MaxPreflightFrames;
                Notify(gui, "建筑范围尚未全部就绪（可能跨越多个区块），正在等待区块加载，最多 " +
                    MaxPreflightFrames + " 帧后自动放弃。当前诊断：" + plan.FailureReason);
                return;
            }

            Notify(gui, "规划失败，未改动世界：" + plan.FailureReason);
        }

        private static void StartBuild(BuildPlan plan, Blueprint blueprint, GameTerrainGateway gateway, CellPos origin)
        {
            _activeGateway = gateway;
            _activeTask = new BuildTask(blueprint.Name, origin, plan.Changes, false);
        }

        private static void AdvancePreflight(ComponentGui gui)
        {
            if (_preflightGateway == null || _preflightBlueprint == null || _palette == null)
            {
                ClearPreflight();
                return;
            }

            BuildPlan plan = BuildPlanner.Plan(
                _preflightBlueprint, _preflightOrigin, _palette, _preflightGateway, false);

            if (plan.Success)
            {
                CellPos origin = _preflightOrigin;
                Blueprint blueprint = _preflightBlueprint;
                GameTerrainGateway gateway = _preflightGateway;
                ClearPreflight();
                StartBuild(plan, blueprint, gateway, origin);
                Notify(gui, "区块已就绪，开始建造 " + blueprint.Name + "，共 " +
                    blueprint.CellCount + " 个方块。");
                return;
            }

            // Became a hard failure mid-wait (e.g. world changed): stop waiting.
            if (!plan.AllEditabilityFailuresRecoverable)
            {
                ClearPreflight();
                Notify(gui, "等待期间范围判定为不可施工，已停止：" + plan.FailureReason);
                return;
            }

            _preflightFramesLeft--;
            if (_preflightFramesLeft == 0)
            {
                ClearPreflight();
                Notify(gui, "区块在等待后仍未全部就绪，已停止（未改动世界）。最后诊断：" + plan.FailureReason);
            }
            // Otherwise keep waiting quietly; re-plan next frame.
        }

        private static void ClearPreflight()
        {
            _preflightGateway = null;
            _preflightBlueprint = null;
            _preflightOrigin = default(CellPos);
            _preflightFramesLeft = 0;
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
                    _activeTask = null;
                    _activeGateway = null;
                    break;
                case BuildTaskState.Failed:
                    if (task.PendingRollbackCount > 0)
                    {
                        // Keep the task so Tick can retry the deferred rollback; do not
                        // claim a clean rollback we have not achieved.
                        Notify(gui, "施工已中止：" + task.FailureReason);
                    }
                    else
                    {
                        Notify(gui, "施工已中止并自动回滚：" + task.FailureReason);
                        _activeTask = null;
                        _activeGateway = null;
                    }
                    break;
                case BuildTaskState.Cancelled:
                    if (task.PendingRollbackCount > 0)
                    {
                        Notify(gui, "施工已取消：" + task.FailureReason);
                    }
                    else
                    {
                        Notify(gui, "施工已取消并回滚。");
                        _activeTask = null;
                        _activeGateway = null;
                    }
                    break;
                default:
                    _activeTask = null;
                    _activeGateway = null;
                    break;
            }
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
