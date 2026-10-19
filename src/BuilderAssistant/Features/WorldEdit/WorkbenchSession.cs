using BuilderAssistant.Core;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Workbench;
using Engine;
using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// v0.11 Visual Building Workbench - the game-side touch driver. It owns ONLY the
    /// interaction mapping (a two-gesture, keyboard-free flow for Android) and delegates
    /// ALL structure logic to the game-free <see cref="BuildingWorkbench"/> state machine.
    /// Nothing here decides bounds, caps, authority, material support or overwrite policy -
    /// those live in Core and are enforced again by WorldEditRuntime's unchanged plan/execute
    /// path when a build is finally confirmed. This keeps the触屏 UI thin and every guarantee
    /// the offline tests assert intact on-device.
    ///
    /// Two gestures (no on-screen buttons needed on a phone):
    ///  - TAP AT THE SKY (the interaction ray hits no block): cycle the value of the knob the
    ///    current step is showing (building / width / length / floors / wall height / roof /
    ///    seed / facing). Repeated taps walk the small, discrete, bounded candidate sets.
    ///  - TAP AT THE GROUND (the ray hits a block): confirm the current step and advance. The
    ///    Position step computes the drop origin from the aimed cell, runs a READ-ONLY footprint
    ///    preview against the live terrain and reports it; the Build step re-validates against
    ///    the CURRENT terrain + authority and, only if everything still passes, hands the build
    ///    to the existing pipeline. A sky-tap on the Position/Build steps steps back instead, so
    ///    the player can always navigate without a cancel button.
    ///
    /// Everything user-visible on-device (that the raycast distinguishes air vs ground
    /// reliably, that the toast text is legible on all resolutions, item index 904) is 待验证:
    /// it compiles and reasons cleanly against the verified x25.01.17 assembly, but has not been
    /// confirmed on the 7723 Android client. No terrain is ever written for a preview.
    /// </summary>
    internal static class WorkbenchSession
    {
        // Ordered guided steps. The parameter knobs between Building and Rotation are skipped
        // automatically for an imported (fixed-shape) blueprint.
        private const int StepBuilding = 0;
        private const int StepWidth = 1;
        private const int StepLength = 2;
        private const int StepFloors = 3;
        private const int StepWallHeight = 4;
        private const int StepRoof = 5;
        private const int StepSeed = 6;
        private const int StepRotation = 7;
        private const int StepPosition = 8;
        private const int StepBuild = 9;
        private const int LastStep = StepBuild;

        private static BuildingSelectionService _service;
        private static BuildingWorkbench _workbench;
        private static int _step = StepBuilding;

        /// <summary>Drop the guided session so a fresh world starts clean at the first step.</summary>
        public static void Reset()
        {
            _service = null;
            _workbench = null;
            _step = StepBuilding;
        }

        private static void EnsureBootstrapped()
        {
            if (_service == null)
            {
                _service = new BuildingSelectionService();
            }
            if (_workbench == null)
            {
                _workbench = new BuildingWorkbench(_service);
            }
        }

        /// <summary>
        /// Workbench-tool (item 904) tap. Decides the sky-vs-ground gesture and applies it.
        /// Returns true so the tap is always consumed and never digs the block under the crosshair.
        /// </summary>
        public static bool OnWorkbenchTool(ComponentMiner miner, Ray3 ray)
        {
            ComponentGui gui = miner != null && miner.ComponentPlayer != null
                ? miner.ComponentPlayer.ComponentGui
                : null;

            if (WorldEditRuntime.Palette == null)
            {
                Notify(gui, "世界尚未初始化完毕，请稍候再试。");
                return true;
            }

            // Never interleave with a running build or a chunk-readiness wait: refuse politely.
            if (WorldEditRuntime.IsBusy)
            {
                Notify(gui, "施工或等待区块就绪中，请先用撤销工具(902)处理完当前建造，再使用工作台。");
                return true;
            }

            EnsureBootstrapped();

            TerrainRaycastResult? hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
            bool groundTap = hit.HasValue;

            if (groundTap)
            {
                CellPos surface = new CellPos(hit.Value.CellFace.X, hit.Value.CellFace.Y, hit.Value.CellFace.Z);
                HandleGroundTap(miner, gui, surface);
            }
            else
            {
                HandleSkyTap(gui);
            }
            return true;
        }

        // ============================ SKY TAP: CYCLE CURRENT KNOB ============================

        private static void HandleSkyTap(ComponentGui gui)
        {
            if (_step == StepPosition || _step == StepBuild)
            {
                // No knob to cycle here: step back so the player can re-aim / tweak.
                _step--;
                Notify(gui, "已返回上一步。" + StepHint(gui));
                return;
            }

            WorkbenchStatus status = CycleCurrentKnob();
            Announce(gui, status);
        }

        private static WorkbenchStatus CycleCurrentKnob()
        {
            switch (_step)
            {
                case StepBuilding:
                {
                    // The imported choice must reflect the live clipboard at the moment we
                    // switch selection, so refresh it before enumerating choices.
                    _service.SetImportedBlueprint(WorldEditRuntime.ClipboardBlueprint);
                    if (_workbench.Stage == WorkbenchStage.Idle)
                    {
                        return _workbench.SelectFirst();
                    }
                    return _workbench.SelectNextChoice();
                }
                case StepWidth: return _workbench.CycleWidth();
                case StepLength: return _workbench.CycleLength();
                case StepFloors: return _workbench.CycleFloors();
                case StepWallHeight: return _workbench.CycleWallHeight();
                case StepRoof: return _workbench.CycleRoof();
                case StepSeed: return _workbench.CycleSeedVariant();
                case StepRotation: return _workbench.RotateCw();
                default: return WorkbenchStatus.Fail("该步骤没有可切换的参数。");
            }
        }

        // ============================ GROUND TAP: CONFIRM / ADVANCE ============================

        private static void HandleGroundTap(ComponentMiner miner, ComponentGui gui, CellPos surface)
        {
            switch (_step)
            {
                case StepPosition:
                    DoPreview(miner, gui, surface);
                    break;
                case StepBuild:
                    DoConfirm(miner, gui);
                    break;
                default:
                    // Any configure step: confirm it and move to the next applicable step.
                    _step = NextStep(_step);
                    if (_step > StepPosition)
                    {
                        _step = StepPosition;
                    }
                    Notify(gui, StepHint(gui));
                    break;
            }
        }

        /// <summary>
        /// READ-ONLY footprint preview at the aimed origin. Generates the current draft if a
        /// parameter is dirty (the pure layer preserves the last valid building on a rejected
        /// change), computes the SAME origin the build will use, then runs the unchanged planner.
        /// No terrain is written here - ever.
        /// </summary>
        private static void DoPreview(ComponentMiner miner, ComponentGui gui, CellPos surface)
        {
            if (miner.m_subsystemTerrain == null)
            {
                Notify(gui, "地形尚未就绪，无法预览。");
                return;
            }

            if (_workbench.EffectiveBlueprint() == null)
            {
                WorkbenchStatus gen = _workbench.Regenerate();
                if (!gen.Accepted)
                {
                    Announce(gui, gen);
                    return;
                }
            }

            Blueprint effective = _workbench.EffectiveBlueprint();
            if (effective == null)
            {
                Notify(gui, "尚未选择或生成可预览的建筑。");
                return;
            }

            CellPos origin = WorldEditRuntime.WorkbenchOriginFor(effective, surface);
            GameTerrainGateway gateway = new GameTerrainGateway(miner.m_subsystemTerrain);

            BuildPreviewModel model;
            WorkbenchStatus status = _workbench.PreviewAt(origin, gateway, WorldEditRuntime.Palette, out model);

            if (status == null || model == null)
            {
                Notify(gui, status != null ? status.Reason : "预览失败。");
                return;
            }

            if (!model.Allowed)
            {
                // Preview shown but refused (occupied / out of bounds / not ready). Stay on the
                // Position step so the player can move the aim and preview again.
                Notify(gui, "预览：" + model.Summarize() + " 请换个落点后对准地面重试。");
                return;
            }

            _step = StepBuild;
            Notify(gui, "预览通过：" + model.Summarize() +
                " 再次对准地面点击工作台即可确认建造（走完整安全管线，可撤销）。" + StepHint(gui));
        }

        /// <summary>
        /// Final confirm. The pure gate re-checks authority + preview validity AND re-plans against
        /// the CURRENT terrain (a chunk that unloaded or a block placed since the preview aborts
        /// here with nothing written). Only an accepted descriptor is handed to the existing build
        /// path; a denial keeps the player on Position to re-preview.
        /// </summary>
        private static void DoConfirm(ComponentMiner miner, ComponentGui gui)
        {
            if (miner.m_subsystemTerrain == null)
            {
                Notify(gui, "地形尚未就绪，无法建造。");
                return;
            }

            GameTerrainGateway gateway = new GameTerrainGateway(miner.m_subsystemTerrain);
            BuildStartDescriptor descriptor;
            WorkbenchStatus status = _workbench.TryBeginBuild(
                WorldEditRuntime.CurrentAuthority(), gateway, WorldEditRuntime.Palette, out descriptor);

            if (!status.Accepted || descriptor == null)
            {
                // Preview invalidated / terrain changed / not authoritative: fall back to Position
                // so the next ground tap re-previews against the current world.
                _step = StepPosition;
                Notify(gui, "未能确认建造（已回到预览步骤，可重新对准地面预览）：" + status.Reason);
                return;
            }

            _workbench.CommitApplied();
            WorldEditRuntime.WorkbenchBeginBuild(miner, gui, descriptor);
            _step = StepPosition;
        }

        // ============================ STEP NAVIGATION ============================

        /// <summary>
        /// The next applicable step from <paramref name="current"/>. For an imported (fixed-shape)
        /// blueprint the parameter knobs (Width..Seed) are skipped so the UI never fakes usability;
        /// those steps would only ever report "不适用".
        /// </summary>
        private static int NextStep(int current)
        {
            int next = current + 1;
            if (IsImported() && next >= StepWidth && next <= StepSeed)
            {
                next = StepRotation;
            }
            if (next > LastStep)
            {
                next = LastStep;
            }
            return next;
        }

        private static bool IsImported()
        {
            return _workbench != null && _workbench.Choice != null
                && _workbench.Choice.Kind == BuildingChoiceKind.Imported;
        }

        /// <summary>A short instruction for the step the session has just moved onto.</summary>
        private static string StepHint(ComponentGui gui)
        {
            switch (_step)
            {
                case StepBuilding: return "【选择建筑】对准天空点击切换建筑；对准地面点击进入下一步。";
                case StepWidth: return "【宽度】对准天空点击切换宽度；对准地面点击进入下一步。";
                case StepLength: return "【长度】对准天空点击切换长度；对准地面点击进入下一步。";
                case StepFloors: return "【楼层】对准天空点击切换楼层；对准地面点击进入下一步。";
                case StepWallHeight: return "【每层墙高】对准天空点击切换；对准地面点击进入下一步。";
                case StepRoof: return "【屋顶】对准天空点击切换四坡/人字/平顶；对准地面点击进入下一步。";
                case StepSeed: return "【随机种子】对准天空点击切换（门/窗/隔断等布局变体）；对准地面点击进入下一步。";
                case StepRotation: return "【朝向】对准天空点击顺时针旋转90°；对准地面点击进入落点预览。";
                case StepPosition: return "【落点预览】对准地面点击生成只读占位预览（不改动世界）；对准天空点击返回上一步。";
                case StepBuild: return "【确认建造】对准地面点击确认建造（会用最新地形重新校验，可撤销）；对准天空点击返回预览。";
                default: return string.Empty;
            }
        }

        /// <summary>Report a pure-layer status to the player (accepted shows the reason + step hint).</summary>
        private static void Announce(ComponentGui gui, WorkbenchStatus status)
        {
            if (status == null)
            {
                return;
            }
            if (status.Accepted)
            {
                Notify(gui, status.Reason + " " + StepHint(gui));
            }
            else
            {
                Notify(gui, "（无效操作）" + status.Reason + " " + StepHint(gui));
            }
        }

        private static void Notify(ComponentGui gui, string message)
        {
            if (gui != null)
            {
                gui.DisplaySmallMessage("[工作台] " + message, Color.White, false, false);
            }
        }
    }
}
