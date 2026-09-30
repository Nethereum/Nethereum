using Nethereum.EngineApi.TwoNodeDemo;

Console.OutputEncoding = System.Text.Encoding.UTF8;

const int BlockCount = 5;
const int ChainId = 31337;

Console.WriteLine();
Console.WriteLine("Nethereum Engine API two-node demo");
Console.WriteLine("producer builds blocks, follower validates and adopts them, both over the Engine API");
Console.WriteLine();

var producer = new EngineNode(18690, 18691, ChainId);
var follower = new EngineNode(18692, 18693, ChainId);

try
{
    Console.WriteLine("starting producer node on http://127.0.0.1:18690 (engine http://127.0.0.1:18691)");
    await producer.StartAsync();

    Console.WriteLine("starting follower node on http://127.0.0.1:18692 (engine http://127.0.0.1:18693)");
    await follower.StartAsync();

    var producerGenesis = await producer.GetBlockByNumberAsync(0);
    var followerGenesis = await follower.GetBlockByNumberAsync(0);

    if (producerGenesis.Hash != followerGenesis.Hash)
    {
        throw new Exception("producer and follower did not start from the same genesis block");
    }

    Console.WriteLine($"genesis {producerGenesis.Hash.Substring(0, 10)} shared by both nodes");
    Console.WriteLine();

    var parentBeaconBlockRoot = "0x" + new string('0', 64);
    var feeRecipient = "0x0000000000000000000000000000000000009999";

    var headHash = producerGenesis.Hash;
    var timestamp = producerGenesis.Timestamp;
    var targetGasLimit = producerGenesis.GasLimit;

    for (var blockNumber = 1; blockNumber <= BlockCount; blockNumber++)
    {
        timestamp += 1;
        var prevRandao = "0x" + blockNumber.ToString("x").PadLeft(64, '0');

        var attributes = new
        {
            timestamp = "0x" + timestamp.ToString("x"),
            prevRandao,
            suggestedFeeRecipient = feeRecipient,
            withdrawals = Array.Empty<object>(),
            parentBeaconBlockRoot,
            slotNumber = "0x" + blockNumber.ToString("x"),
            targetGasLimit = "0x" + targetGasLimit.ToString("x")
        };

        var buildResult = await producer.ForkchoiceUpdatedAsync(headHash, attributes);
        if (buildResult.PayloadStatus != "VALID" || string.IsNullOrEmpty(buildResult.PayloadId))
        {
            throw new Exception($"producer refused to build block {blockNumber}: {buildResult.PayloadStatus}");
        }

        var payload = await producer.GetPayloadAsync(buildResult.PayloadId!);
        var blockHash = payload.BlockHash;

        var producerAdopt = await producer.ForkchoiceUpdatedAsync(blockHash, attributes: null);
        if (producerAdopt.PayloadStatus != "VALID")
        {
            throw new Exception($"producer did not adopt block {blockNumber}: {producerAdopt.PayloadStatus}");
        }

        var followerImport = await follower.NewPayloadAsync(payload, parentBeaconBlockRoot);
        if (followerImport.Status != "VALID")
        {
            throw new Exception($"follower rejected block {blockNumber}: {followerImport.Status}");
        }

        var followerAdopt = await follower.ForkchoiceUpdatedAsync(blockHash, attributes: null);
        if (followerAdopt.PayloadStatus != "VALID")
        {
            throw new Exception($"follower did not adopt block {blockNumber}: {followerAdopt.PayloadStatus}");
        }

        var producerHeadNumber = await producer.GetBlockNumberAsync();
        var followerHeadNumber = await follower.GetBlockNumberAsync();

        Console.WriteLine(
            $"block {blockNumber} | produced {blockHash.Substring(0, 10)} feeRecipient={feeRecipient} | " +
            $"follower newPayload={followerImport.Status} | heads: producer={producerHeadNumber} follower={followerHeadNumber}");

        headHash = blockHash;
    }

    Console.WriteLine();
    Console.WriteLine($"✔ follower reconstructed producer's chain over the Engine API ({BlockCount} blocks, head {headHash.Substring(0, 10)})");
}
finally
{
    await producer.DisposeAsync();
    await follower.DisposeAsync();
}
