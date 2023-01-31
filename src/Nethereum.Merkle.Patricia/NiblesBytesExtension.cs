using System.Collections.Generic;



namespace Nethereum.Merkle.Patricia
{
    public static class NiblesBytesExtension
    {
        public static byte GetHighNibble(this byte value)
        {
            return (byte)((value >> 4) & 0x0F);
        }

        public static byte GetLowNibble(this byte value)
        {
            return (byte)(value & 0x0F);
        }

        public static byte[] ConvertToNibbles(this byte[] values)
        {
            var nibbles = new List<byte>();
            foreach(var value in values)
            {
                nibbles.Add(value.GetHighNibble());
                nibbles.Add(value.GetLowNibble());
            }
            return nibbles.ToArray();
        }

        public static byte[] FindAllTheSameBytesFromTheStart(this byte[] a, byte[] b)
        {
            var length = a.Length;
            if(a.Length > b.Length)
            {
                length = b.Length;
            }

            var matched = new List<byte>();
            for (int i = 0; i < length; i++)
            {
                if (a[i] == b[i])
                {
                    matched.Add(a[i]);
                }
                else
                {
                    return matched.ToArray();
                }
            }
            return matched.ToArray();
        }

        public static bool AreTheSame(this byte[] a, byte[] b)
        {
            if (a == null || b == null) return true;
            if (a == null) return false;
            if (b == null) return false;

            if (a.Length > b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        public static bool AreAllTheSameAsTheStartOf(this byte[] a, byte[] b)
        {
            if (b.Length > a.Length)
            {
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i])
                        return false;

                return true;
            }
            return false;
        }

    public static int HowManyBytesStartTheSame(this byte[] a, byte[] b)
    {
        var counter = 0;
        for (int i = 0; i < a.Length && i < b.Length; i++)
        {
            if (a[i] != b[i])
                break;
            counter++;
        }
    
         return counter;
     }



    public static byte[] ConvertFromNibbles(this byte[] values)
        {
            var converted = new List<byte>();
            for(int i = 0; i < values.Length; i = i + 2)
            {
                converted.Add((byte)(values[i] << 4 | values[i + 1]));
            }
            return converted.ToArray();
        }
    }
}
