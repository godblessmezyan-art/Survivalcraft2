using System;
using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Safety;
using BuilderAssistant.Core.Tree;
using BuilderAssistant.Core.Workbench;
using BuilderAssistant.Features.Generators;
using BuilderAssistant.Infrastructure;
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
    /// two-tap confirmation, the world-authority gate (single player or host may
    /// build, remote clients may not), and the Android undo/cancel entry point.
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

        // v0.6.3 clear-soft: when a footprint is blocked only by walk-through plants/snow,
        // the user can opt (via an extra confirmation tap) to let the build replace them.
        // Never enabled by default; solid terrain and player structures are unaffected.
        private static bool _clearSoftArmed;

        // v0.13.2 "clear obstacles within footprint": an explicit, session-persistent toggle
        // (default OFF). When ON, every build runs with allowOverride so each cell the blueprint
        // writes into is replaced unconditionally - solid terrain, player structures AND fluids -
        // with NO clearance margin beyond the exact footprint, and without the repeated two-tap
        // clear-soft confirmation dance (the toggle itself is the explicit opt-in / second
        // confirm). Undo still captures each original value (including any displaced fluid)
        // before writing, and all bounds / count / axis / batch limits stay in force.
        private static bool _clearObstaclesArmed;

        // The clear-soft intent carried across a bounded chunk-wait re-plan.
        private static bool _preflightClearSoft;

        // The clear-obstacles (allowOverride) intent carried across the same bounded re-plan.
        private static bool _preflightClearObstacles;

        /// <summary>
        /// What a build-tool tap does. <c>Cottage</c> is the unchanged v0.5/v0.6 path.
        /// <c>Copy</c> reads a two-corner region into a blueprint (and auto-exports it
        /// to the Mod's JSON library). <c>Paste</c> rebuilds the clipboard blueprint at a
        /// confirmed origin through the SAME plan/execute/undo pipeline the cottage uses,
        /// so all AGENTS safety rules apply identically to imported/copied structures.
        /// </summary>
        internal enum RuntimeMode
        {
            Cottage,
            Copy,
            Paste,
            Treehouse,
        }

        private static RuntimeMode _mode = RuntimeMode.Cottage;

        // v0.13 M4: the phased giant-treehouse assembler ("森之王庭"). A single confirmed tap
        // grows the whole structure stage by stage through the game-free TreehouseAssembly,
        // which reuses the SAME BuildPlanner / BuildTask / TransactionHistory pipeline the
        // manual tools use (per-phase undo lives in the shared _history stack). It is null
        // whenever a treehouse is not currently being assembled.
        private static TreehouseAssembly _treehouse;

        // Bounded main-thread chunk-wait budget for the treehouse (mirrors MaxPreflightFrames).
        // Reset each time a phase makes real progress; exhausted -> cancel so a phase whose
        // chunks never stream in cannot hang forever. Writing nothing while waiting.
        private static int _treehouseWaitFrames;

        // True while the current treehouse phase is blocked ONLY by walk-through plants/snow.
        // We stop pumping (no spin) and wait for an explicit build-tool re-confirm to clear
        // them, exactly like the cottage clear-soft second tap. Never assumed by default.
        private static bool _treehouseAwaitSoft;

        // The default phased blueprint set is deterministic (fixed seed) and carries no world
        // state, so it is built once and cached: the master bounding box drives drop-point
        // centering, and the part list is handed straight to TreehouseAssembly on confirm.
        private static TreehousePart[] _treehouseParts;
        private static bool _treehousePartsBuilt;
        private static int _treehouseMasterX;
        private static int _treehouseMasterY;
        private static int _treehouseMasterZ;
        private static int _treehouseMinX;
        private static int _treehouseMinY;
        private static int _treehouseMinZ;

        // The blueprint waiting to be pasted (from a copy or an imported JSON file),
        // the quarter-turn rotation to apply (0..3) and the name it came from. NOTE:
        // rotation is carried in state and applied via Blueprint.Reorient at build time;
        // an on-device rotation picker is a follow-up, so paste currently uses 0 unless a
        // caller sets it programmatically. The transform itself is fully tested offline.
        private static Blueprint _clipboard;
        private static int _clipboardRotation;
        private static string _clipboardSourceName;

        // Copy-mode selection: the first corner, then the second completes the copy.
        private static bool _copyHasA;
        private static CellPos _copyCornerA;

        // v0.8 blueprint-tool import browser: the cached library file list, the cursor
        // the last tap left us on (-1 = nothing loaded yet, so the first tap picks the
        // first file), and whether we have already printed the library path so the
        // player is not spammed with it on every empty-library tap.
        private static System.Collections.Generic.List<string> _blueprintLibNames;
        private static int _blueprintCursor = -1;
        private static bool _blueprintPathShown;

        // v0.8.1 clipboard-paste import: a cheap hash of the last pasted text that was
        // successfully imported. Repeated taps while the clipboard is unchanged just
        // re-announce the loaded blueprint instead of saving a fresh numbered copy
        // every time. 0 means "nothing imported from clipboard yet this session".
        private static int _lastClipboardImportHash;

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
            _clearSoftArmed = false;
            _preflightClearSoft = false;
            _clearObstaclesArmed = false;
            _preflightClearObstacles = false;
            _history.Clear();

            // Reset the v0.7 blueprint copy/paste state too, so a fresh world never
            // inherits a stale clipboard or a half-finished copy selection.
            _mode = RuntimeMode.Cottage;
            _clipboard = null;
            _clipboardRotation = 0;
            _clipboardSourceName = null;
            _copyHasA = false;
            _copyCornerA = default(CellPos);

            // Reset the v0.8 import browser too: a fresh world re-scans the library and
            // is allowed to print the path hint once again.
            _blueprintLibNames = null;
            _blueprintCursor = -1;
            _blueprintPathShown = false;
            _lastClipboardImportHash = 0;

            // Reset the v0.13 treehouse assembly too: a fresh world must never inherit a
            // half-grown 森之王庭 or its pending chunk-wait. The deterministic blueprint-set
            // cache (no world state) is deliberately kept so the next build re-uses it.
            _treehouse = null;
            _treehouseWaitFrames = 0;
            _treehouseAwaitSoft = false;

            // Reset the v0.11 guided workbench session too: a fresh world starts the
            // Visual Building Workbench at its first step with no stale preview.
            WorkbenchSession.Reset();
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

            if (!CanMutateWorld(gui))
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

            // v0.13 M4: a treehouse is mid-assembly. The phased pump runs on Tick, so here we
            // only consume taps. The one useful gesture is re-confirming the clear-soft opt-in
            // when a stage is blocked purely by walk-through plants/snow (mirrors the cottage
            // second tap). We never start a second assembly and never dig the block beneath.
            if (_treehouse != null)
            {
                if (_treehouseAwaitSoft)
                {
                    _treehouse.ClearSoft = true;
                    _treehouseAwaitSoft = false;
                    _treehouseWaitFrames = MaxPreflightFrames;
                    Notify(gui, "已确认清理植被，继续装配「森之王庭」（仅替换草/花/枯枝/雪，绝不破坏实心地形或你的建筑）。");
                }
                else
                {
                    Notify(gui, "「森之王庭」正在逐阶段生长（" + _treehouse.CompletedPhaseCount + "/" +
                        _treehouse.PhaseCount + " 阶段已完成），请稍候；如需中止请点击撤销工具。");
                }
                return true;
            }

            TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
            if (!hit.HasValue)
            {
                Notify(gui, "请将准星对准地面方块后再点击。");
                return true;
            }

            CellPos surface = new CellPos(hit.Value.CellFace.X, hit.Value.CellFace.Y, hit.Value.CellFace.Z);

            if (_mode == RuntimeMode.Copy)
            {
                return OnCopyTap(miner, gui, surface);
            }

            if (_mode == RuntimeMode.Paste && _clipboard == null)
            {
                Notify(gui, "剪贴板为空：请先切换到复制模式点选两块对角，或导入一个蓝图文件。");
                return true;
            }

            Blueprint effective = _mode == RuntimeMode.Paste ? EffectiveClipboard() : null;

            if (!_hasPending)
            {
                if (_mode == RuntimeMode.Treehouse)
                {
                    TreehousePart[] parts = DefaultTreehouseParts();
                    if (parts == null || parts.Length == 0)
                    {
                        Notify(gui, "「森之王庭」蓝图集在本版本离线校验未通过，无法开始装配（未改动世界）。");
                        return true;
                    }
                    _pendingOrigin = ComputeTreehouseOrigin(surface);
                    _hasPending = true;
                    _clearSoftArmed = false;
                    Notify(gui, "树屋落点已选定 " + _pendingOrigin + "（居中覆盖整片树冠）。共 " +
                        parts.Length + " 个阶段、约 " + TotalTreehouseCells(parts) +
                        " 方块，将逐段生长。再次点击建造工具以确认装配。");
                    return true;
                }

                _pendingOrigin = _mode == RuntimeMode.Paste
                    ? ComputeOrigin(effective, surface)
                    : ComputeOrigin(surface);
                _hasPending = true;
                _clearSoftArmed = false;
                if (_mode == RuntimeMode.Paste)
                {
                    Notify(gui, "粘贴落点已选定 " + _pendingOrigin + "（蓝图 " + _clipboardSourceName +
                        "）。再次点击建造工具以确认放置。");
                }
                else
                {
                    Notify(gui, "落点已选定 " + _pendingOrigin + "。再次点击建造工具以确认施工。");
                }
                return true;
            }

            CellPos origin = _pendingOrigin;
            _hasPending = false;
            if (_mode == RuntimeMode.Paste)
            {
                BeginBuildBlueprint(miner, gui, effective, origin);
            }
            else if (_mode == RuntimeMode.Treehouse)
            {
                BeginTreehouse(miner, gui, origin);
            }
            else
            {
                BeginBuild(miner, gui, origin);
            }
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

            if (!CanMutateWorld(gui))
            {
                return true;
            }

            // v0.13 M4: a treehouse is assembling. An undo tap requests cancellation of the
            // phased assembly (distinct from undoing a finished one). We leave _treehouse in
            // place so Tick keeps pumping the deferred rollback to a clean terminal state; the
            // already-completed stages remain in the shared _history and stay individually
            // undoable once this halts.
            if (_treehouse != null)
            {
                _treehouseAwaitSoft = false;
                bool cleanCancel = _treehouse.Cancel();
                Notify(gui, cleanCancel
                    ? "已取消「森之王庭」装配：正在生长的阶段已回滚，之前完成的阶段可用撤销工具逐段回退。"
                    : "已请求取消「森之王庭」：个别方块因区块未就绪会稍后自动回滚，完成阶段仍可逐段撤销。");
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
                // Nothing in flight, nothing pending, nothing to undo: on a phone there
                // is no separate mode button, so an idle undo-tool tap cycles the build
                // mode (Cottage → Copy → Paste). All busy states were handled above.
                CycleMode(gui);
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
            // v0.13 M4: while a treehouse is assembling it owns this frame. Each call advances
            // at most MaxBlocksPerBatch cells of the current stage through the SAME BuildTask
            // machinery, so writes stay batched on the main thread and never interleave.
            if (_treehouse != null)
            {
                // Paused for an explicit clear-soft confirmation: do not re-plan or spin.
                if (_treehouseAwaitSoft) return;

                TreehouseAdvanceResult r = _treehouse.Advance(BuildSafetyLimits.MaxBlocksPerBatch);

                if (_treehouse.IsTerminal)
                {
                    // Completed / Failed / Cancelled halt (rollback already drained clean).
                    Notify(gui, _treehouse.LastMessage);
                    _treehouse = null;
                    _treehouseAwaitSoft = false;
                    _treehouseWaitFrames = 0;
                    return;
                }

                switch (r)
                {
                    case TreehouseAdvanceResult.WaitingChunks:
                        // Chunks for this stage are not ready yet; nothing has been written.
                        _treehouseWaitFrames--;
                        if (_treehouseWaitFrames <= 0)
                        {
                            _treehouse.Cancel();
                            Notify(gui, "等待区块超时，正在中止「森之王庭」装配（未改动后续阶段；完成阶段可逐段撤销）。");
                        }
                        break;
                    case TreehouseAdvanceResult.BlockedBySoft:
                        _treehouseAwaitSoft = true;
                        Notify(gui, _treehouse.LastMessage);
                        break;
                    case TreehouseAdvanceResult.PhaseCompleted:
                        _treehouseWaitFrames = MaxPreflightFrames;
                        Notify(gui, _treehouse.LastMessage);
                        break;
                    default:
                        _treehouseWaitFrames = MaxPreflightFrames;
                        break;
                }
                return;
            }

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
            Blueprint cottage = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            PlanAndStart(miner, gui, cottage, origin);
        }

        /// <summary>
        /// Paste entry point. Unlike the cottage (whose keys are known-good logical
        /// names), a copied or imported blueprint may reference block types this
        /// palette cannot rebuild. Pre-flight every material key first and abort with
        /// an explicit unsupported-type list rather than letting the planner fail
        /// opaquely, then reuse the SAME plan/execute/undo pipeline as the cottage.
        /// </summary>
        private static void BeginBuildBlueprint(ComponentMiner miner, ComponentGui gui, Blueprint blueprint, CellPos origin)
        {
            if (blueprint == null || blueprint.CellCount == 0)
            {
                Notify(gui, "剪贴板为空或蓝图没有方块，无法粘贴。");
                return;
            }

            string unsupported = CollectUnresolvableKeys(blueprint);
            if (unsupported != null)
            {
                Notify(gui, "蓝图包含本版本尚不能重建的方块（" + unsupported +
                    "）。为避免劣质生成已中止粘贴，未改动世界。");
                return;
            }

            PlanAndStart(miner, gui, blueprint, origin);
        }

        /// <summary>
        /// Build (once, then cache) the deterministic phased blueprint set for 森之王庭. The
        /// layout is a fixed seed with no world dependency, so the same part list is reused
        /// across the session and its master bounding box is known for centering. Returns null
        /// (and never caches a partial) when generation or offline validation fails, so a
        /// broken treehouse can never reach the world.
        /// </summary>
        private static TreehousePart[] DefaultTreehouseParts()
        {
            if (_treehousePartsBuilt) return _treehouseParts;
            _treehousePartsBuilt = true;

            TreeCanvas canvas; TreehouseValidation validation;
            TreehousePart[] parts = TreehouseBlueprintSet.Build(
                TreehouseSpec.DefaultKingdomOfTheForest(), out canvas, out validation);

            if (parts == null || parts.Length == 0 || validation == null || !validation.IsValid)
            {
                _treehouseParts = null;
                return null;
            }

            // Global master bounding box. Part cells are absolute master coords already
            // normalized per part (each part.OffsetInMaster IS its master min corner), so the
            // union min is the smallest offset and the union max is that plus its size minus 1.
            int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;
            bool any = false;
            for (int i = 0; i < parts.Length; i++)
            {
                TreehousePart p = parts[i];
                if (p == null || p.Blueprint == null || p.Blueprint.CellCount == 0) continue;
                any = true;
                int px0 = p.OffsetInMaster.X, py0 = p.OffsetInMaster.Y, pz0 = p.OffsetInMaster.Z;
                int px1 = px0 + p.Blueprint.SizeX - 1;
                int py1 = py0 + p.Blueprint.SizeY - 1;
                int pz1 = pz0 + p.Blueprint.SizeZ - 1;
                if (px0 < minX) minX = px0;
                if (py0 < minY) minY = py0;
                if (pz0 < minZ) minZ = pz0;
                if (px1 > maxX) maxX = px1;
                if (py1 > maxY) maxY = py1;
                if (pz1 > maxZ) maxZ = pz1;
            }

            if (!any)
            {
                _treehouseParts = null;
                return null;
            }

            _treehouseMinX = minX;
            _treehouseMinY = minY;
            _treehouseMinZ = minZ;
            _treehouseMasterX = maxX - minX + 1;
            _treehouseMasterY = maxY - minY + 1;
            _treehouseMasterZ = maxZ - minZ + 1;
            _treehouseParts = parts;
            return parts;
        }

        private static int TotalTreehouseCells(TreehousePart[] parts)
        {
            int total = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null) total += parts[i].CellCount;
            }
            return total;
        }

        /// <summary>
        /// The master anchor for a treehouse aimed at <c>surface</c>: chosen so the whole master
        /// bounding box is centered horizontally on the aimed cell and its lowest layer sits one
        /// block above the surface. Every part is then stamped at anchor + OffsetInMaster, so a
        /// single tap grows the entire footprint without the player stacking phases by hand.
        /// </summary>
        private static CellPos ComputeTreehouseOrigin(CellPos surface)
        {
            int anchorX = surface.X - (2 * _treehouseMinX + _treehouseMasterX - 1) / 2;
            int anchorZ = surface.Z - (2 * _treehouseMinZ + _treehouseMasterZ - 1) / 2;
            int anchorY = surface.Y + 1 - _treehouseMinY;
            return new CellPos(anchorX, anchorY, anchorZ);
        }

        /// <summary>
        /// Treehouse start entry. Hands the game-free phased assembler the live palette resolver,
        /// a fresh terrain gateway over the current terrain, and the SHARED undo stack, so every
        /// completed stage is recorded as its own reversible transaction (per-stage undo) while
        /// all AGENTS safety rules stay enforced downstream by BuildPlanner/BuildTask. It never
        /// touches _activeTask: the assembly drives its own staged BuildTasks from Tick.
        /// </summary>
        private static void BeginTreehouse(ComponentMiner miner, ComponentGui gui, CellPos origin)
        {
            if (miner.m_subsystemTerrain == null)
            {
                Notify(gui, "地形尚未就绪，无法装配树屋。");
                return;
            }
            if (_palette == null)
            {
                Notify(gui, "方块调色板尚未就绪，请稍候再试。");
                return;
            }

            TreehousePart[] parts = DefaultTreehouseParts();
            if (parts == null || parts.Length == 0)
            {
                Notify(gui, "「森之王庭」蓝图集无效，未改动世界。");
                return;
            }

            GameTerrainGateway gateway = new GameTerrainGateway(miner.m_subsystemTerrain);
            _treehouse = new TreehouseAssembly(parts, origin, _palette, gateway, _history);
            _treehouse.ClearObstacles = _clearObstaclesArmed;
            _treehouseWaitFrames = MaxPreflightFrames;
            _treehouseAwaitSoft = false;
            _clearSoftArmed = false;

            string treehousePrefix = _clearObstaclesArmed
                ? "【清障模式】" : string.Empty;
            Notify(gui, treehousePrefix + "开始装配「森之王庭」，共 " + parts.Length + " 个阶段（逐段建造、可逐段撤销）。" +
                (_clearObstaclesArmed
                    ? "每阶段将无条件清除其占用格内所有障碍（含实心/建筑/水/岩浆，仅限占用格）；随时点击撤销工具可中止当前阶段，撤销可逐格还原含被替换的水/岩浆。"
                    : "随时点击撤销工具可中止当前阶段。"));
        }

        private static void PlanAndStart(ComponentMiner miner, ComponentGui gui, Blueprint blueprint, CellPos origin)
        {
            if (miner.m_subsystemTerrain == null)
            {
                Notify(gui, "地形尚未就绪，无法施工。");
                return;
            }

            GameTerrainGateway gateway = new GameTerrainGateway(miner.m_subsystemTerrain);
            bool clearSoft = _clearSoftArmed;
            bool override_ = _clearObstaclesArmed;

            BuildPlan plan = BuildPlanner.Plan(blueprint, origin, _palette, gateway, override_, clearSoft);
            if (plan.Success)
            {
                StartBuild(plan, blueprint, gateway, origin, clearSoft, override_);
                if (override_)
                {
                    // v0.13.2 clear-obstacles is ON: every footprint cell (solid/structure/fluid)
                    // is being replaced. The toggle stays armed for subsequent builds - it is NOT
                    // a one-shot confirmation - so we do not clear _clearObstaclesArmed here.
                    Notify(gui, "【清障模式】建造 " + blueprint.Name + "，共 " + blueprint.CellCount +
                        " 个方块：将无条件清除落点范围内所有障碍（含实心方块/建筑/水/岩浆），仅限蓝图占用格、不留外扩；" +
                        "撤销可逐格还原原方块（含被替换的水/岩浆）。");
                }
                else if (plan.SoftClearedCount > 0)
                {
                    Notify(gui, "开始建造 " + blueprint.Name + "，共 " + blueprint.CellCount +
                        " 个方块；将清理 " + plan.SoftClearedCount + " 处植被/积雪（不影响实心方块与建筑）。");
                }
                else
                {
                    Notify(gui, "开始建造 " + blueprint.Name + "，共 " + blueprint.CellCount + " 个方块。");
                }
                _clearSoftArmed = false;
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
                _preflightClearSoft = clearSoft;
                _preflightClearObstacles = override_;
                _preflightFramesLeft = MaxPreflightFrames;
                Notify(gui, "建筑范围尚未全部就绪（可能跨越多个区块），正在等待区块加载，最多 " +
                    MaxPreflightFrames + " 帧后自动放弃。当前诊断：" + plan.FailureReason);
                return;
            }

            // Blocked ONLY by walk-through plants/snow, and the user has not yet opted to
            // clear them: arm clear-soft and require one more confirmation tap on the same
            // origin. This keeps the AGENTS "no default overwrite" rule while letting the
            // player proceed with an explicit, reversible extra tap.
            if (plan.FailedBySoftObstruction && !clearSoft)
            {
                _clearSoftArmed = true;
                _pendingOrigin = origin;
                _hasPending = true;
                Notify(gui, "目标区域有植被/积雪覆盖，默认不清理。再次点击建造工具可确认「清理植被后重建」" +
                    "（只会替换草/花/枯枝/雪等可穿过方块，绝不破坏实心地形或你的建筑）。");
                return;
            }

            _clearSoftArmed = false;
            Notify(gui, "规划失败，未改动世界：" + plan.FailureReason);
        }

        private static void StartBuild(BuildPlan plan, Blueprint blueprint, GameTerrainGateway gateway, CellPos origin, bool clearSoft, bool allowOverride)
        {
            _activeGateway = gateway;
            _activeTask = new BuildTask(blueprint.Name, origin, plan.Changes, allowOverride, clearSoft);
        }

        private static void AdvancePreflight(ComponentGui gui)
        {
            if (_preflightGateway == null || _preflightBlueprint == null || _palette == null)
            {
                ClearPreflight();
                return;
            }

            bool clearSoft = _preflightClearSoft;
            bool override_ = _preflightClearObstacles;
            BuildPlan plan = BuildPlanner.Plan(
                _preflightBlueprint, _preflightOrigin, _palette, _preflightGateway, override_, clearSoft);

            if (plan.Success)
            {
                CellPos origin = _preflightOrigin;
                Blueprint blueprint = _preflightBlueprint;
                GameTerrainGateway gateway = _preflightGateway;
                ClearPreflight();
                StartBuild(plan, blueprint, gateway, origin, clearSoft, override_);
                if (override_)
                {
                    Notify(gui, "【清障模式】区块已就绪，建造 " + blueprint.Name + "，共 " +
                        blueprint.CellCount + " 个方块：将无条件清除落点范围内所有障碍（含实心/建筑/水/岩浆，仅限占用格）；" +
                        "撤销可逐格还原（含被替换的水/岩浆）。");
                }
                else if (plan.SoftClearedCount > 0)
                {
                    Notify(gui, "区块已就绪，开始建造 " + blueprint.Name + "，共 " +
                        blueprint.CellCount + " 个方块；将清理 " + plan.SoftClearedCount + " 处植被/积雪。");
                }
                else
                {
                    Notify(gui, "区块已就绪，开始建造 " + blueprint.Name + "，共 " +
                        blueprint.CellCount + " 个方块。");
                }
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
            _preflightClearSoft = false;
            _preflightClearObstacles = false;
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

        private static CellPos ComputeOrigin(Blueprint bp, CellPos surface)
        {
            int ox = surface.X - (bp.SizeX - 1) / 2;
            int oz = surface.Z - (bp.SizeZ - 1) / 2;
            int oy = surface.Y + 1; // sit the structure on top of the aimed block
            return new CellPos(ox, oy, oz);
        }

        private static CellPos ComputeOrigin(CellPos surface)
        {
            Blueprint bp = BuildingGeneratorRegistry.Default.Generate(new GenerationContext(0));
            return ComputeOrigin(bp, surface);
        }

        /// <summary>
        /// The clipboard as it will actually be pasted: the stored blueprint with the
        /// current quarter-turn rotation applied. Returns null when the clipboard is
        /// empty. Rotation is a pure blueprint-space transform (fully tested offline),
        /// so paste never needs to touch terrain to rotate.
        /// </summary>
        private static Blueprint EffectiveClipboard()
        {
            if (_clipboard == null) return null;
            return _clipboardRotation == 0 ? _clipboard : _clipboard.Reorient(_clipboardRotation, false);
        }

        /// <summary>
        /// Distinct material keys in a blueprint the palette cannot rebuild. Returns
        /// null when every key resolves (nothing unsupported). Used to give the user a
        /// precise reason on paste instead of an opaque planner failure.
        /// </summary>
        private static string CollectUnresolvableKeys(Blueprint blueprint)
        {
            if (blueprint == null || _palette == null) return null;
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var bad = new System.Collections.Generic.List<string>();
            for (int i = 0; i < blueprint.Cells.Count; i++)
            {
                string key = blueprint.Cells[i].MaterialKey ?? string.Empty;
                if (seen.Add(key) && !_palette.IsResolvableKey(key))
                {
                    bad.Add(key.Length == 0 ? "(空材料)" : key);
                }
            }
            return bad.Count == 0 ? null : string.Join("、", bad.ToArray());
        }

        /// <summary>
        /// Copy-mode two-corner selection driven by build-tool taps. The first tap
        /// records the aimed cell as corner A; the second completes the box, reads
        /// the LIVE region into a blueprint (read-only, never damages the source),
        /// stores it as the clipboard, auto-exports it to the Mod's JSON library and
        /// flips the runtime into Paste mode so the next taps place the copy. All
        /// capture/skip detail is surfaced so the player knows exactly what will and
        /// will not be reproduced.
        /// </summary>
        private static bool OnCopyTap(ComponentMiner miner, ComponentGui gui, CellPos surface)
        {
            if (!_copyHasA)
            {
                _copyCornerA = surface;
                _copyHasA = true;
                Notify(gui, "复制起点已选定 " + surface + "。请将准星移到对角块再次点击建造工具以完成复制。");
                return true;
            }

            CellPos cornerA = _copyCornerA;
            _copyHasA = false;

            if (miner.m_subsystemTerrain == null)
            {
                Notify(gui, "地形尚未就绪，无法复制。");
                return true;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string name = "copy_" + stamp;
            GameTerrainGateway gateway = new GameTerrainGateway(miner.m_subsystemTerrain);
            BlueprintCopier.Result copy = BlueprintCopier.Copy(gateway, cornerA, surface, name);

            if (copy.HasFailure)
            {
                Notify(gui, "复制失败：" + copy.Failure);
                return true;
            }
            if (copy.Blueprint == null || copy.Copied == 0)
            {
                Notify(gui, "选区内没有可复制的方块。" + copy.Describe());
                return true;
            }

            _clipboard = copy.Blueprint;
            _clipboardSourceName = copy.Blueprint.Name;
            _clipboardRotation = 0;
            _hasPending = false;

            // Auto-export so the captured structure survives the session and can be
            // re-imported or fed to the future AI pipeline. IO problems are reported
            // but never lose the in-memory clipboard (paste still works this session).
            string savedFileName;
            string saveError = BlueprintStorage.Save(name, BlueprintJson.Serialize(_clipboard), out savedFileName);

            _mode = RuntimeMode.Paste;
            string msg = copy.Describe() + " 已切换到粘贴模式：" +
                "对准新的地面两次点击建造工具即可放置该建筑。";
            if (saveError != null)
            {
                msg += "（保存到蓝图库失败：" + saveError + "，但本次粘贴仍可用。）";
            }
            else
            {
                msg += "（已存入蓝图库：" + savedFileName + "）";
            }
            Notify(gui, msg);
            return true;
        }

        /// <summary>
        /// Advance the touch-only control loop one step: Cottage → Copy → Paste → Treehouse →
        /// (clear-obstacles ON) → (clear-obstacles OFF) → Cottage. The first four steps switch
        /// build mode (unchanged v0.5-v0.13 behaviour); the last two toggle the v0.13.2 explicit
        /// "clear obstacles within footprint" (allowOverride) opt-in WITHOUT changing the current
        /// mode, so a phone player can arm/disarm unconditional footprint clearing with the same
        /// idle Undo-tool tap that cycles modes. Only meaningful in single player / as host;
        /// callers gate authority upstream.
        /// </summary>
        public static void CycleMode(ComponentGui gui)
        {
            switch (_mode)
            {
                case RuntimeMode.Cottage: SetMode(RuntimeMode.Copy, gui); break;
                case RuntimeMode.Copy: SetMode(RuntimeMode.Paste, gui); break;
                case RuntimeMode.Paste: SetMode(RuntimeMode.Treehouse, gui); break;
                case RuntimeMode.Treehouse:
                    // From Treehouse, the idle tap first ARMS clear-obstacles (mode stays Treehouse).
                    // Done via the persistent flag, not SetMode, so _clearObstaclesArmed survives.
                    _clearObstaclesArmed = true;
                    _clearSoftArmed = false;
                    _hasPending = false;
                    Notify(gui, "【清障模式 已开启】后续建造（小屋/粘贴/树屋/工作台）将无条件清除落点占用格内所有障碍" +
                        "（含实心方块/建筑/水/岩浆），仅限蓝图占用格、不留外扩。撤销仍可逐格还原（含水/岩浆）。" +
                        "再点一次撤销工具可关闭清障。");
                    break;
                default:
                    // Clear-obstacles currently ON: the next idle tap disarms it and wraps to Cottage.
                    _clearObstaclesArmed = false;
                    _clearSoftArmed = false;
                    _hasPending = false;
                    SetMode(RuntimeMode.Cottage, gui);
                    Notify(gui, "【清障模式 已关闭】恢复默认安全模式（非空气目标不覆盖）。当前：小屋模式。");
                    break;
            }
        }

        /// <summary>Set the runtime mode explicitly and announce it to the player.</summary>
        internal static void SetMode(RuntimeMode mode, ComponentGui gui)
        {
            // Abandon any half-finished selection/confirm when switching modes so a
            // stale corner or pending drop point can never leak into the new mode.
            _mode = mode;
            _hasPending = false;
            _clearSoftArmed = false;
            if (mode != RuntimeMode.Copy)
            {
                _copyHasA = false;
            }

            switch (mode)
            {
                case RuntimeMode.Copy:
                    Notify(gui, "模式：复制。点选一个角，再点选对角即可把一段真实建筑复制成蓝图并自动保存。");
                    break;
                case RuntimeMode.Paste:
                    if (_clipboard == null)
                    {
                        Notify(gui, "模式：粘贴。剪贴板为空——请先复制一段建筑，或导入一个蓝图文件。");
                    }
                    else
                    {
                        Notify(gui, "模式：粘贴。将放置蓝图「" + _clipboardSourceName + "」，对准地面两次点击确认落点。");
                    }
                    break;
                case RuntimeMode.Treehouse:
                    Notify(gui, "模式：树屋（森之王庭）。对准地面两次点击确认落点，将逐阶段自动装配一座大型原版方块奇幻树屋（可逐段撤销，随时点击撤销工具可中止）。");
                    break;
                default:
                    Notify(gui, "模式：小屋。点击建造工具将放置内置的中世纪小屋。");
                    break;
            }

            // v0.13.2 clear-obstacles is session-persistent and survives a mode switch, so remind
            // the player it is still armed whenever they change mode - never silently hide it.
            if (_clearObstaclesArmed)
            {
                Notify(gui, "提示：【清障模式】仍处于开启状态，本模式下的建造会无条件清除落点占用格内障碍（含实心/水/岩浆）。用撤销工具连点可关闭。");
            }
        }

        /// <summary>
        /// Load a saved blueprint JSON (by file stem) from the Mod library into the
        /// clipboard and switch to Paste mode. Every AGENTS rule (bounds, caps,
        /// material support, chunk readiness, no-overwrite) is still enforced later by
        /// BuildPlanner/BuildTask at paste time; this only validates parse + materials
        /// up front so the player gets an immediate, precise answer.
        /// </summary>
        public static bool ImportBlueprint(string name, ComponentGui gui)
        {
            string error;
            Blueprint bp = BlueprintStorage.LoadBlueprint(name, out error);
            if (bp == null)
            {
                Notify(gui, "导入失败：" + error);
                return false;
            }

            string unsupported = CollectUnresolvableKeys(bp);
            if (unsupported != null)
            {
                Notify(gui, "蓝图「" + bp.Name + "」含本版本尚不能重建的方块（" + unsupported +
                    "）。为避免劣质生成未载入。");
                return false;
            }

            _clipboard = bp;
            _clipboardSourceName = bp.Name;
            _clipboardRotation = 0;
            _hasPending = false;
            SetMode(RuntimeMode.Paste, gui);
            Notify(gui, "已导入蓝图「" + bp.Name + "」，共 " + bp.CellCount + " 个方块。");
            return true;
        }

        /// <summary>
        /// Blueprint-tool (item 903) tap: browse and import saved blueprint JSON. This
        /// is the on-device entry point that closes the loop "external AI writes a
        /// .json -&gt; drop it in the library folder -&gt; tap to load -&gt; build tool to
        /// place". It only reads and validates files; it never touches terrain, so a
        /// browse can't damage the world. Busy states (a build running or a chunk wait)
        /// are respected and deferred, exactly like the other tools.
        /// </summary>
        public static bool OnBlueprintTool(ComponentMiner miner, Ray3 ray)
        {
            ComponentGui gui = miner != null && miner.ComponentPlayer != null
                ? miner.ComponentPlayer.ComponentGui
                : null;

            if (!CanMutateWorld(gui))
            {
                return true;
            }

            if ((_activeTask != null && !_activeTask.IsFinished) || _preflightFramesLeft > 0)
            {
                Notify(gui, "施工或等待区块就绪中，请先用撤销工具处理完当前建造，再浏览导入蓝图。");
                return true;
            }

            if (_palette == null)
            {
                Notify(gui, "世界尚未初始化完毕，请稍候再试。");
                return true;
            }

            ImportOrBrowse(gui);
            return true;
        }

        /// <summary>
        /// v0.8.1 blueprint-tool tap decision. Prefer the lowest-friction, most
        /// touch-friendly import: if the OS clipboard currently holds text that looks
        /// like a blueprint JSON object, import it directly (paste). When there is no
        /// such text - or the clipboard service is unavailable on this platform - fall
        /// back to the proven toast + file-library browse so nothing regresses. This is
        /// read/validate only; it never writes terrain.
        /// </summary>
        private static void ImportOrBrowse(ComponentGui gui)
        {
            string raw = GameInterop.TryReadClipboardText();
            string trimmed = raw == null ? null : raw.Trim();

            // Only treat the clipboard as a paste candidate when it looks like a JSON
            // object; any other clipboard content must not hijack the browse flow.
            bool looksLikeJson = trimmed != null && trimmed.Length > 0 && trimmed[0] == '{';
            if (looksLikeJson)
            {
                int hash = TextHash(trimmed);
                if (hash != 0 && hash == _lastClipboardImportHash &&
                    _clipboard != null && _mode == RuntimeMode.Paste)
                {
                    Notify(gui, "剪贴板内容未变，已载入蓝图「" + _clipboardSourceName +
                        "」。改用建造工具(901)对准地面两次点击即可落成。");
                    return;
                }

                ImportFromText(trimmed, gui);
                return;
            }

            // No importable clipboard text: reset the paste de-dupe and browse files.
            _lastClipboardImportHash = 0;
            CycleImportLibrary(gui);
        }

        /// <summary>
        /// Import a blueprint directly from a pasted / typed JSON string. Reuses the
        /// canonical parser + validator via <see cref="BlueprintImportService"/>, and
        /// pre-flights material resolvability with the live palette. On success: load
        /// the clipboard blueprint, switch to Paste mode, and persist a uniquely-named
        /// (non-overwriting) copy to the library. On any parse/material failure: report
        /// the precise reason and LEAVE THE CURRENT CLIPBOARD UNTOUCHED - a bad paste
        /// can never clobber a good clipboard nor write terrain.
        /// </summary>
        internal static bool ImportFromText(string json, ComponentGui gui)
        {
            System.Func<string, bool> resolver = _palette != null
                ? (System.Func<string, bool>)_palette.IsResolvableKey
                : null;

            BlueprintImportResult result = BlueprintImportService.FromString(json, resolver);
            if (!result.Success)
            {
                // Integrity guarantee: no clipboard mutation, no terrain write.
                _lastClipboardImportHash = 0;
                Notify(gui, "导入失败：" + result.Reason);
                return false;
            }

            Blueprint bp = result.Blueprint;
            _clipboard = bp;
            _clipboardSourceName = bp.Name;
            _clipboardRotation = 0;
            _hasPending = false;
            SetMode(RuntimeMode.Paste, gui);
            _lastClipboardImportHash = TextHash(json.Trim());

            // Auto-save to the library so the pasted structure survives the session and
            // can be re-browsed / re-imported later. IO problems are reported but never
            // roll back the in-memory clipboard (paste still works this session).
            string savedFileName;
            string saveError = BlueprintStorage.Save(bp.Name, BlueprintJson.Serialize(bp), out savedFileName);
            string persist = saveError != null
                ? "（已载入但未存入蓝图库：" + saveError + "）"
                : "（已存入蓝图库：" + savedFileName + "）";

            Notify(gui, "已导入并载入蓝图「" + bp.Name + "」 尺寸 " + bp.SizeX + "×" + bp.SizeY + "×" + bp.SizeZ +
                "，共 " + bp.CellCount + " 方块，已切换到粘贴模式。" + persist +
                "改用建造工具(901)对准地面两次点击即可落成（可撤销）。");
            return true;
        }

        /// <summary>
        /// Cheap, stable FNV-1a hash of the trimmed clipboard text for de-duplicating
        /// repeated taps on an unchanged clipboard. Not a security hash; collisions only
        /// mean an extra re-import save at worst. Returns non-zero for non-empty input.
        /// </summary>
        private static int TextHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619u;
            }
            int r = (int)(h & 0x7FFFFFFF);
            return r == 0 ? 1 : r;
        }

        private static void CycleImportLibrary(ComponentGui gui)
        {
            string listError;
            System.Collections.Generic.List<string> names = BlueprintStorage.ListNames(out listError);

            // Library listing itself failed (e.g. no user-data path yet): tell the
            // player once and stop, rather than silently doing nothing on every tap.
            if (listError != null)
            {
                Notify(gui, "无法访问蓝图库：" + listError);
                return;
            }

            _blueprintLibNames = names;

            if (names.Count == 0)
            {
                string dir = BlueprintStorage.BaseDirectory;
                if (!_blueprintPathShown)
                {
                    _blueprintPathShown = true;
                    Notify(gui, "蓝图库是空的。请用系统文件管理器把 AI 生成的 .json 放进：" +
                        (dir ?? "(游戏尚未提供用户数据目录)") +
                        "（文件名仅限字母/数字/下划线/连字符，≤48 字符）。放好后再次点击本工具即可载入。");
                }
                else
                {
                    Notify(gui, "蓝图库仍为空。把 .json 放进：" + (dir ?? "(未知目录)") + " 后再点本工具。");
                }
                _blueprintCursor = -1;
                return;
            }

            // We have files. If the previous selection no longer exists (library shrank),
            // restart the cursor so Advance lands cleanly on the first entry.
            if (_blueprintCursor >= names.Count) _blueprintCursor = -1;

            int next = BlueprintLibraryBrowser.Advance(names.Count, _blueprintCursor);
            if (next < 0 || next >= names.Count)
            {
                _blueprintCursor = -1;
                return;
            }

            string stem = names[next];
            // ImportBlueprint validates (parse + material resolvability), refuses bad
            // files WITHOUT touching the clipboard, and on success sets Paste mode.
            if (ImportBlueprint(stem, gui))
            {
                _blueprintCursor = next;
                Blueprint bp = _clipboard;
                string label = BlueprintLibraryBrowser.PositionLabel(names.Count, next);
                Notify(gui, "已载入蓝图 " + label + "「" + bp.Name + "」 尺寸 " +
                    bp.SizeX + "×" + bp.SizeY + "×" + bp.SizeZ + "，共 " + bp.CellCount +
                    " 方块。再点本工具切换下一个；满意后改用建造工具(901)对准地面两次点击即可落成（可撤销）。");
            }
            else
            {
                // A bad file must not strand the cursor on it forever; move past it so
                // the next tap tries a different entry, and the player sees the reason
                // (ImportBlueprint already announced the specific failure).
                _blueprintCursor = next;
            }
        }

        /// <summary>Export the current clipboard blueprint to the Mod JSON library.</summary>
        public static bool ExportClipboard(ComponentGui gui, string name)
        {
            if (_clipboard == null)
            {
                Notify(gui, "剪贴板为空，没有可导出的蓝图。");
                return false;
            }
            string stem = string.IsNullOrEmpty(name) ? _clipboardSourceName : name;
            if (string.IsNullOrEmpty(stem)) stem = "blueprint";
            string savedFileName;
            string saveError = BlueprintStorage.Save(stem, BlueprintJson.Serialize(_clipboard), out savedFileName);
            if (saveError != null)
            {
                Notify(gui, "导出失败：" + saveError);
                return false;
            }
            Notify(gui, "已导出蓝图到蓝图库：" + savedFileName);
            return true;
        }

        /// <summary>
        /// Set the paste rotation (quarter turns, 0..3). The transform itself is
        /// pure blueprint space and fully tested offline; normalised modulo 4.
        /// </summary>
        public static void SetPasteRotation(int steps, ComponentGui gui)
        {
            _clipboardRotation = ((steps % 4) + 4) % 4;
            if (_clipboard == null)
            {
                Notify(gui, "已设定朝向 " + _clipboardRotation * 90 + "°（剪贴板当前为空，载入蓝图后生效）。");
            }
            else
            {
                Notify(gui, "粘贴朝向已设为 " + _clipboardRotation * 90 + "°。");
            }
        }

        // ==================== v0.11 VISUAL BUILDING WORKBENCH SURFACE ====================
        // These are the ONLY game-side hooks the guided workbench needs. They deliberately
        // expose read-only / reuse-existing-path operations and add NO second execution
        // pipeline: the workbench brain is the game-free Core/Workbench state machine, and
        // any build it confirms is fed back through the SAME BeginBuildBlueprint ->
        // PlanAndStart -> BuildTask/undo path the manual tools already use, so every
        // AGENTS safety rule (bounds, 1024/32/64 caps, no-overwrite, chunk-readiness,
        // bounded stall, main-thread-only batched writes, edit-aware undo) applies verbatim.

        /// <summary>
        /// The live material palette, offered as the read-only resolver the workbench uses to
        /// compose its footprint preview. Null before <see cref="Initialize"/> has run, in
        /// which case the session refuses to preview until the world is ready.
        /// </summary>
        internal static VanillaBlockPalette Palette
        {
            get { return _palette; }
        }

        /// <summary>
        /// The current clipboard blueprint (from a live copy or an imported JSON), surfaced so
        /// the workbench can offer it as its read-only "Imported" choice. Null when empty. The
        /// workbench applies its OWN rotation on top, so it must be handed the base blueprint.
        /// </summary>
        internal static Blueprint ClipboardBlueprint
        {
            get { return _clipboard; }
        }

        /// <summary>
        /// True while a build is executing or a chunk-readiness wait is in flight. The
        /// workbench refuses to preview or confirm during this window so it can never
        /// clobber an in-progress task or interleave two builds.
        /// </summary>
        internal static bool IsBusy
        {
            get { return (_activeTask != null && !_activeTask.IsFinished) || _preflightFramesLeft > 0 || _treehouse != null; }
        }

        /// <summary>
        /// Whether the v0.13.2 "clear obstacles within footprint" (allowOverride) toggle is
        /// currently armed. Surfaced read-only so the workbench composes its preview with the
        /// SAME override flag the final build will use, keeping "预览判定 == 落建判定" true even
        /// in clear-obstacles mode (an occupied footprint previews as allowed exactly as it will
        /// then be built).
        /// </summary>
        internal static bool IsClearObstaclesActive
        {
            get { return _clearObstaclesArmed; }
        }

        /// <summary>The live world authority, classified into the game-free enum the workbench gate understands.</summary>
        internal static WorldAuthority CurrentAuthority()
        {
            return MapAuthority(Game.NetWork.CommonLib.WorkType);
        }

        /// <summary>
        /// Drop-point origin for a blueprint aimed at <paramref name="surface"/>. This is the
        /// SAME arithmetic the manual build/paste path uses (see <see cref="ComputeOrigin(Blueprint, CellPos)"/>),
        /// reused verbatim so the workbench's preview anchor and its eventual build anchor can
        /// never diverge.
        /// </summary>
        internal static CellPos WorkbenchOriginFor(Blueprint blueprint, CellPos surface)
        {
            return ComputeOrigin(blueprint, surface);
        }

        /// <summary>
        /// Hand a workbench-confirmed build to the existing execution pipeline. The descriptor
        /// already passed the pure gate (authority + preview-valid + re-planned against current
        /// terrain). Here we still run it through the full <see cref="BeginBuildBlueprint"/>
        /// pre-flight (unresolvable-material scan) and <see cref="PlanAndStart"/> so the
        /// authoritative terrain re-check, bounded chunk wait, clear-soft re-confirm and undo
        /// recording are all identical to a manual paste. The workbench adds no shortcuts.
        /// </summary>
        internal static void WorkbenchBeginBuild(ComponentMiner miner, ComponentGui gui, BuildStartDescriptor descriptor)
        {
            if (descriptor == null || descriptor.Blueprint == null)
            {
                Notify(gui, "工作台没有可建造的蓝图。");
                return;
            }
            _clearSoftArmed = descriptor.ClearSoftObstructions;
            _clearObstaclesArmed = descriptor.AllowOverride;
            _hasPending = false;
            BeginBuildBlueprint(miner, gui, descriptor.Blueprint, descriptor.Origin);
        }

        /// <summary>
        /// Authority gate. Terrain may only be mutated by the role that owns the
        /// world: single player (Local) or the multiplayer host (Server). A
        /// connected remote client (Client) is refused, because its local writes
        /// are neither authoritative nor guaranteed to reach the host. Anything
        /// unrecognised is refused defensively.
        ///
        /// Note this is orthogonal to whether OTHER peers have the mod installed:
        /// the host writes ordinary vanilla blocks, so buildings it generates are
        /// visible to non-mod clients through normal chunk sync. Whether that sync
        /// actually lands on the 7723 build is still to be verified on-device.
        /// </summary>
        private static bool CanMutateWorld(ComponentGui gui)
        {
            if (!TerrainAuthority.IsWritable(MapAuthority(Game.NetWork.CommonLib.WorkType)))
            {
                Notify(gui, "自动建造需在单人世界或作为联机房主时使用；联机客户端请由房主建造后再查看。");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Maps the live game network role into the game-free authority enum so
        /// the writable/refused decision itself is unit-testable offline. Unknown
        /// values collapse to RemoteClient (never writable).
        /// </summary>
        private static WorldAuthority MapAuthority(Game.NetWork.WorkType workType)
        {
            switch (workType)
            {
                case Game.NetWork.WorkType.Local:
                    return WorldAuthority.SinglePlayer;
                case Game.NetWork.WorkType.Server:
                    return WorldAuthority.Host;
                case Game.NetWork.WorkType.Client:
                default:
                    return WorldAuthority.RemoteClient;
            }
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
