using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.RPC.Eth;
using System.Reflection;
using Nethereum.JsonRpc.Client;
using System.Text;

namespace Nethereum.RPC.ClassesExtractor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var requestInfoCollection = RPCRequestInfoExtractor.ExtractRequestInfo();

                foreach (var requestInfo in requestInfoCollection)
                {
                    Console.WriteLine(requestInfo.ToString());
                    Console.WriteLine(new RequestUnityGenerator().Generate(requestInfo));
                }

                new RequestUnityGenerator().GenerateToFile("UnityRPCRequests.cs", requestInfoCollection);

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
        }
    }
}