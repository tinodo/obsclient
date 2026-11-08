namespace OBSStudioClient.Tests.Simulation
{
    /// <summary>
    /// The one source of randomness of a simulated OBS Studio. It is a small generator of its own (SplitMix64) and not
    /// <see cref="Random"/>, because the sequence of a seeded <see cref="Random"/> is not promised to stay the same between versions of
    /// .NET, and a seed that fails on one target framework has to fail in the same way on the other one.
    /// </summary>
    internal sealed class SimRandom
    {
        private ulong _state;

        public SimRandom(int seed)
        {
            _state = ((ulong)(uint)seed * 0x9E3779B97F4A7C15UL) ^ 0x632BE59BD9B4E019UL;
        }

        public ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Returns a number from 0 up to, but not including, <paramref name="maxExclusive"/>.</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            return (int)(NextUInt64() % (ulong)maxExclusive);
        }

        /// <summary>Returns a number from <paramref name="minInclusive"/> up to, but not including, <paramref name="maxExclusive"/>.</summary>
        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            return minInclusive + Next(maxExclusive - minInclusive);
        }

        /// <summary>Returns a number from 0 up to, but not including, 1.</summary>
        public double NextDouble()
        {
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }

        /// <summary>Returns true with the given probability.</summary>
        public bool Chance(double probability)
        {
            return probability > 0 && NextDouble() < probability;
        }

        /// <summary>Returns a version 4 UUID, which is the form that OBS Studio gives to its sources.</summary>
        public Guid NextGuid()
        {
            var bytes = new byte[16];
            BitConverter.TryWriteBytes(bytes.AsSpan(0, 8), NextUInt64());
            BitConverter.TryWriteBytes(bytes.AsSpan(8, 8), NextUInt64());

            // In the layout of System.Guid, the version is in the high nibble of byte 7 and the variant in the high bits of byte 8.
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            return new Guid(bytes);
        }

        /// <summary>Puts the items in a random order (Fisher-Yates).</summary>
        public void Shuffle<T>(IList<T> items)
        {
            for (var index = items.Count - 1; index > 0; index--)
            {
                var other = Next(index + 1);
                (items[index], items[other]) = (items[other], items[index]);
            }
        }
    }
}
