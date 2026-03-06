using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Nethereum.ABI;
using Nethereum.Contracts;
using Nethereum.Contracts.Services;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Blazor
{
    public abstract class ResultOutputBase : ComponentBase
    {
        [Parameter] public object Result { get; set; }
        [Parameter] public Type ResultType { get; set; }
        [Parameter] public string Title { get; set; }
        [Parameter] public ContractServiceBase ContractService { get; set; }
        [Parameter] public IEnumerable<Type> AdditionalEventTypes { get; set; }

        protected List<object> DecodeEvents(TransactionReceipt receipt)
        {
            if (ContractService == null && AdditionalEventTypes == null) return new();

            var types = new List<Type>();
            if (ContractService != null)
                types.AddRange(ContractService.GetAllEventTypes());
            if (AdditionalEventTypes != null)
                types.AddRange(AdditionalEventTypes);

            var decoded = new List<object>();

            foreach (var t in types.Distinct())
            {
                var eventAbi = ABITypedRegistry.GetEvent(t);
                var decodeMethod = typeof(EventExtensions)
                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == "DecodeAllEvents"
                                         && m.GetParameters().Length == 2
                                         && m.GetGenericArguments().Length == 1);

                if (decodeMethod == null) continue;

                var genericMethod = decodeMethod.MakeGenericMethod(t);
                var result = genericMethod.Invoke(null, new object[] { eventAbi, receipt.Logs });

                if (result is IList enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        var titem = item.GetType();
                        var prop = titem.GetProperty("Event");
                        if (prop != null)
                            decoded.Add(prop.GetValue(item));
                    }
                }
            }

            return decoded;
        }

        protected bool IsSimple(Type type) =>
            type.IsPrimitive || type == typeof(string) || type == typeof(BigInteger) || type == typeof(decimal);

        protected string FormatSimple(object value) =>
            value switch
            {
                BigInteger bi => bi.ToString("N0"),
                decimal d => d.ToString("G"),
                _ => value?.ToString()
            };
    }
}
