using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using edjCase.JsonRpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace edjCase.JsonRpc.Router
{
	/// <summary>
	/// Object that represents a preconfigured method that the Rpc Api allows a request to call
	/// </summary>
	internal class RpcMethod
	{
		/// <summary>
		/// The method's configured request route it can be called from
		/// </summary>
		public RpcRoute Route { get; }
		/// <summary>
		/// The name of the method
		/// </summary>
		public string Method => this.methodInfo.Name;
		/// <summary>
		/// Reflection information about the method
		/// </summary>
		private MethodInfo methodInfo { get; }
		/// <summary>
		/// The class the method exists in 
		/// </summary>
		private Type type { get; }
		/// <summary>
		/// Reflection information about each of the method's parameters
		/// </summary>
		private ParameterInfo[] parameterInfoList { get; }

		/// <summary>
		/// Service provider to be used as an IoC Container. If not set it will use
		/// basic reflection to invoke methods
		/// </summary>
		private IServiceProvider serviceProvider { get; }

		/// <summary>
		/// Json serialization settings that will be used in serialization and deserialization
		/// for rpc requests
		/// </summary>
		private JsonSerializerSettings jsonSerializerSettings { get; }

		/// <param name="type">Class type that the method is in</param>
		/// <param name="route">Request route the method can be called from</param>
		/// <param name="methodInfo">Reflection information about the method</param>
		/// <param name="serviceProvider">(Optional) Service provider to be used as an IoC Container</param>
		/// <param name="jsonSerializerSettings">Json serialization settings that will be used in serialization and deserialization for rpc requests</param>
		public RpcMethod(Type type, RpcRoute route, MethodInfo methodInfo, IServiceProvider serviceProvider = null, JsonSerializerSettings jsonSerializerSettings = null)
		{
			this.type = type;
			this.Route = route;
			this.methodInfo = methodInfo;
			this.parameterInfoList = methodInfo.GetParameters();
			this.serviceProvider = serviceProvider;
			this.jsonSerializerSettings = jsonSerializerSettings;
		}

		/// <summary>
		/// Invokes the method with the specified parameters, returns the result of the method
		/// </summary>
		/// <exception cref="RpcInvalidParametersException">Thrown when conversion of parameters fails or when invoking the method is not compatible with the parameters</exception>
		/// <param name="parameters">List of parameters to invoke the method with</param>
		/// <returns>The result of the invoked method</returns>
		public object Invoke(params object[] parameters)
		{
			object obj = null;
			if (this.serviceProvider != null)
			{
				//Use service provider (if exists) to create instance
				var objectFactory = ActivatorUtilities.CreateFactory(this.type, Type.EmptyTypes);
				obj = objectFactory(this.serviceProvider, null);
			}
			if(obj == null)
			{
				//Use reflection to create instance if service provider failed or is null
				obj = Activator.CreateInstance(this.type);
			}
			try
			{
				parameters = this.ConvertParameters(parameters);

				object returnObj = this.methodInfo.Invoke(obj, parameters);
				
				returnObj = RpcMethod.HandleAsyncResponses(returnObj);
				
				return returnObj;
			}
			catch (Exception)
			{
				throw new RpcInvalidParametersException();
			}
		}

		/// <summary>
		/// Handles/Awaits the result object if it is a async Task
		/// </summary>
		/// <param name="returnObj">The result of a invoked method</param>
		/// <returns>Awaits a Task and returns its result if object is a Task, otherwise returns the same object given</returns>
		private static object HandleAsyncResponses(object returnObj)
		{
			Task task = returnObj as Task;
			if (task == null) //Not async request
			{
				return returnObj;
			}
			PropertyInfo propertyInfo = task.GetType().GetProperty("Result");
			if (propertyInfo != null)
			{
				//Type of Task<T>. Wait for result then return it
				return propertyInfo.GetValue(returnObj);
			}
			//Just of type Task with no return result
			task.GetAwaiter().GetResult();
			return null;
		}

		/// <summary>
		/// Converts the object array into the exact types the method needs (e.g. long -> int)
		/// </summary>
		/// <param name="parameters">Array of parameters for the method</param>
		/// <returns>Array of objects with the exact types required by the method</returns>
		private object[] ConvertParameters(object[] parameters)
		{
			if (parameters != null)
			{
				for (int index = 0; index < parameters.Length; index++)
				{
					ParameterInfo parameterInfo = this.parameterInfoList[index];

					if (parameters[index] is string && parameterInfo.ParameterType == typeof (Guid))
					{
						Guid guid;
						Guid.TryParse((string) parameters[index], out guid);
						parameters[index] = guid;
					}
					if (parameters[index] is JObject)
					{
						JsonSerializer jsonSerializer = JsonSerializer.Create(this.jsonSerializerSettings);
						parameters[index] = ((JObject)parameters[index]).ToObject(parameterInfo.ParameterType, jsonSerializer);
					}
					if (parameters[index] is JArray)
					{
						JsonSerializer jsonSerializer = JsonSerializer.Create(this.jsonSerializerSettings);
						parameters[index] = ((JArray)parameters[index]).ToObject(parameterInfo.ParameterType, jsonSerializer);
					}
					parameters[index] = Convert.ChangeType(parameters[index], parameterInfo.ParameterType);
				}
			}
			return parameters;
		}

		/// <summary>
		/// Detects if list of parameters matches the method signature
		/// </summary>
		/// <param name="parameterList">Array of parameters for the method</param>
		/// <returns>True if the method signature matches the parameterList, otherwise False</returns>
		public bool HasParameterSignature(object[] parameterList)
		{
			if(parameterList == null)
			{
				throw new ArgumentNullException(nameof(parameterList));
			}
			if (parameterList.Count() > this.parameterInfoList.Count())
			{
				return false;
			}

			for (int i = 0; i < this.parameterInfoList.Count(); i++)
			{
				ParameterInfo parameterInfo = this.parameterInfoList[i];
				if (parameterList.Count() <= i)
				{
					if (!parameterInfo.IsOptional)
					{
						return false;
					}
				}
				else
				{
					object parameter = parameterList[i];
					bool isMatch = RpcMethod.ParameterMatches(parameterInfo, parameter);
					if (!isMatch)
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Detects if the request parameter matches the method parameter
		/// </summary>
		/// <param name="parameterInfo">Reflection info about a method parameter</param>
		/// <param name="parameter">The request's value for the parameter</param>
		/// <returns>True if the request parameter matches the type of the method parameter</returns>
		private static bool ParameterMatches(ParameterInfo parameterInfo, object parameter)
		{
			if (parameter == null)
			{
				bool isNullable = parameterInfo.HasDefaultValue && parameterInfo.DefaultValue == null;
				return isNullable;
			}
			if (parameterInfo.ParameterType == parameter.GetType())
			{
				return true;
			}
			if (parameter is long)
			{
				return parameterInfo.ParameterType == typeof (short) 
					|| parameterInfo.ParameterType == typeof (int);
			}
			if (parameter is double || parameter is decimal)
			{
				return parameterInfo.ParameterType == typeof(double) 
					|| parameterInfo.ParameterType == typeof(decimal) 
					|| parameterInfo.ParameterType == typeof(float);
			}
			if (parameter is string && parameterInfo.ParameterType == typeof (Guid))
			{
				Guid guid;
				return Guid.TryParse((string)parameter, out guid);
			}
			try
			{
				if (parameter is JObject)
				{
					JObject jObject = (JObject)parameter;
					jObject.ToObject(parameterInfo.ParameterType); //Test conversion
					return true;
				}
				if (parameter is JArray)
				{
					JArray jArray = (JArray)parameter;
					jArray.ToObject(parameterInfo.ParameterType); //Test conversion
					return true;
				}
				//Final check to see if the conversion can happen
				// ReSharper disable once ReturnValueOfPureMethodIsNotUsed
				Convert.ChangeType(parameter, parameterInfo.ParameterType);
			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Detects if the request parameters match the method parameters and converts the map into an ordered list
		/// </summary>
		/// <param name="parametersMap">Map of parameter name to parameter value</param>
		/// <param name="parameterList">Result of converting the map to an ordered list, null if result is False</param>
		/// <returns>True if the request parameters match the method parameters, otherwise Fasle</returns>
		public bool HasParameterSignature(Dictionary<string, object> parametersMap, out object[] parameterList)
		{
			if(parametersMap == null)
			{
				throw new ArgumentNullException(nameof(parametersMap));
			}
			bool canParse = this.TryParseParameterList(parametersMap, out parameterList);
			if (!canParse)
			{
				return false;
			}
			bool hasSignature = this.HasParameterSignature(parameterList);
			if (hasSignature)
			{
				return true;
			}
			parameterList = null;
			return false;
		}


		/// <summary>
		/// Tries to parse the parameter map into an ordered parameter list
		/// </summary>
		/// <param name="parametersMap">Map of parameter name to parameter value</param>
		/// <param name="parameterList">Result of converting the map to an ordered list, null if result is False</param>
		/// <returns>True if the parameters can convert to an ordered list based on the method signature, otherwise Fasle</returns>
		public bool TryParseParameterList(Dictionary<string, object> parametersMap, out object[] parameterList)
		{
			parameterList = new object[this.parameterInfoList.Count()];
			foreach (ParameterInfo parameterInfo in this.parameterInfoList)
			{
				if (!parametersMap.ContainsKey(parameterInfo.Name) && !parameterInfo.IsOptional)
				{
					parameterList = null;
					return false;
				}
				parameterList[parameterInfo.Position] = parametersMap[parameterInfo.Name];
			}
			return true;
		}
	}
}
