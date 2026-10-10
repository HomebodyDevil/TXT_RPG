namespace TxTRPG.Gameplay
{
    // Shared random contract for gameplay systems that pick one index out of a list (dice faces, slot reel cells).
    // Systems depend on this instead of each other's random types, so the dice and slot machine domains stay separate.
    public interface IRandomIndexProvider
    {
        int NextIndex(int exclusiveUpperBound);
    }

    public sealed class SystemRandomIndexProvider : IRandomIndexProvider
    {
        private readonly System.Random random;

        public SystemRandomIndexProvider() : this(new System.Random()) { }
        public SystemRandomIndexProvider(int seed) : this(new System.Random(seed)) { }
        private SystemRandomIndexProvider(System.Random source) => random = source;

        public int NextIndex(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0) throw new System.ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
            return random.Next(exclusiveUpperBound);
        }
    }
}
