using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class Eip155SignerTests
    {
        [Fact]
        // ported from the main example in the spec:
        // https://github.com/ethereum/EIPs/blob/master/EIPS/eip-155.md
        public void BasicSigning()
        {
            var nonce = 9.ToBytesForRLPEncoding();
            var gasPrice = BigInteger.Parse("20" + "000" + "000" + "000").ToBytesForRLPEncoding();
            var gasLimit = 21000.ToBytesForRLPEncoding();
            var to = "0x3535353535353535353535353535353535353535".HexToByteArray();
            var amount = BigInteger.Parse("1" + "000" + "000" + "000" +
                                          "000" + "000" + "000").ToBytesForRLPEncoding();
            var data = "".HexToByteArray();
            byte[] ZERO_BYTE_ARRAY = 0.ToBytesForRLPEncoding();

            var chain = 1;
            var chainId = ((int)chain).ToBytesForRLPEncoding();

            //Create a transaction from scratch
            var tx = new TransactionChainId(nonce, gasPrice, gasLimit, to, amount, data ,chainId);

            Assert.Equal(chainId, tx.ChainId);
            Assert.Equal(ZERO_BYTE_ARRAY, tx.RHash);
            Assert.Equal(ZERO_BYTE_ARRAY, tx.SHash);

            var expectedSigningData =
                "ec098504a817c800825208943535353535353535353535353535353535353535880de0b6b3a764000080018080";
            Assert.Equal(expectedSigningData, tx.GetRLPEncodedRaw().ToHex());

            var expectedSigningHash = "daf5a779ae972f972197303d7b574746c7ef83eadac0f2791ad23db92e4c8e53";
            Assert.Equal(expectedSigningHash, tx.RawHash.ToHex());

            var privateKey = "4646464646464646464646464646464646464646464646464646464646464646";
            tx.Sign(new EthECKey(privateKey));

            var rFromSignature = tx.Signature.R.ToBigIntegerFromRLPDecoded();
            var expectedRFromSignature = "18515461264373351373200002665853028612451056578545711640558177340181847433846";
            Assert.Equal(expectedRFromSignature, rFromSignature.ToString());

            var sFromSignature = tx.Signature.S.ToBigIntegerFromRLPDecoded();
            var expectedSFromSignature = "46948507304638947509940763649030358759909902576025900602547168820602576006531";
            Assert.Equal(expectedSFromSignature, sFromSignature.ToString());

            byte[] vFromSignature = tx.Signature.V;

            Assert.Equal(37, vFromSignature.ToIntFromRLPDecoded());

            var expectedSignedTx = "f86c098504a817c800825208943535353535353535353535353535353535353535880de0b6b3a76400008025a028ef61340bd939bc2195fe537567866003e1a15d3c71ff63e1590620aa636276a067cbe9d8997f761aecb703304b3800ccf555c9f3dc64214b297fb1966a3b6d83";
            Assert.Equal(expectedSignedTx.Length, tx.GetRLPEncoded().ToHex().Length);
            Assert.Equal(expectedSignedTx, tx.GetRLPEncoded().ToHex());
        }
    }
}