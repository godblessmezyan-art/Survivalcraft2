using System.Collections.Generic;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;

namespace BuilderAssistant.Core.Workbench
{
    /// <summary>The kind of a workbench building choice.</summary>
    internal enum BuildingChoiceKind
    {
        /// <summary>A parametric cottage built from a <see cref="BuildingSpec"/>.</summary>
        Cottage,
        /// <summary>A parametric two-storey house built from a <see cref="BuildingSpec"/>.</summary>
        House,
        /// <summary>A blueprint the player imported/copied this session (params are read-only).</summary>
        Imported,
        FurnishedHome,
    }

    /// <summary>A selectable entry in the workbench: a display name + a way to seed the spec.</summary>
    internal sealed class BuildingChoice
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly BuildingChoiceKind Kind;

        public BuildingChoice(string key, string displayName, BuildingChoiceKind kind)
        {
            Key = key;
            DisplayName = displayName;
            Kind = kind;
        }
    }

    /// <summary>
    /// The game-free catalog the workbench builds against. It exposes exactly the buildings
    /// the task requires (a cottage, a two-storey house, and an optional imported blueprint),
    /// and produces the correct starting <see cref="BuildingSpec"/> for the parametric choices
    /// (or the imported blueprint itself). It does NOT touch the generator registry or the
    /// world, so the same list is what the offline tests assert and what the device shows -
    /// no drift between the two. Adding a building later is a one-line entry here plus, if it
    /// is parametric, a <see cref="BuildingType"/>; no execution path changes.
    /// </summary>
    internal sealed class BuildingSelectionService
    {
        private Blueprint _importedBlueprint;

        /// <summary>Fixed parametric choices. The imported slot appears only when loaded.</summary>
        public IReadOnlyList<BuildingChoice> Choices
        {
            get
            {
                var list = new List<BuildingChoice>
                {
                    new BuildingChoice("workbench_cottage", "小屋 (Cottage)", BuildingChoiceKind.Cottage),
                    new BuildingChoice("workbench_house", "两层住宅 (Two-Story House)", BuildingChoiceKind.House),
                    new BuildingChoice("workbench_furnished", "一室一厅（自动家具）", BuildingChoiceKind.FurnishedHome),
                };
                if (_importedBlueprint != null)
                {
                    string name = string.IsNullOrEmpty(_importedBlueprint.Name)
                        ? "已导入蓝图"
                        : ("已导入蓝图「" + _importedBlueprint.Name + "」");
                    list.Add(new BuildingChoice("workbench_imported", name, BuildingChoiceKind.Imported));
                }
                return list;
            }
        }

        /// <summary>The currently loaded imported blueprint, or null.</summary>
        public Blueprint ImportedBlueprint { get { return _importedBlueprint; } }

        /// <summary>Load (or clear with null) the imported blueprint offered as a choice.</summary>
        public void SetImportedBlueprint(Blueprint blueprint)
        {
            _importedBlueprint = blueprint;
        }

        /// <summary>
        /// Produce a fresh default <see cref="BuildingSpec"/> for a parametric choice. Returns
        /// false for the imported kind (it is driven by the blueprint, not a spec).
        /// </summary>
        public bool TryMakeDefaultSpec(BuildingChoice choice, out BuildingSpec spec)
        {
            spec = null;
            if (choice == null)
            {
                return false;
            }
            switch (choice.Kind)
            {
                case BuildingChoiceKind.Cottage:
                    spec = BuildingSpec.DefaultCottage();
                    return true;
                case BuildingChoiceKind.House:
                    spec = BuildingSpec.DefaultTwoStoryHouse();
                    return true;
                case BuildingChoiceKind.FurnishedHome:
                    spec = BuildingSpec.DefaultFurnishedHome();
                    return true;
                case BuildingChoiceKind.Imported:
                default:
                    return false;
            }
        }

        public BuildingChoice Find(string key)
        {
            IReadOnlyList<BuildingChoice> all = Choices;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Key == key)
                {
                    return all[i];
                }
            }
            return null;
        }
    }
}
