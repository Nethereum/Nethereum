using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Crypto;
using System;
using System.Text;

namespace Nethereum.Signer.Performance
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<MessageSignerTest>();
        }
    }

    public class MessageSignerTest
    {

        EthECKey key;
        byte[] message;

        public MessageSignerTest()
        {
            key = new EthECKey("b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7".HexToByteArray(), true);
            message = Encoding.UTF8.GetBytes("Hello"); ;
        }

        [Benchmark]
        public EthECDSASignature MessageSigning() => key.SignAndCalculateV(message);
    }

}
