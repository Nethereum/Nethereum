using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.ABI.Decoders
{
    public class ArrayTypeDecoder : TypeDecoder
    {
        private readonly AttributesToABIExtractor _attributesToABIExtractor;

        public ArrayTypeDecoder(ABIType elementType, int size)
        {
            Size = size;
            ElementType = elementType;
            _attributesToABIExtractor = new AttributesToABIExtractor();
        }

        public int Size { get; protected set; }

        protected ABIType ElementType { get; set; }

        public override object Decode(byte[] encoded, Type type)
        {
            return Decode(encoded, type, Size);
        }

        protected object Decode(byte[] encoded, Type type, int size)
        {
            if (ElementType.IsDynamic())
                return DecodeDynamicElementType(encoded, type, size);
            return DecodeStaticElementType(encoded, type, size);
        }

        public override Type GetDefaultDecodingType()
        {
            return typeof(List<>).MakeGenericType(new[] { ElementType.GetDefaultDecodingType() });
        }

        public override bool IsSupportedType(Type type)
        {
            return GetIListElementType(type) != null;
        }

        protected virtual object DecodeDynamicElementType(byte[] encoded, Type type, int size)
        {
            if (size < 0 || (long)size * 32 > encoded.Length) throw new Exception($@"Insufficient data length to decode Array. Data: {encoded.Length}, Size: {size}");

            var decodedListOutput = (IList) Activator.CreateInstance(type);

            if (decodedListOutput == null)
                throw new Exception("Only types that implement IList<T> are supported to decode Array Types");

            var elementType = GetIListElementType(type);

            if (elementType == null)
                throw new Exception("Only types that implement IList<T> are supported to decode Array Types");

            var offsets = new int[size];
            for (var i = 0; i < size; i++)
            {
                var offset = ReadOffsetWord(encoded, i * 32);
                if (offset > encoded.Length) throw new Exception($@"Array element offset out of range. Offset: {offset}, Data: {encoded.Length}");
                offsets[i] = offset;
            }

            var ends = AbiOffsetTable.ComputeEnds(offsets, encoded.Length);
            for (var i = 0; i < size; i++)
                DecodeAndAddElement(elementType, decodedListOutput, Slice(encoded, offsets[i], ends[i] - offsets[i]));

            return decodedListOutput;
        }

        private static int ReadOffsetWord(byte[] encoded, int index)
        {
            for (var i = 0; i < 28; i++)
                if (encoded[index + i] != 0) throw new OverflowException();
            var value = (encoded[index + 28] << 24) | (encoded[index + 29] << 16) | (encoded[index + 30] << 8) | encoded[index + 31];
            if (value < 0) throw new OverflowException();
            return value;
        }

        private static byte[] Slice(byte[] source, long start, long count)
        {
            return AbiOffsetTable.Slice(source, start, count);
        }

        private void DecodeAndAddElement(Type elementType, IList decodedList, byte[] encodedElement)
        {
            if (ElementType is TupleType tupleTypeElement)
            {
                if (elementType == typeof(List<ParameterOutput>))
                {
                    decodedList.Add(ElementType.Decode(encodedElement, elementType));
                }
                else
                {
                    InitTupleElementComponents(elementType, tupleTypeElement);
                    decodedList.Add(new ParameterDecoder().DecodeAttributes(encodedElement, elementType));
                }
            }
            else
            {
                decodedList.Add(ElementType.Decode(encodedElement, elementType));
            }
        }

        protected void InitTupleElementComponents(Type elementType, TupleType tupleTypeElement)
        {
            if (tupleTypeElement.Components == null)
                _attributesToABIExtractor.InitTupleComponentsFromTypeAttributes(elementType,
                    tupleTypeElement);
        }

        protected virtual object DecodeStaticElementType(byte[] encoded, Type type, int size)
        {
            var elementSize = ElementType.FixedSize;
            if (size < 0 || (long)size * Math.Max(elementSize, 1) > encoded.Length) throw new Exception($@"Insufficient data length to decode Array. Data: {encoded.Length}, Size: {size}");

            var decodedListOutput = (IList) Activator.CreateInstance(type);

            if (decodedListOutput == null)
                throw new Exception("Only types that implement IList<T> are supported to decoded Array Types");

            var elementType = GetIListElementType(type);

            if (elementType == null)
                throw new Exception("Only types that implement IList<T> are supported to decoded Array Types");

            var currentIndex = 0;

            while (currentIndex < size)
            {
                var encodedElement = Slice(encoded, (long)currentIndex * elementSize, elementSize);
                DecodeAndAddElement(elementType, decodedListOutput, encodedElement);
                currentIndex++;
            }

            return decodedListOutput;
        }

        public static Type GetIListElementType(Type listType)
        {
#if DOTNET35
            var enumType = listType.GetTypeInfo().ImplementedInterfaces()
            .Where(i => i.GetTypeInfo().IsGenericType && (i.GenericTypeArguments().Length == 1))
            .FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumType?.GenericTypeArguments()[0];
#else
            var enumType = listType.GetTypeInfo().ImplementedInterfaces
                .Where(i => i.GetTypeInfo().IsGenericType && i.GenericTypeArguments.Length == 1)
                .FirstOrDefault(i => i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumType?.GenericTypeArguments[0];
#endif
        }

        public object DecodePackedUsingElementPacked(byte[] encoded, Type type)
        {
            if (this.ElementType.IsDynamic() || this.ElementType.StaticSize == 0)
                throw new Exception("Packed decoding only supported on arrays for static element types");

            var decodedListOutput = (IList)Activator.CreateInstance(type);

            if (decodedListOutput == null)
                throw new Exception("Only types that implement IList<T> are supported to decode Array Types");

            var elementType = GetIListElementType(type);

            if (elementType == null)
                throw new Exception("Only types that implement IList<T> are supported to decode Array Types");

            var currentIndex = 0;

            while ((long)currentIndex * ElementType.StaticSize < encoded.Length)
            {
                var encodedElement = Slice(encoded, (long)currentIndex * ElementType.StaticSize, ElementType.StaticSize);
                decodedListOutput.Add(ElementType.DecodePacked(encodedElement, elementType));
                currentIndex++;
            }

            return decodedListOutput;
        }

        public override object DecodePacked(byte[] encoded, Type type)
        {

            if (this.ElementType.IsDynamic() || this.ElementType.StaticSize == 0)
                throw new Exception("Packed decoding only supported on arrays for static element types");

            var decodedListOutput = (IList)Activator.CreateInstance(type);

            if (decodedListOutput == null)
                throw new Exception("Only types that implement IList<T> are supported to decode Array Types");

            var elementType = GetIListElementType(type);

            if (elementType == null)
                throw new Exception("Only types that implement IList<T> are supported to decode Array Types");

            var currentIndex = 0;

            while ((long)currentIndex * ElementType.FixedSize < encoded.Length)
            {
                var encodedElement = Slice(encoded, (long)currentIndex * ElementType.FixedSize, ElementType.FixedSize);
                decodedListOutput.Add(ElementType.DecodePacked(encodedElement, elementType));
                currentIndex++;
            }

            return decodedListOutput;

        }
    }
}