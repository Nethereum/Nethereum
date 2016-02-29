namespace RPCRequestResponseHandlers
{
    public class Configuration
    {
        private static object _defaultRequestId = 1;

        public static object DefaultRequestId
        {
            get { return _defaultRequestId; }
            set { _defaultRequestId = value; }
        }
    }
}