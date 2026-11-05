using System.IO;
using System.Linq;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    internal static class NestedListFlood
    {
        public const int PayloadBytes = 4 * 1024 * 1024;

        public static byte[] BudgetPassing(int targetBytes)
        {
            var chain = new byte[] { 0xc0 };
            for (var i = 1; i < 54; i++)
                chain = new[] { (byte)(0xc0 + chain.Length) }.Concat(chain).ToArray();
            var block = chain.Concat(new byte[] { 0x93 }).Concat(Enumerable.Repeat((byte)0xaa, 19)).ToArray();

            var count = (targetBytes - 16) / block.Length;
            var content = new MemoryStream(count * block.Length);
            for (var i = 0; i < count; i++) content.Write(block, 0, block.Length);

            var inner = RLP.RLP.EncodeList(content.ToArray());
            return RLP.RLP.EncodeList(new byte[] { 0x01 }, inner);
        }
    }
}
