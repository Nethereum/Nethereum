using Microsoft.Extensions.Configuration;
using Nethereum.AppChain.Server;
using Nethereum.AppChain.Server.Configuration;

// The AppChain node runs the real AppChain server (sequencer / anchoring / follower),
// not the DevChain dev engine. Configuration is bound from the Aspire-injected
// environment (section "AppChain"); RunAsync respects ASPNETCORE_URLS for the endpoint.
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var config = new AppChainServerConfig();
configuration.GetSection("AppChain").Bind(config);

await AppChainServerRunner.RunAsync(config);
