using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace edjCase.JsonRpc.Core.JsonConverters
{
	/// <summary>
	/// Json converter for Rpc parameters
	/// </summary>
	public class RpcParametersJsonConverter : JsonConverter
	{
		/// <summary>
		/// Writes the value of the parameters to json format
		/// </summary>
		/// <param name="writer">Json writer</param>
		/// <param name="value">Value to be converted to json format</param>
		/// <param name="serializer">Json serializer</param>
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			serializer.Serialize(writer, value);
		}

		/// <summary>
		/// Read the json format and return the correct object type/value for it
		/// </summary>
		/// <param name="reader">Json reader</param>
		/// <param name="objectType">Type of property being set</param>
		/// <param name="existingValue">The current value of the property being set</param>
		/// <param name="serializer">Json serializer</param>
		/// <returns>The object value of the converted json value</returns>
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			switch (reader.TokenType)
			{
				case JsonToken.StartObject:
					try
					{
						JObject jObject = JObject.Load(reader);
						return jObject.ToObject<Dictionary<string, object>>();
					}
					catch (Exception)
					{
						throw new RpcInvalidRequestException("Request parameters can only be an associative array, list or null.");
					}
				case JsonToken.StartArray:
					return JArray.Load(reader).ToObject<object[]>(serializer);
				case JsonToken.Null:
					return null;
			}
			throw new RpcInvalidRequestException("Request parameters can only be an associative array, list or null.");
		}

		/// <summary>
		/// Determines if the type can be convertered with this converter
		/// </summary>
		/// <param name="objectType">Type of the object</param>
		/// <returns>True if the converter converts the specified type, otherwise False</returns>
		public override bool CanConvert(Type objectType)
		{
			return true;
		}
	}
}
