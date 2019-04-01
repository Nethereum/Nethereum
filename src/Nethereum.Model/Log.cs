using System.Collections.Generic;

namespace Nethereum.Model
{
    public class Log
    {
        public string Address { get; set; }
        public byte[] Data { get; set; }
        public List<byte[]> Topics { get; set; } = new List<byte[]>();

        public static Log Create(byte[] data, string address, params byte[][] topics)
        {
            var log = new Log(){Data = data, Address = address};
            log.Topics.AddRange(topics);
            return log;
        }

        public static Log Create(string address, params byte[][] topics)
        {
            return Create(null, address, topics);
        }
    }
}