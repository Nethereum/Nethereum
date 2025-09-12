using System;

namespace Nethereum.Wallet.UI.Components.Utils
{
    public static class IdenticonGenerator
    {
        public static string GetIdenticonText(string address)
        {
            if (string.IsNullOrEmpty(address)) return "?";
            
            return address.Length >= 4 ? address.Substring(2, 2).ToUpper() : "??";
        }

        public static string GetNetworkIdenticonText(string networkName)
        {
            if (string.IsNullOrEmpty(networkName)) return "?";
            
            if (networkName.Length >= 3)
                return networkName.Substring(0, 3).ToUpper();
            else if (networkName.Length >= 2)
                return networkName.Substring(0, 2).ToUpper();
            else
                return networkName.ToUpper().PadRight(2, '?');
        }

        public static string GetIdenticonStyle(string address)
        {
            if (string.IsNullOrEmpty(address)) 
                return "background: #ccc;";
            
            var addressBytes = address.ToLowerInvariant().Replace("0x", "");
            var hash1 = 0;
            var hash2 = 0;
            
            for (int i = 0; i < Math.Min(addressBytes.Length, 8); i++)
            {
                hash1 = hash1 * 31 + addressBytes[i];
                if (i + 8 < addressBytes.Length)
                    hash2 = hash2 * 31 + addressBytes[i + 8];
            }
            
            var hue = Math.Abs(hash1) % 360;
            var saturation = 65 + (Math.Abs(hash2) % 20);
            var lightness = 45 + (Math.Abs(hash1 >> 8) % 20);
            
            return $"background: hsl({hue}, {saturation}%, {lightness}%); color: white; font-weight: 600; font-size: 1.1rem;";
        }
    }
}