using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.BuildEngine;
using BuilderAssistant.Core.Building;
using BuilderAssistant.Core.Safety;
using BuilderAssistant.Core.Transactions;

namespace BuilderAssistant.Core.Workbench
{
    /// <summary>
    /// The brain of the Visual Building Workbench: a pure, game-independent, offline-testable
    /// state machine that turns touch gestures into <see cref="BuildingSpec"/> edits, reuses
    /// the UNCHANGED generation engine (via <see cref="BuildingComposer"/>) and the UNCHANGED
    /// read-only planner (via <see cref="BuildPlanner"/>) to produce a real <see cref="BuildPreviewModel"/>,
    /// and only then admits the player to the existing build pipeline.
    ///
    /// Load-bearing guarantees (each backed by a unit test):
    /// - A rejected command NEVER destroys the last valid building. Parameters live in a
    ///   <c>_draft</c> spec; the built blueprint lives in <c>_blueprint</c>; a failed generate
    ///   leaves <c>_blueprint</c> exactly as it was. ("改参失败不改变当前有效建筑")
    /// - Every mutation that changes the structure bumps a monotonic <c>_version</c>. A preview
    ///   stamps the version it was taken at; confirmation is only honoured while the versions
    ///   still match, so tweaking params / rotating / re-selecting after a preview silently
    ///   invalidates the pending confirmation. ("改参后确认失效")
    /// - Preview and the pre-build gate run ONLY through <c>BuildPlanner.Plan</c>, which is
    ///   read-only; the workbench itself never calls <c>SetCellValue</c>. ("预览阶段绝不写地形")
    /// - <see cref="TryBeginBuild"/> re-plans against the CURRENT terrain and checks authority,
    ///   so terrain that changed (or became non-authoritative) between preview and confirm is
    ///   refused, and a remote client is always refused. ("建造前重校验""权限未退化")
    /// - Footprint/area/cell-count come from the same effective blueprint the build writes, so
    ///   "预览面积 == 最终建造面积" holds by construction.
    /// </summary>
    internal sealed class BuildingWorkbench
    {
        // Discrete, bounded candidates per knob. Fixed small sets are what make single-tap
        // cycling safe: no arbitrary text entry (which would need an unverified on-device
        // textbox), and every value is still validated by the engine on generate.
        private static readonly int[] Widths = { 5, 7, 9, 11, 13 };
        private static readonly int[] Lengths = { 5, 7, 9, 11, 13 };
        private static readonly int[] CottageFloors = { 1, 2, 3 };
        private static readonly int[] HouseFloors = { 2, 3 };
        private static readonly int[] WallHeights = { 3, 4, 5 };
        private static readonly RoofType[] Roofs = { RoofType.Hip, RoofType.Gabled, RoofType.Flat };
        private static readonly int[] Seeds = { 0, 1, 2, 3, 4 };

        private readonly BuildingSelectionService _service;

        private BuildingChoice _choice;
        private BuildingSpec _draft;
        private bool _draftDirty;
        private int _rotation;
        private Blueprint _blueprint; // last successfully generated (base, unrotated)

        private WorkbenchStage _stage = WorkbenchStage.Idle;
        private long _version;

        // The preview + the version it was stamped at (confirmation validity).
        private bool _hasPreview;
        private bool _previewStale;
        private long _previewVersion;
        private BuildPreviewModel _preview;
        private Blueprint _previewBlueprint;
        private CellPos _previewOrigin;

        // The last CONFIRMED-good building, used by Cancel to restore ("还原").
        private bool _hasApplied;
        private BuildingChoice _appliedChoice;
        private BuildingSpec _appliedDraft;
        private int _appliedRotation;
        private Blueprint _appliedBlueprint;

        public BuildingWorkbench(BuildingSelectionService service)
        {
            _service = service;
        }

