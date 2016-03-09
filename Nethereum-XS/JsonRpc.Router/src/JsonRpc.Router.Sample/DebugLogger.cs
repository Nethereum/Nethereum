using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace edjCase.JsonRpc.Router.Sample
{
	public class DebugLogger : ILogger
	{
		public string Name { get; }
		public LogLevel LogLevel { get; set; }
		public DebugLogger(string name, LogLevel logLevel = LogLevel.Information)
		{
			this.Name = name;
			this.LogLevel = logLevel;
		}

		public IDisposable BeginScopeImpl(object state)
		{
			return null;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel >= this.LogLevel;
		}

		public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
		{
			if (!this.IsEnabled(logLevel))
			{
				return;
			}
			string formattedException = formatter.Invoke(state, exception);
			string logMessage = $"[{logLevel}] " + formattedException;
			Debug.WriteLine(logMessage);
		}
	}
}
