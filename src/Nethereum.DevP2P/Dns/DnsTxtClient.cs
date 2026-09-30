using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Dns
{
    internal sealed class DnsTxtClient
    {
        private static readonly IPEndPoint[] PublicFallbackDnsServers = new[]
        {
            new IPEndPoint(IPAddress.Parse("1.1.1.1"), 53),
            new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53),
            new IPEndPoint(IPAddress.Parse("9.9.9.9"), 53)
        };

        private static readonly Lazy<IPEndPoint[]> DnsServers =
            new Lazy<IPEndPoint[]>(GetDnsServers);

        private static IPEndPoint[] GetDnsServers()
        {
            var os = new List<IPEndPoint>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    var dns = nic.GetIPProperties().DnsAddresses;
                    foreach (var addr in dns)
                    {
                        if (addr.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(addr)) continue;
                        var ep = new IPEndPoint(addr, 53);
                        if (!os.Any(e => e.Address.Equals(ep.Address))) os.Add(ep);
                    }
                }
            }
            catch
            {
            }
            foreach (var fb in PublicFallbackDnsServers)
            {
                if (!os.Any(e => e.Address.Equals(fb.Address))) os.Add(fb);
            }
            return os.ToArray();
        }

        public async Task<List<string>> QueryTxtAsync(
            string domain, TimeSpan timeout, CancellationToken ct)
        {
            Exception last = null;
            foreach (var server in DnsServers.Value)
            {
                try
                {
                    return await QueryTxtOnceAsync(domain, server, timeout, ct);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    last = ex;
                }
            }
            throw last ?? new InvalidOperationException("No DNS server reachable");
        }

        private static async Task<List<string>> QueryTxtOnceAsync(
            string domain, IPEndPoint server, TimeSpan timeout, CancellationToken ct)
        {
            var (query, queryId) = BuildTxtQuery(domain);
            using var udp = new UdpClient();
            await udp.SendAsync(query, query.Length, server);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            var result = await udp.ReceiveAsync().WaitAsync(cts.Token);
            return ParseTxtResponse(result.Buffer, queryId);
        }

        private static (byte[] Query, ushort Id) BuildTxtQuery(string domain)
        {
            var ms = new System.IO.MemoryStream();
            var idBytes = new byte[2];
            System.Security.Cryptography.RandomNumberGenerator.Fill(idBytes);
            ushort id = (ushort)((idBytes[0] << 8) | idBytes[1]);
            ms.WriteByte((byte)(id >> 8));
            ms.WriteByte((byte)id);
            ms.WriteByte(0x01); ms.WriteByte(0x00);
            ms.WriteByte(0x00); ms.WriteByte(0x01);
            ms.WriteByte(0x00); ms.WriteByte(0x00);
            ms.WriteByte(0x00); ms.WriteByte(0x00);
            ms.WriteByte(0x00); ms.WriteByte(0x00);
            foreach (var label in domain.Split('.'))
            {
                var bytes = Encoding.ASCII.GetBytes(label);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }
            ms.WriteByte(0x00);
            ms.WriteByte(0x00); ms.WriteByte(0x10);
            ms.WriteByte(0x00); ms.WriteByte(0x01);
            return (ms.ToArray(), id);
        }

        private static List<string> ParseTxtResponse(byte[] response, ushort expectedId)
        {
            if (response == null || response.Length < 12)
                throw new InvalidOperationException(
                    $"DNS response too short ({response?.Length ?? 0} bytes, header needs 12)");

            int responseId = (response[0] << 8) | response[1];
            if (responseId != expectedId)
                throw new InvalidOperationException(
                    $"DNS transaction-id mismatch (expected 0x{expectedId:X4}, got 0x{responseId:X4}) - possible spoofed reply");

            int offset = 12;
            offset = SkipName(response, offset);
            if (offset + 4 > response.Length)
                throw new InvalidOperationException("DNS response truncated after question");
            offset += 4;

            int ancount = (response[6] << 8) | response[7];
            var results = new List<string>();
            for (int i = 0; i < ancount; i++)
            {
                offset = SkipName(response, offset);
                if (offset + 10 > response.Length)
                    throw new InvalidOperationException("DNS response truncated in answer header");
                int type = (response[offset] << 8) | response[offset + 1];
                offset += 8;
                int rdlength = (response[offset] << 8) | response[offset + 1];
                offset += 2;
                if (rdlength < 0 || offset + rdlength > response.Length)
                    throw new InvalidOperationException("DNS response truncated in rdata");

                if (type == 16)
                {
                    int end = offset + rdlength;
                    var sb = new StringBuilder();
                    while (offset < end)
                    {
                        int len = response[offset++];
                        if (offset + len > end)
                            throw new InvalidOperationException("DNS TXT character-string overruns rdata");
                        sb.Append(Encoding.ASCII.GetString(response, offset, len));
                        offset += len;
                    }
                    results.Add(sb.ToString());
                }
                else
                {
                    offset += rdlength;
                }
            }
            return results;
        }

        private static int SkipName(byte[] data, int offset)
        {
            while (true)
            {
                if (offset >= data.Length)
                    throw new InvalidOperationException("DNS name overruns response");
                byte b = data[offset];
                if (b == 0) return offset + 1;
                if ((b & 0xC0) == 0xC0)
                {
                    if (offset + 1 >= data.Length)
                        throw new InvalidOperationException("DNS compression pointer truncated");
                    return offset + 2;
                }
                offset += b + 1;
            }
        }
    }
}
