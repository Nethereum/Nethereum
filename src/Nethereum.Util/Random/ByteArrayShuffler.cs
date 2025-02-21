using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Util.Random
{
    public class ByteArrayShuffler
    {
        public GenericShuffler<byte> Shuffler { get; }

        public ByteArrayShuffler()
        {
            Shuffler = new GenericShuffler<byte>();
        }

        public byte[] ShuffleRandomTimes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 3)
                throw new ArgumentException("Array must have at least 3 items to shuffle.");
            List<byte> byteList = bytes.ToList();
            byteList = Shuffler.ShuffleRandomTimes(byteList);
            return byteList.ToArray();
        }

        public byte[] ShuffleMultipleTimes(byte[] bytes, int times = 7)
        {
            if (bytes == null || bytes.Length < 3)
                throw new ArgumentException("Array must have at least 3 items to shuffle.");
            List<byte> byteList = bytes.ToList();
            byteList = Shuffler.ShuffleMultipleTimes(byteList, times);
            return byteList.ToArray();
        }

        public byte[] ShuffleOnce(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 3)
                throw new ArgumentException("Array must have at least 3 items to shuffle.");
            List<byte> byteList = bytes.ToList();
            byteList = Shuffler.ShuffleOnce(byteList);
            return byteList.ToArray();
        }
    }
}
