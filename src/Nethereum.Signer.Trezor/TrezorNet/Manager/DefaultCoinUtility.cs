// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;

namespace Trezor.Net
{
    public class DefaultCoinUtility : ICoinUtility
    {
        public static DefaultCoinUtility Instance { get; } = new DefaultCoinUtility();

        public Dictionary<uint, CoinInfo> Coins { get; } = new Dictionary<uint, CoinInfo>();

        public DefaultCoinUtility()
        {
            var assembly = typeof(TrezorManager).GetTypeInfo().Assembly;
            var stream = assembly.GetManifestResourceStream("Trezor.Net.Resources.Coins.xml");
            if (stream != null)
            {
                string xml;
                using (var reader = new StreamReader(stream))
                {
                    xml = reader.ReadToEnd();
                }
                var coinsList = DeserialiseObject<List<CoinInfo>>(xml);

                foreach (var coinInfo in coinsList)
                {
                    if (!Coins.ContainsKey(coinInfo.CoinType))
                    {
                        Coins.Add(coinInfo.CoinType, new CoinInfo(coinInfo.CoinName, AddressType.Bitcoin, coinInfo.IsSegwit, coinInfo.CoinType));
                    }
                }
            }

            Coins[43] = new CoinInfo(null, AddressType.NEM, false, 43);
            Coins[60] = new CoinInfo("Ethereum", AddressType.Ethereum, false, 60);
            Coins[61] = new CoinInfo("Ethereum Classic", AddressType.Ethereum, false, 61);
            Coins[1815] = new CoinInfo(null, AddressType.Cardano, false, 1815);
            Coins[148] = new CoinInfo(null, AddressType.Stellar, false, 148);
            Coins[1729] = new CoinInfo(null, AddressType.Tezoz, false, 1729);
        }

        public CoinInfo GetCoinInfo(uint coinNumber)
        {
            return Coins.TryGetValue(coinNumber, out var coinInfo)
                ? coinInfo
                : throw new NotImplementedException($"The coin number {coinNumber} has not been filled in for {nameof(DefaultCoinUtility)}. Please create a class that implements ICoinUtility, or call an overload that specifies coin information.");
        }

        public static TSerialiseType DeserialiseObject<TSerialiseType>(string objectXml)
        {
            var serializer = new XmlSerializer(typeof(TSerialiseType));
            using (var sr = new StringReader(objectXml))
            {
                var retVal = serializer.Deserialize(sr);
                return (TSerialiseType)retVal;
            }
        }
    }
}
