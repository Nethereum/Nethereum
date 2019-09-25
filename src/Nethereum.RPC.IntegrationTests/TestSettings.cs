using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

namespace Nethereum.RPC.Tests.Testers
{
    public class TestSettings
    {
        public readonly string EthereumClient;
        public readonly string CurrentSettings;

        public TestSettings() : this(TestSettingsCategory.hostedTestNet)
        {
        }

        public TestSettings(TestSettingsCategory settingsCategory)
        {
            var builder = new ConfigurationBuilder()
                //load defaults from json file
                .AddJsonFile("test-settings.json")
                //allow environmental overrides (e.g. ETHEREUM_CLIENT )
                .AddEnvironmentVariables();

            Configuration = builder.Build();
            EthereumClient = Configuration["ETHEREUM_CLIENT"] ?? "geth";
            CurrentSettings = GetSettingName(Configuration, EthereumClient, settingsCategory);
        }

        private static string GetSettingName(IConfigurationRoot configuration, string ethereumClient, TestSettingsCategory settingsCategory)
        {
            string settingName = null;
            var testConfigurationSections = configuration.GetSection("testConfigurations");
            foreach (var configurationSection in testConfigurationSections.GetChildren())
            {
                if (ethereumClient == configurationSection["ethereumClient"])
                {
                    settingName = configurationSection[settingsCategory.ToString()];
                    break;
                }
            }

            if(string.IsNullOrEmpty(settingName))
                throw new Exception($"TestSettings not found for Ethereum Client : {ethereumClient} and TestSettingsCategory: '{settingsCategory}'");

            return settingName;
        }

        public bool IsParity()
        {
            return EthereumClient.Equals("parity", StringComparison.OrdinalIgnoreCase);
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