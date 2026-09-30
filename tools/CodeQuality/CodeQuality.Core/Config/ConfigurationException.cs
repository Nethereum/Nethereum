namespace CodeQuality.Core.Config;

public sealed class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message)
    {
    }
}
