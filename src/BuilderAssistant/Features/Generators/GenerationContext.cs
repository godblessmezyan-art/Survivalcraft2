namespace BuilderAssistant.Features.Generators
{
    /// <summary>
    /// Parameters handed to a generator. Kept deliberately small for v0.5.
    /// The Seed / MaterialOverrideKey fields exist now so that Phase-4 features
    /// (random seeds, material substitution, building variants) can plug in
    /// without changing the generator contract. MaterialOverrideKey is
    /// intentionally null for v0.5 (no re-skinning yet).
    /// </summary>
    internal sealed class GenerationContext
    {
        public int Seed;
        public string MaterialOverrideKey; // reserved for future re-skinning

        public GenerationContext(int seed)
        {
            Seed = seed;
            MaterialOverrideKey = null;
        }
    }
}