        // --- Read-only view for the caller / tests. ---
        public WorkbenchStage Stage { get { return _stage; } }
        public BuildingChoice Choice { get { return _choice; } }
        public BuildingSpec Draft { get { return _draft; } }
        public int Rotation { get { return _rotation; } }
        public Blueprint CurrentBlueprint { get { return _blueprint; } }
        public bool HasPreview { get { return _hasPreview; } }
        public BuildPreviewModel Preview { get { return _preview; } }
        public bool NeedsRegenerate { get { return _choice != null && _choice.Kind != BuildingChoiceKind.Imported && (_draftDirty || _blueprint == null); } }

        /// <summary>The blueprint that will actually be built/previewed: the current base with
        /// rotation applied. Null when nothing has been produced yet.</summary>
        public Blueprint EffectiveBlueprint()
        {
            Blueprint baseBp = _choice != null && _choice.Kind == BuildingChoiceKind.Imported
                ? (_service != null ? _service.ImportedBlueprint : null)
                : _blueprint;
            if (baseBp == null)
            {
                return null;
            }
            return _rotation == 0 ? baseBp : baseBp.Reorient(_rotation, false);
        }

        // ============================ SELECTION ============================

        public WorkbenchStatus SelectFirst()
        {
            if (_service == null || _service.Choices.Count == 0)
            {
                return WorkbenchStatus.Fail("没有可选建筑。");
            }
            return ApplyChoice(_service.Choices[0]);
        }

