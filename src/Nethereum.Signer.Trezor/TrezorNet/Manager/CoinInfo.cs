// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿namespace Trezor.Net
{
    public class CoinInfo
    {
        public uint CoinType { get; set; }
        public string CoinName { get; set; }
        public AddressType AddressType { get; set; }
        public bool IsSegwit { get; set; }

        /// <summary>
        /// Serialization only constructor
        /// </summary>
        public CoinInfo()
        {

        }

        public CoinInfo(string coinName, AddressType addressType, bool isSegwit, uint cointType)
        {
            CoinName = coinName;
            AddressType = addressType;
            IsSegwit = isSegwit;
            CoinType = cointType;
        }
    }
}
