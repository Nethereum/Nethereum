#if !NET35
using System;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#endif
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.Client
{

#if NETSTANDARD1_1 || NET451 || NETCOREAPP2_1
      public enum LogLevel
    {

        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Critical,
        None
    }

     public interface ILogger
    {
        //
        // Summary:
        //     Writes a log entry.
        //
        // Parameters:
        //   logLevel:
        //     Entry will be written on this level.
        //
        //
        //   messsage:
        //     The entry to be written. Can be also an object.
        //
        //   exception:
        //     The exception related to this entry.
      

        void Log(LogLevel logLevel, string message, Exception exception = null);

        //
        // Summary:
        //     Checks if the given logLevel is enabled.
        //
        // Parameters:
        //   logLevel:
        //     Level to be checked.
        //
        // Returns:
        //     true if enabled.
        bool IsEnabled(LogLevel logLevel);
       }

       public static class LoggerExtensions
       {
       

        public static void LogDebug(this ILogger logger,  Exception exception, string message = null)
        {
            logger.Log(LogLevel.Debug, exception, message);
        }


        public static void LogDebug(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Debug, message);
        }

        public static void LogTrace(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Trace, exception, message);
        }


        public static void LogTrace(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Trace, message);
        }

        public static void LogInformation(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Information, exception, message);
        }


        public static void LogInformation(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Information, message);
        }

        public static void LogError(this ILogger logger, Exception exception, string message)
        {
            logger.Log(LogLevel.Error, exception, message);
        }


        public static void LogError(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Error, message);
        }


        public static void Log(this ILogger logger, LogLevel logLevel, string message)
        {
            logger.Log(logLevel, message);
        }

        public static void Log(this ILogger logger, LogLevel logLevel, Exception exception)
        {
            logger.Log(logLevel, exception.Message, exception);
        }

        public static void Log(this ILogger logger, LogLevel logLevel, Exception exception, string message)
        {
            logger.Log(logLevel, message, exception);
        }


    }
     
#endif

    public class RpcLogger
    {
        public RpcLogger(ILogger log)
        {
            Log = log;
        }
        public ILogger Log { get; private set; }
        public string RequestJsonMessage { get; private set; }
        public RpcResponseMessage ResponseMessage { get; private set; }

        public void LogRequest(string requestJsonMessage)
        {
            if (Log != null)
            {
                RequestJsonMessage = requestJsonMessage;
                Log.LogTrace(GetRPCRequestLogMessage());
            }
        }

        private string GetRPCRequestLogMessage()
        {
            return $"RPC Request: {RequestJsonMessage}";
        }

        private string GetRPCResponseLogMessage()
        {
            return ResponseMessage != null ? $"RPC Response: {ResponseMessage.Result}" : String.Empty;
        }
        private bool IsLogErrorEnabled()
        {
            return Log != null && Log.IsEnabled(LogLevel.Error);
        }

        public void LogResponse(RpcResponseMessage responseMessage)
        {
            if (Log != null)
            {
                ResponseMessage = responseMessage;

                Log.LogTrace(GetRPCResponseLogMessage());

                if (HasError(responseMessage))
                {

                    Log.LogError($"RPC Response Error: {responseMessage.Error.Message}");
                }
            }
        }

        public void LogException(Exception ex)
        {
            if (Log != null)
            {
                Log.LogError(ex, "RPC Exception, " + GetRPCRequestLogMessage() + GetRPCResponseLogMessage());
            }
        }

        private bool HasError(RpcResponseMessage message)
        {
            return message.Error != null && message.HasError;
        }

        private bool IsLogTraceEnabled()
        {
            return Log != null && Log.IsEnabled(LogLevel.Trace);
        }

    }

}
#endif