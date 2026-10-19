using System;
using BuilderAssistant.Core.Blueprints;
using BuilderAssistant.Core.Building;

namespace BuilderAssistant.Features.Generators
{
    /// <summary>
    /// Thin <see cref="IBuildingGenerator"/> adapter over the parametric engine. It keeps
    /// the existing generator contract intact (so the build tool and the registry are
    /// unchanged) while producing blueprints from a <see cref="BuildingSpec"/> rather than
    /// hand-written coordinates. The default spec is the familiar 9x11 single-storey cottage
    /// with a hip roof, so this generator reproduces the shape users already know; changing a
    /// handful of spec fields (or the seed) yields different sizes / roofs / window layouts.
    ///
    /// This is NOT wired to replace <c>MedievalCottageGenerator</c>, which remains the
    /// registry default so the shipped 267-test cottage entry keeps behaving exactly as
    /// before. This adapter is the additive, parametric path.
    /// </summary>
    internal sealed class ParametricCottageGenerator : IBuildingGenerator
    {
        public string Key { get { return "cottage_param"; } }
        public string DisplayName { get { return "参数化小屋 (Cottage)"; } }

        /// <summary>Optional override so a caller (UI / future AI) can inject a custom spec.
        /// When null, <see cref="BuildingSpec.DefaultCottage"/> is used.</summary>
        public Func<BuildingSpec> SpecFactory;

        public ParametricCottageGenerator()
        {
            SpecFactory = null;
        }

        public Blueprint Generate(GenerationContext context)
        {
            BuildingSpec spec = SpecFactory != null ? SpecFactory() : BuildingSpec.DefaultCottage();
            if (context != null)
            {
                spec.Seed = context.Seed;
            }
            BuildingGenResult result = BuildingComposer.Compose(spec, "Parametric Cottage");
            return result.Blueprint;
        }
    }
}
