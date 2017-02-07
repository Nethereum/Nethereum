using System;
using System.Reflection;

namespace Nethereum.ABI.FunctionEncoding.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class FunctionAttribute : Attribute
    {
        public string Name { get; set; }

        public static FunctionAttribute GetAttribute<T>()
        {
            var type = typeof(T);
            return type.GetTypeInfo().GetCustomAttribute<FunctionAttribute>();
        }

        public static bool IsFunctionType<T>()
        {
            return GetAttribute<T>() != null;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class FunctionOutputAttribute : Attribute
    {
        public static FunctionOutputAttribute GetAttribute<T>()
        {
            var type = typeof(T);
            return type.GetTypeInfo().GetCustomAttribute<FunctionOutputAttribute>();
        }

        public static bool IsFunctionType<T>()
        {
            return GetAttribute<T>() != null;
        }
    }
}