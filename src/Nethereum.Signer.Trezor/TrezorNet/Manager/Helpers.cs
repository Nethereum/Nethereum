// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿using System;
using System.Text;

#if NETSTANDARD2_0
#endif

namespace Trezor.Net
{
    public static class Helpers
    {
        public static string ToHex(this byte[] address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            var sb = new StringBuilder();

            foreach (var b in address)
            {
#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable CA1304 // Specify CultureInfo
                _ = sb.Append(b.ToString("X2").ToLower());
#pragma warning restore CA1304 // Specify CultureInfo
#pragma warning restore CA1305 // Specify IFormatProvider
            }

            var hexString = sb.ToString();

            hexString = $"0x{hexString}";

            return hexString;
        }
    }
}
