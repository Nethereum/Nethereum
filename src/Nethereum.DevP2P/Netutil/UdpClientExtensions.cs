using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Nethereum.DevP2P.Netutil
{
    public static class UdpClientExtensions
    {
        private const int SioUdpConnReset = -1744830452;

        public static void IgnoreIcmpConnectionReset(this UdpClient udp)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            try { udp.Client.IOControl(SioUdpConnReset, new byte[] { 0 }, null); }
            catch (Exception) { }
        }
    }
}
