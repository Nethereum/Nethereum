using System;
using System.IO;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.DevP2P.IntegrationTests
{
    public static class GethChainRlpFixtureReader
    {
        public static byte[] ReadGenesisHash(string chainRlpPath)
        {
            var fileBytes = File.ReadAllBytes(chainRlpPath);

            var firstBlock = (RLPCollection)RLP.RLP.DecodeFirstElement(fileBytes, 0);
            var firstHeader = (RLPCollection)firstBlock[0];

            return firstHeader[0].RLPData
                ?? throw new InvalidOperationException("chain.rlp first block header missing parentHash field");
        }
    }
}
