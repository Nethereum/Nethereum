using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();

var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LoadGenerator");

var appchainUrl = builder.Configuration["services:appchain:http:0"]
    ?? builder.Configuration["LoadGenerator:AppChainUrl"]
    ?? "http://localhost:53510";

var intervalMs = int.TryParse(builder.Configuration["LoadGenerator:IntervalMs"], out var i) ? i : 2000;
var chainId = int.TryParse(builder.Configuration["LoadGenerator:ChainId"], out var c) ? c : 420420;

var senderKey = builder.Configuration["LoadGenerator:PrivateKey"]
    ?? "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";

long txCount = 0;
long txFailed = 0;

app.MapGet("/", () => Results.Ok(new
{
    service = "Nethereum.AppChain.LoadGenerator",
    sent = Interlocked.Read(ref txCount),
    failed = Interlocked.Read(ref txFailed),
    intervalMs,
    rpcUrl = appchainUrl
}));

_ = Task.Run(async () =>
{
    await Task.Delay(8000);

    try
    {
        var account = new Account(senderKey, chainId);
        var web3 = new Web3(account, appchainUrl);
        web3.TransactionManager.UseLegacyAsDefault = true;

        var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
        logger.LogInformation("LoadGenerator connected: rpc={Url}, chainId={ChainId}, block={Block}, sender={Sender}",
            appchainUrl, chainId, blockNumber.Value, account.Address);

        var balance = await web3.Eth.GetBalance.SendRequestAsync(account.Address);
        logger.LogInformation("Sender balance: {Balance} wei", balance.Value);

        if (balance.Value == 0)
        {
            logger.LogError("Sender account {Address} has no balance — cannot send transactions", account.Address);
            return;
        }

        var receiver = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";

        while (true)
        {
            try
            {
                var hash = await web3.Eth.GetEtherTransferService()
                    .TransferEtherAndWaitForReceiptAsync(receiver, 0.001m);
                Interlocked.Increment(ref txCount);

                var count = Interlocked.Read(ref txCount);
                if (count % 10 == 0)
                    logger.LogInformation("Sent {Count} txs, latest block={Block}",
                        count, hash.BlockNumber.Value);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref txFailed);
                if (Interlocked.Read(ref txFailed) % 10 == 1)
                    logger.LogWarning("Tx failed ({Failed} total): {Error}",
                        Interlocked.Read(ref txFailed), ex.Message);
            }

            await Task.Delay(intervalMs);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "LoadGenerator failed to start");
    }
});

app.Run();
