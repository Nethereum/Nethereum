using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
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
    ?? "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
var receiver = "0x1111111111111111111111111111111111111111";

long txCount = 0;
long txFailed = 0;

app.MapGet("/", () => Results.Ok(new
{
    service = "Nethereum.AppChain.LoadGenerator",
    sent = txCount,
    failed = txFailed,
    intervalMs
}));

_ = Task.Run(async () =>
{
    await Task.Delay(5000);

    var account = new Account(senderKey, chainId);
    var web3 = new Web3(account, appchainUrl);
    web3.TransactionManager.UseLegacyAsDefault = true;

    logger.LogInformation("LoadGenerator starting: rpc={Url}, interval={Interval}ms, chainId={ChainId}",
        appchainUrl, intervalMs, chainId);

    while (true)
    {
        try
        {
            var tx = new TransactionInput
            {
                From = account.Address,
                To = receiver,
                Value = new HexBigInteger(1000),
                Gas = new HexBigInteger(21000),
                GasPrice = new HexBigInteger(10)
            };

            var hash = await web3.Eth.Transactions.SendTransaction.SendRequestAsync(tx);
            Interlocked.Increment(ref txCount);

            if (txCount % 10 == 0)
                logger.LogInformation("Sent {Count} txs, latest={Hash}", txCount, hash);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref txFailed);
            logger.LogWarning("Tx failed: {Error}", ex.Message);
        }

        await Task.Delay(intervalMs);
    }
});

app.Run();
