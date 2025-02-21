using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nethereum.Util.Random
{

    public class GenericShuffler<T>
    {
        private readonly System.Random _random = new System.Random();

        public List<T> ShuffleOnce(List<T> items)
        {
            if (items == null || items.Count < 3)
                throw new ArgumentException("List must have at least 3 items to shuffle.");

            int countToShuffle = items.Count / 3;
            List<T> selectedItems = items.OrderBy(_ => _random.Next()).Take(countToShuffle).ToList();
            List<T> remainingItems = items.Except(selectedItems).ToList();

            foreach (var item in selectedItems.OrderBy(_ => _random.Next()))
            {
                int insertIndex = (_random.Next(1, 10) * _random.Next(1, 10)) % (remainingItems.Count + 1);
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
            int times = (_random.Next(1, 6) * _random.Next(1, 4)) / 2; // Shuffle between 1 and 10 times, roughly
            return ShuffleMultipleTimes(items, times);
        }
    }
}
