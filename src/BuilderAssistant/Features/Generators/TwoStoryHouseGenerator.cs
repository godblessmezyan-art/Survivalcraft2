using System;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;

namespace BuilderAssistant.Features.Generators
{
    /// <summary>
    /// A NEW building type produced entirely through the parametric engine - no bespoke
    /// coordinate code. The default spec is a two-storey, gabled house: real floor slabs per
    /// storey, a load-bearing exterior shell with a corner support beam, a walkable switchback
    /// stairwell that punches a landing void through the upper slab, windows on every face,
    /// a proper gable silhouette, and simple interior furniture. It is a genuine two-level
    /// structure, not two hollow boxes stacked.
    ///
    /// Everything here is built from blocks whose behaviour is already confirmed on the
    /// target assembly (planks/stone cubes, wooden stairs, stone slabs, wooden fences, and the
    /// two-cell wooden door). Stairs and the door rely on the same schema-v2 oriented path the
    /// cottage already uses; their on-device Android rendering stays marked 待验证 until proven
    /// in the live client - we do not claim compatibility we have not tested.
    /// </summary>
    internal sealed class TwoStoryHouseGenerator : IBuildingGenerator
    {
        public string Key { get { return "house_two_story"; } }
        public string DisplayName { get { return "两层住宅 (Two-Story House)"; } }

        /// <summary>Optional custom spec; null falls back to <see cref="BuildingSpec.DefaultTwoStoryHouse"/>.</summary>
        public Func<BuildingSpec> SpecFactory;

        public TwoStoryHouseGenerator()
        {
            SpecFactory = null;
        }

        public Blueprint Generate(GenerationContext context)
        {
            BuildingSpec spec = SpecFactory != null ? SpecFactory() : BuildingSpec.DefaultTwoStoryHouse();
            if (context != null)
            {
                spec.Seed = context.Seed;
            }
            BuildingGenResult result = BuildingComposer.Compose(spec, "Two-Story House");
            return result.Blueprint;
        }
    }
}
