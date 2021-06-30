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

            //for (var i = 0; i < 1000; i++)
            //{
            //    var key = new EthECKey("b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7".HexToByteArray(), true);
            //    var message = Nethereum.Util.Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("Hello" + i));
            //    Console.WriteLine(EthECDSASignature.CreateStringSignature(key.SignAndCalculateV(message))); 
            //     // ==
            //     // EthECDSASignature.CreateStringSignature(key.SignAndCalculateVOld(message)));
            //    //Console.WriteLine(EthECDSASignature.CreateStringSignature(key.SignAndCalculateVOld(message)));
            //}
        }
    }

    /*
        BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
        AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
        .NET Core SDK=3.1.400
          [Host]     : .NET Core 3.1.6 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.31603), X64 RyuJIT
          DefaultJob : .NET Core 3.1.6 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.31603), X64 RyuJIT
        Method	Mean	Error	StdDev
        MessageSigning	229.8 μs	1.49 μs	1.25 μs
        OldMessageSigning	2,498.8 μs	43.51 μs	40.70 μs
    */
    public class MessageSignerTest
    {

        EthECKey key;
        byte[] message;

        public MessageSignerTest()
        {
            key = new EthECKey("b5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7".HexToByteArray(), true);
            message = Nethereum.Util.Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes("Hello"));
        }

        [Benchmark]
        public EthECDSASignature MessageSigning() => key.SignAndCalculateV(message);

        [Benchmark]
        public EthECDSASignature OldMessageSigning() => key.SignAndCalculateV(message);
    }

}
