using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Features.Generators
{
    /// <summary>
    /// A procedural building generator. It emits a terrain-independent
    /// <see cref="Blueprint"/> built purely from relative coordinates and
    /// composable structure modules - never hand-hardcoded world positions.
    /// </summary>
    internal interface IBuildingGenerator
    {
        string Key { get; }
        string DisplayName { get; }
        Blueprint Generate(GenerationContext context);
    }
}
