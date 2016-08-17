using System;
using System.Reflection;

namespace Nethereum.ABI.FunctionEncoding.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class FunctionAttribute : Attribute
    {
        public string Name { get; set; }

        public static bool IsFunctionType<T>()
        {
            return GetAttribute<T>() != null;
        }

        public static FunctionAttribute GetAttribute<T>()
        {
            var type = typeof(T);
            return type.GetTypeInfo().GetCustomAttribute<FunctionAttribute>();
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class FunctionOutputAttribute : Attribute
    {
        public static bool IsFunctionType<T>()
        {
            return GetAttribute<T>() != null;
        }

        public static FunctionOutputAttribute GetAttribute<T>()
        {
            var type = typeof(T);
            return type.GetTypeInfo().GetCustomAttribute<FunctionOutputAttribute>();
        }
    }
}