        public WorkbenchStatus SelectNextChoice()
        {
            if (_service == null || _service.Choices.Count == 0)
            {
                return WorkbenchStatus.Fail("没有可选建筑。");
            }
            var all = _service.Choices;
            int idx = 0;
            if (_choice != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Key == _choice.Key) { idx = i; break; }
                }
            }
            int next = _choice == null ? 0 : (idx + 1) % all.Count;
            return ApplyChoice(all[next]);
        }

        private WorkbenchStatus ApplyChoice(BuildingChoice choice)
        {
            _choice = choice;
            _rotation = 0;
            _blueprint = null;
            if (choice.Kind == BuildingChoiceKind.Imported)
            {
                _draft = null;
                _draftDirty = false;
                if (_service.ImportedBlueprint == null)
                {
                    _stage = WorkbenchStage.Idle;
                    Bump();
                    return WorkbenchStatus.Fail("当前没有已导入的蓝图。");
                }
            }
            else
            {
                BuildingSpec spec;
                if (!_service.TryMakeDefaultSpec(choice, out spec))
                {
                    _stage = WorkbenchStage.Idle;
                    Bump();
                    return WorkbenchStatus.Fail("无法为该建筑生成默认参数。");
                }
                _draft = spec;
                _draftDirty = true;
            }
            InvalidatePreview();
            _stage = WorkbenchStage.Configure;
            Bump();
            return WorkbenchStatus.Ok("已选择：" + choice.DisplayName + DescribeParams(), _stage, true);
        }

        // ============================ PARAMETER CYCLING ============================

        public WorkbenchStatus CycleWidth()
        {
            return CycleScalarAxis(Widths, isWidth: true);
        }

        public WorkbenchStatus CycleLength()
        {
            return CycleScalarAxis(Lengths, isWidth: false);
        }

        public WorkbenchStatus CycleFloors()
        {
            if (!EnsureParametric())
            {
                return NotApplicableToImported();
            }
            int[] candidates = _choice.Kind == BuildingChoiceKind.House ? HouseFloors : CottageFloors;
            int ni = NextIndex(candidates, _draft.Floors);
            _draft.Floors = candidates[ni];
            return AfterParamChange("楼层=" + _draft.Floors);
        }

        public WorkbenchStatus CycleWallHeight()
        {
            if (!EnsureParametric())
            {
                return NotApplicableToImported();
            }
            int ni = NextIndex(WallHeights, _draft.WallHeight);
            _draft.WallHeight = WallHeights[ni];
            return AfterParamChange("墙高(每层)=" + _draft.WallHeight);
        }

        public WorkbenchStatus CycleRoof()
        {
            if (!EnsureParametric())
            {
                return NotApplicableToImported();
            }
            int ni = NextIndexInt(RoofsAsInts(), (int)_draft.Roof);
            _draft.Roof = Roofs[ni];
            return AfterParamChange("屋顶=" + RoofName(_draft.Roof));
        }

        public WorkbenchStatus CycleSeedVariant()
        {
            if (!EnsureParametric())
            {
                return NotApplicableToImported();
            }
            int ni = NextIndex(Seeds, _draft.Seed);
            _draft.Seed = Seeds[ni];
            return AfterParamChange("随机种子=" + _draft.Seed);
        }

        public WorkbenchStatus RotateCw()
        {
            if (_choice == null)
            {
                return WorkbenchStatus.Fail("尚未选择建筑。");
            }
            _rotation = (_rotation + 1) % 4;
            MarkPreviewStale();
            Bump();
            _stage = WorkbenchStage.Configure;
            return WorkbenchStatus.Ok("朝向已旋转：" + (_rotation * 90) + "°（顺时针）。", _stage, true);
        }

        private WorkbenchStatus CycleScalarAxis(int[] table, bool isWidth)
        {
            if (!EnsureParametric())
            {
                return NotApplicableToImported();
            }
            int current = isWidth ? _draft.Width : _draft.Length;
            int ni = NextIndex(table, current);
            if (isWidth)
            {
                _draft.Width = table[ni];
            }
            else
            {
                _draft.Length = table[ni];
            }
            return AfterParamChange((isWidth ? "宽度=" : "长度=") + table[ni]);
        }

        private WorkbenchStatus AfterParamChange(string what)
        {
            _draftDirty = true;
            MarkPreviewStale();
            Bump();
            _stage = WorkbenchStage.Configure;
            return WorkbenchStatus.Ok(what + "（参数已变，需重新生成后才生效）。" + DescribeParams(), _stage, true);
        }

        private bool EnsureParametric()
        {
            return _choice != null && _choice.Kind != BuildingChoiceKind.Imported && _draft != null;
        }

        private WorkbenchStatus NotApplicableToImported()
        {
            return WorkbenchStatus.Fail("已导入蓝图为固定形状，尺寸/楼层/屋顶/种子参数不适用；如需改动请先在外部编辑蓝图再重新导入。");
        }

        // ============================ GENERATE ============================

        /// <summary>Compose the draft spec into a fresh blueprint. On failure the previous valid
        /// blueprint is left untouched (the whole point of the restore guarantee).</summary>
        public WorkbenchStatus Regenerate()
        {
            bool ok;
            string reason = RegenerateInternal(out ok);
            if (!ok)
            {
                return WorkbenchStatus.Fail("未能生成（已保留上一个有效建筑）：" + reason);
            }
            return WorkbenchStatus.Ok("已生成：" + DescribeParams(), _stage, true);
        }

        private string RegenerateInternal(out bool ok)
        {
            ok = true;
            if (_choice == null)
            {
                ok = false;
                return "尚未选择建筑。";
            }
            if (_choice.Kind == BuildingChoiceKind.Imported)
            {
                return null; // imported blueprint IS the shape; nothing to compose
            }
            if (!_draftDirty && _blueprint != null)
            {
                return null; // already fresh
            }

            BuildingGenResult result = BuildingComposer.Compose(_draft, _choice.DisplayName);
            if (result == null || !result.Success)
            {
                ok = false;
                if (result == null)
                {
                    return "生成器返回空结果。";
                }
                string specErr = result.SpecResult != null && !result.SpecResult.IsValid
                    ? result.SpecResult.JoinedErrors()
                    : null;
                string qcErr = result.QcResult != null && !result.QcResult.IsValid
                    ? result.QcResult.JoinedProblems()
                    : null;
                string reason = specErr != null ? specErr : (qcErr != null ? qcErr : "结构校验未通过。");
                // Note: deliberately NOT assigning result.Blueprint when QC failed, and NOT
                // clearing _draftDirty - the stale draft stays editable, the old good
                // blueprint (_blueprint) is preserved verbatim.
                return reason;
            }

            _blueprint = result.Blueprint;
            _draftDirty = false;
            InvalidatePreview();
            Bump();
            return null;
        }

        // ============================ PREVIEW (read-only) ============================

        /// <summary>
        /// Generate-if-needed (honouring the preserve-on-failure rule) and then compute the
        /// read-only footprint preview at <paramref name="origin"/> against <paramref name="gateway"/>.
        /// Never writes. On a failed generate the previous valid blueprint is still previewed,
        /// with the rejection reported, so the player always sees their current real structure.
        /// </summary>
        public WorkbenchStatus PreviewAt(
            CellPos origin, ITerrainGateway gateway, IBlockValueResolver resolver,
            out BuildPreviewModel model)
        {
            model = null;
            if (_choice == null)
            {
                return WorkbenchStatus.Fail("尚未选择建筑。");
            }

            bool genOk;
            string genReason = RegenerateInternal(out genOk);
            string note = genOk ? null : ("（参数未生效，仍预览当前有效建筑：" + genReason + "）");

            Blueprint effective = EffectiveBlueprint();
            if (effective == null)
            {
                return WorkbenchStatus.Fail("还没有可预览的建筑，请先选择并生成。");
            }

            BuildPreviewModel m = BuildPreviewComposer.Compose(effective, origin, resolver, gateway, false, false);
            model = m;
            _preview = m;
            _previewBlueprint = effective;
            _previewOrigin = origin;
            _previewVersion = _version;
            _hasPreview = true;
            _previewStale = false;
            _stage = WorkbenchStage.Previewed;

            string reason = m.Summarize() + (note ?? string.Empty);
            if (!m.Allowed)
            {
                return new WorkbenchStatus(true, reason, _stage, false); // preview shown but refused
            }
            return WorkbenchStatus.Ok(reason, _stage, false);
        }

        // ============================ CONFIRM / GATE ============================

        /// <summary>
        /// The final pure gate before the real build. Rejects: no preview; preview invalidated by a
        /// later change; a non-writable authority; or terrain that no longer admits the plan. On
        /// success returns the descriptor to hand to the unchanged execution pipeline.
        /// </summary>
        public WorkbenchStatus TryBeginBuild(
            WorldAuthority authority, ITerrainGateway gateway, IBlockValueResolver resolver,
            out BuildStartDescriptor descriptor)
        {
            descriptor = null;

            if (!TerrainAuthority.IsWritable(authority))
            {
                return WorkbenchStatus.Fail("权限不足：仅单人世界或联机房主可确认建造，联机客户端无权改动世界。");
            }

            if (!_hasPreview)
            {
                return WorkbenchStatus.Fail("尚未预览，无法确认建造。");
            }

            if (_previewStale || _version != _previewVersion)
            {
                ClearPreview();
                return WorkbenchStatus.Fail("参数或朝向在预览之后发生了改变，确认已失效，请重新预览后再建造。");
            }

            // Re-run the real planner against the CURRENT terrain: this is the "预览通过后、真正
            // 落建前必须用最新地形重新校验" guard. A chunk that unloaded, a player block placed
            // in the footprint, or a height change since the preview all fail here - and nothing
            // is written.
            BuildPlan plan = BuildPlanner.Plan(_previewBlueprint, _previewOrigin, resolver, gateway, false, false);
            if (!plan.Success)
            {
                ClearPreview();
                return WorkbenchStatus.Fail("建造前目标地形已变化或被占用，已拒绝施工：" + plan.FailureReason);
            }

            descriptor = new BuildStartDescriptor(_previewBlueprint, _previewOrigin, _choice.DisplayName, false);
            // Keep the preview so a follow-up could re-confirm, but the caller will immediately
            // hand the descriptor to the executor; the executor's own per-cell re-check still
            // applies (unchanged v0.6 pipeline).
            return WorkbenchStatus.Ok("已确认，可开始建造。", _stage, false);
        }

        /// <summary>Snapshot the current (now-built) configuration as the restore baseline.</summary>
        public void CommitApplied()
        {
            _hasApplied = true;
            _appliedChoice = _choice;
            _appliedDraft = _draft != null ? CopySpec(_draft) : null;
            _appliedRotation = _rotation;
            _appliedBlueprint = _blueprint;
        }

        // ============================ CANCEL / RESTORE ============================

        public WorkbenchStatus Cancel()
        {
            if (_hasApplied)
            {
                _choice = _appliedChoice;
                _draft = _appliedDraft != null ? CopySpec(_appliedDraft) : null;
                _rotation = _appliedRotation;
                _blueprint = _appliedBlueprint;
                _draftDirty = false;
                InvalidatePreview();
                Bump();
                _stage = WorkbenchStage.Configure;
                return WorkbenchStatus.Ok("已取消本次改动，还原到上一次确认的建筑。" + DescribeParams(), _stage, true);
            }

            _choice = null;
            _draft = null;
            _blueprint = null;
            _rotation = 0;
            _draftDirty = false;
            ClearPreview();
            _stage = WorkbenchStage.Idle;
            Bump();
            return WorkbenchStatus.Ok("已清空工作台。", _stage, true);
        }

        // ============================ HELPERS ============================

        private void InvalidatePreview()
        {
            ClearPreview();
        }

        private void MarkPreviewStale()
        {
            // Keep the last preview model so the UI can still show what was on screen, but
            // flag that the pending confirmation no longer matches it. TryBeginBuild refuses on
            // this flag (and the version token) so nothing stale is ever built.
            if (_hasPreview)
            {
                _previewStale = true;
            }
        }

        private void ClearPreview()
        {
            _hasPreview = false;
            _previewStale = false;
            _preview = null;
            _previewBlueprint = null;
            _previewOrigin = default(CellPos);
            _previewVersion = -1;
        }

        private void Bump()
        {
            _version++;
        }

        private static int NextIndex(int[] table, int current)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] == current)
                {
                    return (i + 1) % table.Length;
                }
            }
            // current not in table (e.g. a house default) -> clamp to nearest then advance
            return 0;
        }

        private static int NextIndexInt(int[] table, int current)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] == current)
                {
                    return (i + 1) % table.Length;
                }
            }
            return 0;
        }

        private static int[] RoofsAsInts()
        {
            return new int[] { (int)RoofType.Hip, (int)RoofType.Gabled, (int)RoofType.Flat };
        }

        private static string RoofName(RoofType roof)
        {
            switch (roof)
            {
                case RoofType.Gabled: return "人字顶(Gabled)";
                case RoofType.Flat: return "平顶(Flat)";
                default: return "四坡顶(Hip)";
            }
        }

        private string DescribeParams()
        {
            if (_choice == null)
            {
                return string.Empty;
            }
            if (_choice.Kind == BuildingChoiceKind.Imported)
            {
                Blueprint bp = _service != null ? _service.ImportedBlueprint : null;
                return bp == null
                    ? "（导入蓝图未载入）"
                    : " 尺寸 " + bp.SizeX + "×" + bp.SizeY + "×" + bp.SizeZ + "，共 " + bp.CellCount + " 方块。";
            }
            return " " + _draft.Width + "×" + _draft.Length + " 宽长，楼层 " + _draft.Floors +
                   "，每层墙高 " + _draft.WallHeight + "，" + RoofName(_draft.Roof) + "，种子 " + _draft.Seed + "。";
        }

        private static BuildingSpec CopySpec(BuildingSpec s)
        {
            return new BuildingSpec
            {
                Type = s.Type,
                Width = s.Width,
                Length = s.Length,
                Floors = s.Floors,
                WallHeight = s.WallHeight,
                Roof = s.Roof,
                Seed = s.Seed,
                HasChimney = s.HasChimney,
                WindowDensity = s.WindowDensity,
                Materials = s.Materials != null ? s.Materials.Clone() : null,
            };
        }
    }
}
