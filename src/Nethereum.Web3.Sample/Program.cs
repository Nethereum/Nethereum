using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.Web3.Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //var contractDeploymentAndCallTest = new ContractDeploymentAndCall();
            //Console.WriteLine(contractDeploymentAndCallTest.Test().Result);

            //var contractConstructorDeploymentAndCall = new ContractConstructorDeploymentAndCall();
            //Console.WriteLine(contractConstructorDeploymentAndCall.Test().Result);

            //var eventFilterTopic = new EventFilterTopic();
            //Console.WriteLine(eventFilterTopic.Test().Result);

            var eventFilterTopic2 = new EventFilterTopic2();
            Console.WriteLine(eventFilterTopic2.Test().Result);

            Console.ReadLine();
        }
    }
}
