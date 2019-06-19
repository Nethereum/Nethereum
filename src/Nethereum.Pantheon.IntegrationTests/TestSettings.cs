using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
//using System.Configuration;

namespace Nethereum.RPC.Tests.Testers
{
    public class TestSettings
    {
        public static string PantheonSettings = "testSettings";

        public string CurrentSettings = PantheonSettings;

        public TestSettings()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("test-settings.json");
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; set; }

        public string GetDefaultAccount()
        {
            return GetAppSettingsValue("defaultAccount");
        }

        public string GetDefaultNodeIrl()
        {
            return GetAppSettingsValue("defaultNodeIrl");
        }

        public string GetBlockHash()
        {
            return GetAppSettingsValue("blockhash");
        }

        public string GetTransactionHash()
        {
            return GetAppSettingsValue("transactionHash");
        }

        public string GetLiveRpcUrl()
        {
            return GetLiveSettingsValue("rpcUrl");
        }

        public ulong GetBlockNumber()
        {
            return Convert.ToUInt64(GetAppSettingsValue("blockNumber"));
        }

        private string GetAppSettingsValue(string key)
        {
            return GetSectionSettingsValue(key, CurrentSettings);
        }

        private string GetLiveSettingsValue(string key)
        {
            return GetSectionSettingsValue(key, "liveSettings");
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

        public string GetDefaultLogLocation()
        {
            return GetAppSettingsValue("debugLogLocation");
        }
    }
}