namespace edjCase.JsonRpc.Core
{
	/// <summary>
	/// Error codes for different Rpc errors
	/// </summary>
	public enum RpcErrorCode
	{
		ParseError = -32700,
		InvalidRequest = -32600,
		MethodNotFound = -32601,
		InvalidParams = -32602,
		InternalError = -32603,

		//Custom
		AmbiguousMethod = -32000
	}

	public static class JsonRpcContants
	{
		/// <summary>
		/// Version of Json Rpc protocol being used
		/// </summary>
		public const string JsonRpcVersion = "2.0";
	}
}
