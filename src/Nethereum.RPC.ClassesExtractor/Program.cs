using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.RPC.Eth;
using System.Reflection;
using RPCRequestResponseHandlers;

namespace Nethereum.RPC.ClassesExtractor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Type accountType = typeof (EthAccounts);
                var assembly = accountType.GetTypeInfo().Assembly;
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    if (typeof (IRpcRequestHandler).IsAssignableFrom(type))
                     Debug.WriteLine(type.Name);
                }

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