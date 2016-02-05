using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Web3.Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var contractDeploymentAndCallTest = new ContractDeploymentAndCall();
            Console.WriteLine(contractDeploymentAndCallTest.Test().Result);

            var contractConstructorDeploymentAndCall = new ContractConstructorDeploymentAndCall();
            Console.WriteLine(contractConstructorDeploymentAndCall.Test().Result);

            Console.ReadLine();
        }
    }
}
