using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.ABI.Model;

namespace Nethereum.ABI.Decoders
{
    public class ArrayTypeDecoder : TypeDecoder
    {
        private AttributesToABIExtractor _attributesToABIExtractor;
        public int Size { get; protected set; }

        public ArrayTypeDecoder(ABIType elementType, int size)
        {
            Size = size;
            ElementType = elementType;
            _attributesToABIExtractor = new AttributesToABIExtractor();
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
            var decodedListOutput = (IList)Activator.CreateInstance(type);

            if (decodedListOutput == null)
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

                DecodeAndAddElement(elementType, decodedListOutput, encodedElement);
                
                currentIndex++;
            }
            return decodedListOutput;
        }

        private void DecodeAndAddElement(Type elementType, IList decodedList, byte[] encodedElement)
        {
            if (ElementType is TupleType tupleTypeElement)
            {
                InitTupleElementComponents(elementType, tupleTypeElement);
                decodedList.Add(new ParameterDecoder().DecodeAttributes(encodedElement, elementType));
            }
            else
            {
                decodedList.Add(ElementType.Decode(encodedElement, elementType));
            }
        }

        protected void InitTupleElementComponents(Type elementType, TupleType tupleTypeElement)
        {
            if (tupleTypeElement.Components == null)
            {
                _attributesToABIExtractor.InitTupleComponentsFromTypeAttributes(elementType,
                    tupleTypeElement);
            }
        }

        protected virtual object DecodeStaticElementType(byte[] encoded, Type type, int size)
        {
            var decodedListOutput = (IList) Activator.CreateInstance(type);

            if (decodedListOutput == null)
                throw new Exception("Only types that implement IList<T> are supported to decoded Array Types");

            var elementType = GetIListElementType(type);

            if (elementType == null)
                throw new Exception("Only types that implement IList<T> are supported to decoded Array Types");

            var currentIndex = 0;

            while (currentIndex != encoded.Length)
            {
                var encodedElement = encoded.Skip(currentIndex).Take(ElementType.FixedSize).ToArray();
                DecodeAndAddElement(elementType, decodedListOutput, encodedElement);
                var newIndex = currentIndex + ElementType.FixedSize;
                currentIndex = newIndex;
            }

            return decodedListOutput;
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