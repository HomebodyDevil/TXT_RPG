using System;

namespace TxTRPG.Gameplay.Dice
{
    // Kept for the dice API; the contract itself is the shared IRandomIndexProvider.
    public interface IRandomIndexSource : IRandomIndexProvider
    {
    }

    public sealed class SystemRandomIndexSource : IRandomIndexSource
    {
        private readonly Random random;

        public SystemRandomIndexSource()
            : this(new Random())
        {
        }

        public SystemRandomIndexSource(int seed)
            : this(new Random(seed))
        {
        }

        private SystemRandomIndexSource(Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public int NextIndex(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
            }

            return random.Next(exclusiveUpperBound);
        }
    }
}
