
namespace Nethereum.Util.Random
{

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Security.Cryptography;

    public class GenericShuffler<T>
    {
#if NET6_0_OR_GREATER
        private static readonly Random _random = Random.Shared;
#else
        private static readonly Random _random = new Random();
#endif

#if NET6_0_OR_GREATER
    // .NET 6+ has built-in secure random
    private int SecureRandom(int maxExclusive)
    {
        return RandomNumberGenerator.GetInt32(maxExclusive);
    }
#else
        // Compatible with .NET Standard 2.0 and .NET Framework
        private static readonly RandomNumberGenerator _secureRng = RandomNumberGenerator.Create();

        private int SecureRandom(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));

            byte[] buffer = new byte[4];
            int result;
            do
            {
                _secureRng.GetBytes(buffer);
                result = BitConverter.ToInt32(buffer, 0) & int.MaxValue;
            } while (result >= (int.MaxValue - int.MaxValue % maxExclusive));

            return result % maxExclusive;
        }
#endif

        public List<T> ShuffleOnce(List<T> items)
        {
            if (items == null || items.Count < 3)
                throw new ArgumentException("List must have at least 3 items to shuffle.");

            int countToShuffle = items.Count / 3;
            List<T> selectedItems = items.OrderBy(_ => SecureRandom(int.MaxValue)).Take(countToShuffle).ToList();
            List<T> remainingItems = items.Except(selectedItems).ToList();

            foreach (var item in selectedItems.OrderBy(_ => SecureRandom(int.MaxValue)))
            {
                int insertIndex = SecureRandom(remainingItems.Count + 1);
                remainingItems.Insert(insertIndex, item);
            }

            return remainingItems;
        }

        public List<T> ShuffleMultipleTimes(List<T> items, int times)
        {
            var shuffledList = new List<T>(items);
            for (int i = 0; i < times; i++)
            {
                shuffledList = ShuffleOnce(shuffledList);
            }
            return shuffledList;
        }

        public List<T> ShuffleRandomTimes(List<T> items)
        {
            int times = (SecureRandom(5) + 1) * (SecureRandom(3) + 1) / 2; // Roughly 1 to 10
            return ShuffleMultipleTimes(items, times);
        }
    }

}
