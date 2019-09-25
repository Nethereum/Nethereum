using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.XUnitEthereumClients
{
    public static class EnvironmentalVariableLoader
    {
        /// <summary>
        /// loads environmentVariables in launchSettings.json into environmental variables
        /// </summary>
        public static void LoadFromLaunchSettings()
        {
            const string LaunchSettingsFilePath = "Properties\\launchSettings.json";

            if (!File.Exists(LaunchSettingsFilePath)) return;

            using (var file = File.OpenText(LaunchSettingsFilePath))
            {
                var reader = new JsonTextReader(file);
                var jObject = JObject.Load(reader);

                var variables = jObject
                    .GetValue("profiles")
                    //select a proper profile here
                    .SelectMany(profiles => profiles.Children())
                    .SelectMany(profile => profile.Children<JProperty>())
                    .Where(prop => prop.Name == "environmentVariables")
                    .SelectMany(prop => prop.Value.Children<JProperty>())
                    .ToList();

                foreach (var variable in variables)
                {
                    Environment.SetEnvironmentVariable(variable.Name, variable.Value.ToString());
                }
            }
        }

        /// <summary>
        /// Copies user target environment variables into process target environmental variables
        /// this ensures they will be returned from Environment.GetEnvironmentVariables when the target is not explicitly specified 
        /// </summary>
        public static void CopyUserTargetVariablesIntoProcessTarget()
        {
            var userVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User);

            foreach (string variableName in userVariables.Keys)
            {
                Environment.SetEnvironmentVariable(
                    variableName, 
                    (string)userVariables[variableName], 
                    EnvironmentVariableTarget.Process);
            }
        }
    }
}
