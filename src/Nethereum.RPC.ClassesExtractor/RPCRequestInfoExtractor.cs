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
                if (typeof(IRpcRequestHandler).GetTypeInfo().IsAssignableFrom(type))
                {

                    if (!type.GetTypeInfo().IsGenericType)
                    {
                        var requestInfo = new RequestInfo();
                        requestInfoCollection.Add(requestInfo);

                        requestInfo.RequestType = type;

                        Console.WriteLine(type.Name);
                        foreach (var method in type.GetTypeInfo().GetMethods())
                        {
                            if (method.Name == "SendRequestAsync")
                            {
                                requestInfo.ReturnType = method.ReturnType.GetTypeInfo().GetGenericArguments()[0];
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
