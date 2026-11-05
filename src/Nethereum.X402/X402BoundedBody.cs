using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.X402;

internal static class X402BoundedBody
{
    public static async Task<string> ReadAsync(HttpResponseMessage response, int limit, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > limit)
            throw Exceeds(limit);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
                throw Exceeds(limit);

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    public static string Bound(string value, int limit) =>
        value.Length > limit ? throw Exceeds(limit) : value;

    private static InvalidDataException Exceeds(int limit) => new($"The response exceeds {limit} bytes.");
}
