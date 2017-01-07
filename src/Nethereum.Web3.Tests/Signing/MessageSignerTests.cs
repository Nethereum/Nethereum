using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.ABI.Util;
using Nethereum.Core;
using Xunit;

namespace Nethereum.Web3.Tests.Signing
{
    public class MessageSignerTests
    {
      
            [Fact]
            public void ShouldRecoverSimple()
            {
                var signer = new MessageSigner();
                var account = signer.EcRecover("0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470".HexToByteArray(), "0xbd685c98ec39490f50d15c67ba2a8e9b5b1d6d7601fca80b295e7d717446bd8b7127ea4871e996cdc8cae7690408b4e800f60ddac49d2ad34180e68f1da0aaf001");
                Assert.Equal("0x8a3106a3e50576d4b6794a0e74d3bb5f8c9acaab", account.EnsureHexPrefix());
            }

            [Fact]
            public void ShouldRecoverGethPrefix()
            {
                //signed message using geth 1.5^
                var signature = "0x0976a177078198a261faf206287b8bb93ebb233347ab09a57c8691733f5772f67f398084b30fc6379ffee2cc72d510fd0f8a7ac2ee0162b95dc5d61146b40ffa1c";
                var text = "test";
                var hasher = new Sha3Keccack();
                var hash = hasher.CalculateHash(text);
                var byteList = new List<byte>();

                var bytePrefix = "0x19".HexToByteArray();
                var textBytePrefix = Encoding.UTF8.GetBytes("Ethereum Signed Message:\n" + hash.HexToByteArray().Length);
                var bytesMessage = hash.HexToByteArray();

                byteList.AddRange(bytePrefix);
                byteList.AddRange(textBytePrefix);
                byteList.AddRange(bytesMessage);
                var hashPrefix2 = hasher.CalculateHash(byteList.ToArray()).ToHex();

                var signer = new MessageSigner();

                var account = signer.EcRecover(hashPrefix2.HexToByteArray(), signature);

                Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c", account.EnsureHexPrefix());

                signature = signer.Sign(hashPrefix2.HexToByteArray(),
                    "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

                account = signer.EcRecover(hashPrefix2.HexToByteArray(), signature);

                Assert.Equal("0x12890d2cce102216644c59dae5baed380d84830c", account.EnsureHexPrefix());
            }
        

    }
}
