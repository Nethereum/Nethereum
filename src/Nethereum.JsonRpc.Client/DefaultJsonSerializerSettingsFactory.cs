using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Linq;

namespace Nethereum.JsonRpc.Client
{
    public static class DefaultJsonSerializerSettingsFactory
    {
        public static JsonSerializerSettings BuildDefaultJsonSerializerSettings()
        {
            return new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, ContractResolver = new NullParamsFirstElementResolver()};
        }
    }

    //Passing a null value as the first parameter in the rpc (as no value) causes issues on client as it is not being ignored deserialising, as it is treated as the first element of the array.
    public class NullParamsFirstElementResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            return type.GetTypeInfo().DeclaredProperties
                    .Select(p => {
                        var jp = base.CreateProperty(p, memberSerialization);
                        jp.ValueProvider = new NullParamsValueProvider(p);
                        return jp;
                    }).ToList();
        }
    }

    public class NullParamsValueProvider : IValueProvider
    {
        PropertyInfo memberInfo;
        public NullParamsValueProvider(PropertyInfo memberInfo)
        {
            this.memberInfo = memberInfo;
        }

        public object GetValue(object target)
        {
            var result = memberInfo.GetValue(target);
            if (memberInfo.Name == "RawParameters")
            {
                var array = result as object[];
                if (array != null && array.Length == 1 && array[0] == null)
                {
                    result = "[]";
                }
            }
            return result;
        }

        public void SetValue(object target, object value)
        {
            memberInfo.SetValue(target, value);
        }
    }
}