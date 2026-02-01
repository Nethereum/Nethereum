using System.Collections.Generic;

namespace Nethereum.Signer.UnitTests
{
    public static class GethTransactionTestVectors
    {
        public class TransactionTestCase
        {
            public string Name { get; set; }
            public string TxBytes { get; set; }
            public string ExpectedSender { get; set; }
            public string ExpectedHash { get; set; }
        }

        public static IEnumerable<object[]> GetSignatureTestCases()
        {
            yield return new object[] { new TransactionTestCase {
                Name = "Vitalik_1",
                TxBytes = "0xf864808504a817c800825208943535353535353535353535353535353535353535808025a0044852b2a670ade5407e78fb2863c51de9fcb96542a07186fe3aeda6bb8a116da0044852b2a670ade5407e78fb2863c51de9fcb96542a07186fe3aeda6bb8a116d",
                ExpectedSender = "0xf0f6f18bca1b28cd68e4357452947e021241e9ce",
                ExpectedHash = "0xb1e2188bc490908a78184e4818dca53684167507417fdb4c09c2d64d32a9896a"
            }};

            yield return new object[] { new TransactionTestCase {
                Name = "Vitalik_2",
                TxBytes = "0xf864018504a817c80182a410943535353535353535353535353535353535353535018025a0489efdaa54c0f20c7adf612882df0950f5a951637e0307cdcb4c672f298b8bcaa0489efdaa54c0f20c7adf612882df0950f5a951637e0307cdcb4c672f298b8bc6",
                ExpectedSender = "0x23ef145a395ea3fa3deb533b8a9e1b4c6c25d112",
                ExpectedHash = "0xe62703f43b6f10d42b520941898bf710ebb66dba9df81702702b6d9bf23fef1b"
            }};

            yield return new object[] { new TransactionTestCase {
                Name = "Vitalik_3",
                TxBytes = "0xf864028504a817c80282f618943535353535353535353535353535353535353535088025a02d7c5bef027816a800da1736444fb58a807ef4c9603b7848673f7e3a68eb14a5a02d7c5bef027816a800da1736444fb58a807ef4c9603b7848673f7e3a68eb14a5",
                ExpectedSender = "0x2e485e0c23b4c3c542628a5f672eeab0ad4888be",
                ExpectedHash = "0x1f621d7d8804723ab6fec606e504cc893ad4fe4a545d45f499caaf16a61d86dd"
            }};

            yield return new object[] { new TransactionTestCase {
                Name = "SenderTest",
                TxBytes = "0xf85f800182520894095e7baea6a6c7c4c2dfeb977efac326af552d870a801ba048b55bfa915ac795c431978d8a6a992b628d557da5ff759b307d495a36649353a01fffd310ac743f371de3b9f7f9cb56c0b28ad43601b4ab949f53faa07bd2c804",
                ExpectedSender = "0x963f4a0d8a11b758de8d5b99ab4ac898d6438ea6",
                ExpectedHash = null
            }};
        }

        public static IEnumerable<object[]> GetInvalidSignatureTestCases()
        {
            yield return new object[] { "ZeroSigTransaction_V0",
                "0xf85f030182520894b94f5374fce5edbc8e2a8697c15331677e6ebf0b0a801ca07c0e5f1f3c8d2f2c0dce95bc6e7c6b0e5a5a3a0a5a3a0a5a3a0a5a3a0a5a3a0a5a00000000000000000000000000000000000000000000000000000000000000000" };
        }
    }
}
