using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.Exceptions;
using Nethereum.RLP;

using Nethereum.ABI.FunctionEncoding.AttributeEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;

using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Nethereum.Mud.EncodingDecoding
{
    public class SchemaEncoded
    {
        public byte[] TableId { get; set; }
        
        public byte[] FieldLayout { get; set; }
        
        public byte[] KeySchema { get; set; }
        
        public byte[] ValueSchema { get; set; }
        
        public List<string> KeyNames { get; set; }
        
        public List<string> FieldNames { get; set; }
    }

    public static class SchemaEncoder
    {
        public const int MAX_NUMBER_OF_FIELDS = 28;
        public const int MAX_NUMBER_OF_DYNAMIC_FIELDS = 5;

        public static ABIType GetABIType(int index)
        {
            return ABIType.CreateABIType(SchemaAbiTypes[index]);
        }

        //need to validate if it is the same if not use index position 92?
        public static bool IsDynamic(int index)
        {
            return GetABIType(index).IsDynamic();
        }

        public static bool IsStatic(int index)
        {
            return !IsDynamic(index);
        }

        public static List<FieldInfo> Decode(byte[] schema, bool isKeySchema = false)
        {
            var totalLength = schema[0] * 256 + schema[1];
            var numStaticFields = schema[2];
            var numDynamicFields = schema[3];
            var fieldInfos = new List<FieldInfo>();
            var startIndex = 4;
            var fieldCount = 0;
            for (var i = 0; i < numStaticFields; i++)
            {
                fieldCount = i + 1;
                var abiType = SchemaAbiTypes[schema[i + startIndex]];
                fieldInfos.Add(new FieldInfo(abiType, isKeySchema, null, fieldCount));
            }

            startIndex += numStaticFields;
            for (var i = 0; i < numDynamicFields; i++)
            {
                fieldCount = fieldCount + 1;
                var abiType = SchemaAbiTypes[schema[i + startIndex]];
                fieldInfos.Add(new FieldInfo(abiType, isKeySchema, null, fieldCount));
            }

            return fieldInfos;
        }

        public static SchemaEncoded GetSchemaEncoded<TTableRecord, TKey, TValue>() where TTableRecord: TableRecord<TKey, TValue>, new()
            where TValue : class, new()
            where TKey : class, new()
        { 
            var tableRecord = new TTableRecord();
            var tableResourceId = tableRecord.ResourceIdEncoded;
            return GetSchemaEncoded<TKey, TValue>(tableResourceId);
        }

        public static SchemaEncoded GetSchemaEncoded<TKey, TValue>(byte[] tableResourceId)
        {
            var schemaEncoded = GetSchemaEncodedSingleton<TValue>(tableResourceId);
            var keyFields = GetFieldsFromType<TKey>(true);
            var keySchema = EncodeTypesToByteArray(keyFields);
            var keyNames = keyFields.OrderBy(x => x.Order).Select(x => x.Name).ToList();
            schemaEncoded.KeySchema = keySchema;
            schemaEncoded.KeyNames = keyNames;
            return schemaEncoded;
        }

        public static SchemaEncoded GetSchemaEncodedSingleton<TValue>(byte[] tableResourceId)
        {
            var valueFieldInfos = GetFieldsFromType<TValue>();
            var valueSchema = EncodeTypesToByteArray(valueFieldInfos);
            var fieldLayout = FieldLayoutEncoder.EncodeFieldLayout(valueFieldInfos);

            var valueNames = valueFieldInfos.OrderBy(x => x.Order).Select(x => x.Name).ToList();
            return new SchemaEncoded
            {
                TableId = tableResourceId,
                FieldLayout = fieldLayout,
                KeyNames = new List<string>(),
                KeySchema = new byte[0],
                ValueSchema = valueSchema,
                FieldNames = valueNames
            };
        }

        public static SchemaEncoded GetSchemaEncodedSingleton<TTableRecord, TValue>() where TTableRecord : TableRecordSingleton<TValue>, new()
            where TValue : class, new()
           
        {
            var tableRecord = new TTableRecord();
            var tableResourceId = tableRecord.ResourceIdEncoded;
            return GetSchemaEncodedSingleton<TValue>(tableResourceId);
        }

        public static SchemaEncoded GetSchemaEncoded(byte[] tableResourceId,  List<FieldInfo> valueFields, List<FieldInfo> keyFields = null)
        {
            var keyNames = new List<string>();
            var keySchema = new byte[0];
            if (keyFields != null)
            {
                keySchema = EncodeTypesToByteArray(keyFields);
                keyNames = keyFields.OrderBy(x => x.Order).Select(x => x.Name).ToList();
            }
          
            var valueSchema = EncodeTypesToByteArray(valueFields);
            var fieldLayout = FieldLayoutEncoder.EncodeFieldLayout(valueFields);
            var valueNames = valueFields.OrderBy(x => x.Order).Select(x => x.Name).ToList();
            return new SchemaEncoded
            {
                TableId = tableResourceId,
                FieldLayout = fieldLayout,
                KeyNames = keyNames,
                KeySchema = keySchema,
                ValueSchema = valueSchema,
                FieldNames = valueNames
            };
        }


        public static List<FieldInfo> GetFieldsFromType<TType>(bool isKey = false)
        {
            var attributesToABIExtractor = new AttributesToABIExtractor();
            var parameters = attributesToABIExtractor.ExtractParametersFromAttributes(typeof(TType));
            var fieldInfos = new List<FieldInfo>();
            for (var i = 0; i < parameters.Length; i++)
            {
                fieldInfos.Add(new FieldInfo(parameters[i].ABIType.Name, isKey, parameters[i].Name, parameters[i].Order));
            }
            return fieldInfos;
        }

        public static byte[] EncodeTypesToByteArray(List<FieldInfo> fieldInfos)
        {
            var fieldTypes = fieldInfos.OrderBy(x => x.Order).Select(x => x.Type).ToArray();
            return EncodeTypesToByteArray(fieldTypes);
        }

        public static byte[] EncodeTypesToByteArray(params string[] schemaTypes)
        {
            if (schemaTypes.Length > MAX_NUMBER_OF_FIELDS)
                throw new SchemaInvalidNumberOfFieldsException();
            var schema = new byte[32];
            for (var i = 0; i < schema.Length; i++)
                schema[i] = 0;

            int totalLength = 0;
            int dynamicFields = 0;
            int startIndexFields = 4;

            // Compute the length of the schema and the number of static fields
            // and store the schema types in the encoded schema
            for (var i = 0; i < schemaTypes.Length; i++)
            {
                var abiType = ABIType.CreateABIType(schemaTypes[i]);
                var staticByteLength = abiType.StaticSize;
                if (staticByteLength < 0)
                {
                    dynamicFields++;
                }
                else if (dynamicFields > 0)
                {
                    throw new Exception("Static fields must come before dynamic fields in the schema");
                }

                if (staticByteLength < 0)
                    staticByteLength = 0;

                totalLength += staticByteLength;
                // Sequentially store schema types after the first 4 bytes (which are reserved for length and field numbers)
                // (safe because of the initial _schema.length check)
                schema[i + startIndexFields] = (byte)Array.IndexOf(SchemaAbiTypes, schemaTypes[i]);

            }

            var totalLengthBytes = totalLength.ToBytesForRLPEncoding();
            if (totalLengthBytes.Length > 1)
            {
                schema[0] = totalLengthBytes[0];
                schema[1] = totalLengthBytes[1];
            }
            else
            {
                schema[1] = totalLengthBytes[0];
            }

            schema[2] = (byte)(schemaTypes.Length - dynamicFields);
            schema[3] = (byte)dynamicFields;
            return schema;
        }

        public static string EncodeTypesToHex(params string[] schemaTypes)
        {
            return EncodeTypesToByteArray(schemaTypes).ToHex();
        }


        public static readonly string[] SchemaAbiTypes = new[] {
                                                             "uint8",
                                                              "uint16",
                                                              "uint24",
                                                              "uint32",
                                                              "uint40",
                                                              "uint48",
                                                              "uint56",
                                                              "uint64",
                                                              "uint72",
                                                              "uint80",
                                                              "uint88",
                                                              "uint96",
                                                              "uint104",
                                                              "uint112",
                                                              "uint120",
                                                              "uint128",
                                                              "uint136",
                                                              "uint144",
                                                              "uint152",
                                                              "uint160",
                                                              "uint168",
                                                              "uint176",
                                                              "uint184",
                                                              "uint192",
                                                              "uint200",
                                                              "uint208",
                                                              "uint216",
                                                              "uint224",
                                                              "uint232",
                                                              "uint240",
                                                              "uint248",
                                                              "uint256",
                                                              "int8",
                                                              "int16",
                                                              "int24",
                                                              "int32",
                                                              "int40",
                                                              "int48",
                                                              "int56",
                                                              "int64",
                                                              "int72",
                                                              "int80",
                                                              "int88",
                                                              "int96",
                                                              "int104",
                                                              "int112",
                                                              "int120",
                                                              "int128",
                                                              "int136",
                                                              "int144",
                                                              "int152",
                                                              "int160",
                                                              "int168",
                                                              "int176",
                                                              "int184",
                                                              "int192",
                                                              "int200",
                                                              "int208",
                                                              "int216",
                                                              "int224",
                                                              "int232",
                                                              "int240",
                                                              "int248",
                                                              "int256",
                                                              "bytes1",
                                                              "bytes2",
                                                              "bytes3",
                                                              "bytes4",
                                                              "bytes5",
                                                              "bytes6",
                                                              "bytes7",
                                                              "bytes8",
                                                              "bytes9",
                                                              "bytes10",
                                                              "bytes11",
                                                              "bytes12",
                                                              "bytes13",
                                                              "bytes14",
                                                              "bytes15",
                                                              "bytes16",
                                                              "bytes17",
                                                              "bytes18",
                                                              "bytes19",
                                                              "bytes20",
                                                              "bytes21",
                                                              "bytes22",
                                                              "bytes23",
                                                              "bytes24",
                                                              "bytes25",
                                                              "bytes26",
                                                              "bytes27",
                                                              "bytes28",
                                                              "bytes29",
                                                              "bytes30",
                                                              "bytes31",
                                                              "bytes32",
                                                              "bool",
                                                              "address",
                                                              "uint8[]",
                                                              "uint16[]",
                                                              "uint24[]",
                                                              "uint32[]",
                                                              "uint40[]",
                                                              "uint48[]",
                                                              "uint56[]",
                                                              "uint64[]",
                                                              "uint72[]",
                                                              "uint80[]",
                                                              "uint88[]",
                                                              "uint96[]",
                                                              "uint104[]",
                                                              "uint112[]",
                                                              "uint120[]",
                                                              "uint128[]",
                                                              "uint136[]",
                                                              "uint144[]",
                                                              "uint152[]",
                                                              "uint160[]",
                                                              "uint168[]",
                                                              "uint176[]",
                                                              "uint184[]",
                                                              "uint192[]",
                                                              "uint200[]",
                                                              "uint208[]",
                                                              "uint216[]",
                                                              "uint224[]",
                                                              "uint232[]",
                                                              "uint240[]",
                                                              "uint248[]",
                                                              "uint256[]",
                                                              "int8[]",
                                                              "int16[]",
                                                              "int24[]",
                                                              "int32[]",
                                                              "int40[]",
                                                              "int48[]",
                                                              "int56[]",
                                                              "int64[]",
                                                              "int72[]",
                                                              "int80[]",
                                                              "int88[]",
                                                              "int96[]",
                                                              "int104[]",
                                                              "int112[]",
                                                              "int120[]",
                                                              "int128[]",
                                                              "int136[]",
                                                              "int144[]",
                                                              "int152[]",
                                                              "int160[]",
                                                              "int168[]",
                                                              "int176[]",
                                                              "int184[]",
                                                              "int192[]",
                                                              "int200[]",
                                                              "int208[]",
                                                              "int216[]",
                                                              "int224[]",
                                                              "int232[]",
                                                              "int240[]",
                                                              "int248[]",
                                                              "int256[]",
                                                              "bytes1[]",
                                                              "bytes2[]",
                                                              "bytes3[]",
                                                              "bytes4[]",
                                                              "bytes5[]",
                                                              "bytes6[]",
                                                              "bytes7[]",
                                                              "bytes8[]",
                                                              "bytes9[]",
                                                              "bytes10[]",
                                                              "bytes11[]",
                                                              "bytes12[]",
                                                              "bytes13[]",
                                                              "bytes14[]",
                                                              "bytes15[]",
                                                              "bytes16[]",
                                                              "bytes17[]",
                                                              "bytes18[]",
                                                              "bytes19[]",
                                                              "bytes20[]",
                                                              "bytes21[]",
                                                              "bytes22[]",
                                                              "bytes23[]",
                                                              "bytes24[]",
                                                              "bytes25[]",
                                                              "bytes26[]",
                                                              "bytes27[]",
                                                              "bytes28[]",
                                                              "bytes29[]",
                                                              "bytes30[]",
                                                              "bytes31[]",
                                                              "bytes32[]",
                                                              "bool[]",
                                                              "address[]",
                                                              "bytes",
                                                              "string",
        };

    }
}
