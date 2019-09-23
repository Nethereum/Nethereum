using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace Nethereum.RPC.Tests.Testers
{
    public class TestSettings
    {
        public TestSettings():this(GethRinkebySettings)
        {
        }

        public TestSettings(string currentSettings)
        {
            CurrentSettings = currentSettings;
            var builder = new ConfigurationBuilder()
           .AddJsonFile("test-settings.json");
            Configuration = builder.Build();
        }

        public static string LiveSettings = "liveSettings";
        public static string ParityRopstenSettings = "parityRopstenSettings";
        public static string GethLocalSettings = "gethLocalSettings";
        public static string GethRinkebySettings = "gethRinkebySettings";

        public string CurrentSettings;

        public bool IsParity()
        {
            return CurrentSettings == ParityRopstenSettings;
        }

        public IConfigurationRoot Configuration { get; set; }

        public string GetDefaultAccount()
        {
            return GetAppSettingsValue("defaultAccount");
        }

        public string GetBlockHash()
        {
            return GetAppSettingsValue("blockhash");
        }

        public string GetTransactionHash()
        {
            return GetAppSettingsValue("transactionHash");
        }

        public string GetWSRpcUrl()
        {
            return GetAppSettingsValue("wsUrl");
        }

        public ulong GetBlockNumber()
        {
            return Convert.ToUInt64(GetAppSettingsValue("blockNumber"));
        }

        public string GetRPCUrl()
        {
            return GetAppSettingsValue("rpcUrl");
        }

        public string GetDefaultAccountPassword()
        {
            return GetAppSettingsValue("defaultAccountPassword");
        }

        public string GetContractAddress()
        {
            return GetAppSettingsValue("contractAddress");
        }

        private string GetAppSettingsValue(string key)
        {
            return GetSectionSettingsValue(key, CurrentSettings);
        }

        private string GetSectionSettingsValue(string key, string sectionSettingsKey)
        {
            var configuration = Configuration.GetSection(sectionSettingsKey);
            var children = configuration.GetChildren();
            var setting = children.FirstOrDefault(x => x.Key == key);
            if (setting != null)
                return setting.Value;
            throw new Exception("Setting: " + key + " Not found");
        }

    }
}