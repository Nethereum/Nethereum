using System;
using Newtonsoft.Json;

namespace edjCase.JsonRpc.Core.JsonConverters
{
	/// <summary>
	/// Converter to convert and enforce the id to be a string, number or null
	/// </summary>
	public class RpcIdJsonConverter : JsonConverter
	{
		/// <summary>
		/// Writes the value of the id to json format
		/// </summary>
		/// <param name="writer">Json writer</param>
		/// <param name="value">Value to be converted to json format</param>
		/// <param name="serializer">Json serializer</param>
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			value = this.ValidateValue(value);
			writer.WriteValue(value);
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
			return this.ValidateValue(reader.Value);
		}

		/// <summary>
		/// Validates that the value is a string, number or null and converts emtpy strings to null
		/// </summary>
		/// <param name="value"></param>
		/// <exception cref="RpcInvalidRequestException">Thrown when value is not a string, number or null</exception>
		/// <returns>The same value or null if it is a string and empty</returns>
		private object ValidateValue(object value)
		{
			if (value == null)
			{
				return null;
			}
			if(!this.CanConvert(value.GetType()))
			{
				throw new RpcInvalidRequestException("Id must be a string, a number or null.");
			}
			string idString = value as string;
			if (idString != null && string.IsNullOrWhiteSpace(idString))
			{
				value = null; //If just empty or whitespace id should be null
			}
			return value;
		}

		/// <summary>
		/// Determines if the type can be convertered with this converter
		/// </summary>
		/// <param name="objectType">Type of the object</param>
		/// <returns>True if the converter converts the specified type, otherwise False</returns>
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof (string) || this.IsNumericType(objectType);
		}

		/// <summary>
		/// Determines if the type is a number
		/// </summary>
		/// <param name="type">Type of the object</param>
		/// <returns>True if the type is a number, otherwise False</returns>
		private bool IsNumericType(Type type)
		{
			return type == typeof (long)
					|| type == typeof (int)
					|| type == typeof (short)
					|| type == typeof (float)
					|| type == typeof (double)
					|| type == typeof (decimal);
		}
	}
}
