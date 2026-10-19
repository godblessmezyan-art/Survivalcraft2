using System.Collections.Generic;

namespace BuilderAssistant.Features.Generators
{
    /// <summary>
    /// Registry of available generators. The build tool currently stamps the
    /// first/default generator; adding more (or a picker UI) later requires no
    /// change to the execution path. This is the seam for "building
    /// composition" in a future phase.
    /// </summary>
    internal static class BuildingGeneratorRegistry
    {
        private static readonly List<IBuildingGenerator> _generators = new List<IBuildingGenerator>
        {
            // Index 0 = registry Default = the hand-tuned medieval cottage. It MUST stay
            // first so the shipped build tool and the existing 267 offline tests keep stamping
            // the exact same structure. The parametric engine is additive, never a replacement.
            new MedievalCottageGenerator(),

            // New parametric options (resolvable by key; not the default).
            new ParametricCottageGenerator(),  // "cottage_param"
            new TwoStoryHouseGenerator(),      // "house_two_story"
        };

        public static IReadOnlyList<IBuildingGenerator> All { get { return _generators; } }

        public static IBuildingGenerator Default { get { return _generators[0]; } }

        public static IBuildingGenerator Find(string key)
        {
            for (int i = 0; i < _generators.Count; i++)
            {
                if (_generators[i].Key == key)
                {
                    return _generators[i];
                }
            }
            return null;
        }
    }
}
