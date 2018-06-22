using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Nethereum.ABI.Decoders
{
    public class ArrayTypeDecoder : TypeDecoder
    {
        public int Size { get; protected set; }

        public ArrayTypeDecoder(ABIType elementType, int size)
        {
            Size = size;
            ElementType = elementType;
        }

        protected ABIType ElementType { get; set; }

        public override object Decode(byte[] encoded, Type type)
        {
            return Decode(encoded, type, Size);
        }

        protected object Decode(byte[] encoded, Type type, int size)
        {
            if (ElementType.IsDynamic())
                return DecodeDynamicElementType(encoded, type, size);
            else
                return DecodeStaticElementType(encoded, type, size);
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(List<object>);
        }

        public override bool IsSupportedType(Type type)
        {
            return GetIListElementType(type) != null;
        }

        protected virtual object DecodeDynamicElementType(byte[] encoded, Type type, int size)
        {
            var decoded = (IList)Activator.CreateInstance(type);

            if (decoded == null)
                throw new Exception("Only types that implement IList<T> are supported to decoded Array Types");

            var elementType = GetIListElementType(type);

            if (elementType == null)
                throw new Exception("Only types that implement IList<T> are supported to decoded Array Types");

            var intDecoder = new IntTypeDecoder();
            var dataIndexes = new List<int>();
            
            var currentIndex = 0;
            
            while (currentIndex < size)
            {
                dataIndexes.Add(intDecoder.DecodeInt(encoded.Skip(currentIndex * 32).Take(32).ToArray()));
                currentIndex++;
            }

            currentIndex = 0;

            while (currentIndex < size)
            {
                var currentDataIndex = dataIndexes[currentIndex];
                var nextDataIndex = encoded.Length;
                if (currentIndex + 1 < dataIndexes.Count)
                {
                    nextDataIndex = dataIndexes[currentIndex + 1];
                }   
                var encodedElement =
                    encoded.Skip(currentDataIndex).Take(nextDataIndex - currentDataIndex).ToArray();
                decoded.Add(ElementType.Decode(encodedElement, elementType));
                
                currentIndex++;
            }
            return decoded;
        }

        protected virtual object DecodeStaticElementType(byte[] encoded, Type type, int size)
        {
            var decoded = (IList) Activator.CreateInstance(type);

            if (decoded == null)
                throw new Exception("Only types that implement IList<T> are supported to decoded Array Types");

            var elementType = GetIListElementType(type);

            if (elementType == null)
                throw new Exception("Only types that implement IList<T> are supported to decoded Array Types");

            var currentIndex = 0;

            while (currentIndex != encoded.Length)
            {
                var encodedElement = encoded.Skip(currentIndex).Take(ElementType.FixedSize).ToArray();
                decoded.Add(ElementType.Decode(encodedElement, elementType));
                var newIndex = currentIndex + ElementType.FixedSize;
                currentIndex = newIndex;
            }

            return decoded;
        }

        protected static Type GetIListElementType(Type listType)
        {
#if DOTNET35
            var enumType = listType.GetTypeInfo().ImplementedInterfaces()
            .Where(i => i.GetTypeInfo().IsGenericType && (i.GenericTypeArguments().Length == 1))
            .FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumType?.GenericTypeArguments()[0];
#else
            var enumType = listType.GetTypeInfo().ImplementedInterfaces
            .Where(i => i.GetTypeInfo().IsGenericType && (i.GenericTypeArguments.Length == 1))
            .FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumType?.GenericTypeArguments[0];
#endif
        }
    }
}