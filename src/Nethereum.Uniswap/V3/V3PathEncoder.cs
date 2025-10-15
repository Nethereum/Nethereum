using System.Collections.Generic;

namespace Nethereum.Uniswap.V3
{
    public class V3PathEncoder
    {
        public static byte[] EncodePath(string addressToken0, int fee, string addressToken1)
        {
            var abiEncoder = new ABI.ABIEncode();
            return abiEncoder.GetABIEncodedPacked(new ABI.ABIValue("address", addressToken0), new ABI.ABIValue("uint24", fee),
                new ABI.ABIValue("address", addressToken1));
        }

        public static byte[] EncodePath(string addressToken0, int poolFee0, string addressToken1, int poolfee1, string addressToken2)
        {
            var abiEncoder = new ABI.ABIEncode();
            return abiEncoder.GetABIEncodedPacked(new ABI.ABIValue("address", addressToken0), new ABI.ABIValue("uint24", poolFee0),
                new ABI.ABIValue("address", addressToken1), new ABI.ABIValue("uint24", poolfee1), new ABI.ABIValue("address", addressToken2));
        }

        public class PathPoolFee
        {
            public string AddressToken { get; set; }
            public int PoolFee { get; set; }
        }

        /// <summary>
        /// Encode complex path with pool fees, input token and fee, then output token with fee of next pool, etc
        /// Example: DAI, 3000, USDC, 500, USDT
        /// pathPoolFee = new PathPoolFee[] { new PathPoolFee { AddressToken = "DAI", PoolFee = 3000 }, new PathPoolFee{ AddressToken = "USDC", Poolfee = 500 } }, "USDT"
        /// </summary>
        public static byte[] EncodePathPoolFee(PathPoolFee[] pathPoolFee, string  addressTokenOuput)
        {
            var abiEncoder = new ABI.ABIEncode();
            var values = new List<ABI.ABIValue>();
            foreach (var path in pathPoolFee)
            {
                values.Add(new ABI.ABIValue("address", path.AddressToken));
                values.Add(new ABI.ABIValue("uint24", path.PoolFee));
            }
            values.Add(new ABI.ABIValue("address", addressTokenOuput));
            return abiEncoder.GetABIEncodedPacked(values.ToArray());
        }
    }
}
