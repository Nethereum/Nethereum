using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Rlpx
{
    internal static class RlpxStreamReader
    {
        public static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct = default)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, ct);
                if (read == 0) throw new IOException("Connection closed");
                offset += read;
            }
        }
    }
}
