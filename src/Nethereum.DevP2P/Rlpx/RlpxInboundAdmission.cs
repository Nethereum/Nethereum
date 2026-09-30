using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Nethereum.DevP2P.Netutil;

namespace Nethereum.DevP2P.Rlpx
{
    public enum InboundRejectReason
    {
        None,

        PerIpCap,

        PerSubnetCap
    }

    public readonly struct InboundReservation
    {
        public InboundReservation(bool admitted, string? subnetKey, InboundRejectReason reason)
        {
            Admitted = admitted;
            SubnetKey = subnetKey;
            Reason = reason;
        }

        public bool Admitted { get; }

        public string? SubnetKey { get; }

        public InboundRejectReason Reason { get; }
    }

    public sealed class RlpxInboundAdmission
    {
        private readonly DevP2PConfig _config;
        private readonly ConcurrentDictionary<IPAddress, int> _inboundByIp = new();
        private readonly ConcurrentDictionary<string, int> _inboundBySubnet = new();

        public RlpxInboundAdmission(DevP2PConfig config)
        {
            _config = config;
        }

        public int CountInboundForIp(IPAddress ip)
            => _inboundByIp.TryGetValue(ip, out var n) ? n : 0;

        public InboundReservation TryReserve(IPAddress remoteIp)
        {
            if (!ConcurrentCounter.TryReserve(_inboundByIp, remoteIp, _config.MaxInboundPerIP))
                return new InboundReservation(false, null, InboundRejectReason.PerIpCap);

            string? subnetKey = GetSubnetKey(remoteIp);
            bool subnetLimited = subnetKey != null && _config.MaxInboundPerSubnet > 0;
            if (subnetLimited
                && !ConcurrentCounter.TryReserve(_inboundBySubnet, subnetKey!, _config.MaxInboundPerSubnet))
            {
                ConcurrentCounter.DecrementOrRemove(_inboundByIp, remoteIp);
                return new InboundReservation(false, null, InboundRejectReason.PerSubnetCap);
            }

            return new InboundReservation(true, subnetKey, InboundRejectReason.None);
        }

        public void Release(IPAddress remoteIp, string? subnetKey)
        {
            ConcurrentCounter.DecrementOrRemove(_inboundByIp, remoteIp);
            if (subnetKey != null)
                ConcurrentCounter.DecrementOrRemove(_inboundBySubnet, subnetKey);
        }

        private static string? GetSubnetKey(IPAddress ip)
        {
            var keyed = ip.NormalizeMappedV4();
            var bytes = keyed.GetAddressBytes();
            if (bytes.Length == 4)
                return "4:" + bytes[0] + "." + bytes[1] + "." + bytes[2];
            if (bytes.Length == 16)
                return "6:" + bytes[0] + "." + bytes[1] + "." + bytes[2] + "."
                            + bytes[3] + "." + bytes[4] + "." + bytes[5];
            return null;
        }
    }
}
