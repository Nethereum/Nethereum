using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.RPC.Eth;
using System.Reflection;
using Nethereum.JsonRpc.Client;


namespace Nethereum.RPC.ClassesExtractor
{
    public class RPCRequestInfoExtractor
    {
        public static List<RequestInfo> ExtractRequestInfo()
        {
            Type accountType = typeof(EthAccounts);
            var assembly = accountType.GetTypeInfo().Assembly;
            var types = assembly.GetTypes();
            var requestInfoCollection = new List<RequestInfo>();

            foreach (var type in types)
            {
                if (typeof(IRpcRequestResponse).IsAssignableFrom(type))
                {

                    if (!type.GetTypeInfo().IsGenericType)
                    {
                        var requestInfo = new RequestInfo();
                        requestInfoCollection.Add(requestInfo);

                        requestInfo.RequestType = type;

                        Console.WriteLine(type.Name);
                        foreach (var method in type.GetMethods())
                        {
                            if (method.Name == "SendRequestAsync")
                            {
                                requestInfo.ReturnType = method.ReturnType.GetGenericArguments()[0];
                            }

                            if (method.Name == "BuildRequest")
                            {
                                requestInfo.BuildRequestParameters.Add(method.GetParameters());
                            }
                        }
                    }
                }

            }

            return requestInfoCollection;
        }

    }
}
