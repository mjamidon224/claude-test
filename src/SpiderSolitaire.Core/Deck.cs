namespace SpiderSolitaire.Core;

/// <summary>Builds and shuffles the 104 cards for a game.</summary>
public static class Deck
{
    public const int CardCount = 104;
    public const int CardsPerSuit = 13;

    /// <summary>
    /// The card an id stands for. Ids run 0-103 in eight runs of Ace to King; the
    /// difficulty decides which suit each run is, so a saved game only needs the ids.
    /// </summary>
    public static Card CardFromId(int id, Difficulty difficulty)
    {
        if (id is < 0 or >= CardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Card ids run from 0 to 103.");
        }

        int run = id / CardsPerSuit;
        Suit suit = difficulty switch
        {
            Difficulty.OneSuit => Suit.Spades,
            Difficulty.TwoSuits => run % 2 == 0 ? Suit.Spades : Suit.Hearts,
            _ => (Suit)(run % 4),
        };

        return new Card(id, suit, id % CardsPerSuit + 1);
    }

    /// <summary>All 104 cards in an order fixed by <paramref name="seed"/>, so a deal can be replayed.</summary>
    public static IReadOnlyList<Card> Shuffled(Difficulty difficulty, int seed)
    {
        Card[] cards = new Card[CardCount];
        for (int id = 0; id < CardCount; id++)
        {
            cards[id] = CardFromId(id, difficulty);
        }

        // Fisher-Yates with a generator defined here rather than System.Random, whose
        // sequence for a given seed is not a documented guarantee across .NET versions.
        SplitMix64 random = new((ulong)(uint)seed);
        for (int i = cards.Length - 1; i > 0; i--)
        {
            int j = random.NextInt(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return cards;
    }

    private struct SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed) => _state = seed;

        private ulong Next()
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform in [0, exclusiveMax); the modulo bias at these sizes is around 1e-17.</summary>
        public int NextInt(int exclusiveMax) => (int)(Next() % (ulong)exclusiveMax);
    }
}
