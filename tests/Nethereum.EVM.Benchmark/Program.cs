using System.CommandLine;
using System.Diagnostics;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Signer;

namespace Nethereum.EVM.Benchmark;

public class Program
{
    // Compiled EVMBenchmark contract bytecode
    private const string ContractBytecode = "0x608060405234801561001057600080fd5b50610973806100206000396000f3fe608060405234801561001057600080fd5b50600436106101005760003560e01c80639184881311610097578063c744c48611610066578063c744c486146101e9578063c7dd9e0f146101fc578063e9c9ff7f1461020f578063f0ba84401461022257600080fd5b8063918488131461019b578063a7334feb146101b0578063b6b3dbf5146101c3578063bbad09f5146101d657600080fd5b806361bc221a116100d357806361bc221a14610159578063772a8b02146101625780637e7af6f314610175578063820f61171461018857600080fd5b806318c9e5b2146101055780633fa218061461012a57806346449502146101335780635869885d14610146575b600080fd5b61011861011336600461073e565b610242565b60405190815260200160405180910390f35b61011860025481565b61011861014136600461073e565b6102cc565b61011861015436600461073e565b610371565b61011860005481565b61011861017036600461073e565b61039a565b61011861018336600461073e565b6103d4565b61011861019636600461073e565b610415565b6101ae6101a936600461073e565b610496565b005b6101186101be36600461073e565b6104f7565b6101186101d136600461073e565b6105e8565b6101186101e436600461073e565b61063f565b6101186101f736600461073e565b610690565b61011861020a36600461073e565b6106a3565b6101ae61021d36600461073e565b6106fc565b61011861023036600461073e565b60016020526000908152604090205481565b60006001805b8381116102c057633b9aca0761025f82600361076d565b61026a84600761076d565b6102749190610784565b61027e91906107ad565b915061028b81600261076d565b6102959083610784565b91506102a26002826107c1565b6102ac90836107d5565b9150806102b8816107e8565b915050610248565b50600081905592915050565b6000808267ffffffffffffffff8111156102e8576102e8610801565b6040519080825280601f01601f191660200182016040528015610312576020820181803683370190505b50905060005b83811015610362578060ff1660f81b82828151811061033957610339610817565b60200101906001600160f81b031916908160001a9053508061035a816107e8565b915050610318565b50805160209091012092915050565b6000805b8281101561038f5780610387816107e8565b915050610375565b600081905592915050565b600080805b838110156102c0576000818152600160205260409020546103c09083610784565b9150806103cc816107e8565b91505061039f565b60008060015b8381116102c0576103ec6020826107ad565b6103f7906002610911565b6104019083610784565b91508061040d816107e8565b9150506103da565b600080805b838110156102c0576040516363a2624360e11b815260048101839052309063c744c48690602401602060405180830381865afa15801561045e573d6000803e3d6000fd5b505050506040513d601f19601f820116820180604052508101906104829190610924565b91508061048e816107e8565b91505061041a565b60005b818110156104f157807f96a0854c87d61dd214f20e63418eb9ddf16ebdc42c51f79cd1ebea68133ae8bc6104ce82600261076d565b60405190815260200160405180910390a2806104e9816107e8565b915050610499565b50600055565b600080805b838110156102c05761050f81600361076d565b61051a906001610784565b6000828152600160205260409020819055633b9aca079061053b9084610784565b61054591906107ad565b9150610552600a826107ad565b60000361058957604080516020810184905290810182905260600160408051601f1981840301815291905280516020909101206002555b6105946014826107ad565b6000036105d657807f96a0854c87d61dd214f20e63418eb9ddf16ebdc42c51f79cd1ebea68133ae8bc836040516105cd91815260200190565b60405180910390a25b806105e0816107e8565b9150506104fc565b600080805b838110156102c05760008181526001602081905260409091205461061091610784565b600082815260016020526040902081905561062b9083610784565b915080610637816107e8565b9150506105ed565b6000600182116106525750600081905590565b6000600160025b84811161068357600061066c8385610784565b92935081905061067b816107e8565b915050610659565b5060008190559392505050565b600061069d826001610784565b92915050565b60006001815b838110156106f057604080516020810184905290810182905260600160405160208183030381529060405280519060200120915080806106e8906107e8565b9150506106a9565b50600281905592915050565b60005b818110156104f15761071281600261076d565b61071d906001610784565b60008281526001602052604090205580610736816107e8565b9150506106ff565b60006020828403121561075057600080fd5b5035919050565b634e487b7160e01b600052601160045260246000fd5b808202811582820484141761069d5761069d610757565b8082018082111561069d5761069d610757565b634e487b7160e01b600052601260045260246000fd5b6000826107bc576107bc610797565b500690565b6000826107d0576107d0610797565b500490565b8181038181111561069d5761069d610757565b6000600182016107fa576107fa610757565b5060010190565b634e487b7160e01b600052604160045260246000fd5b634e487b7160e01b600052603260045260246000fd5b600181815b8085111561086857816000190482111561084e5761084e610757565b8085161561085b57918102915b93841c9390800290610832565b509250929050565b60008261087f5750600161069d565b8161088c5750600061069d565b81600181146108a257600281146108ac576108c8565b600191505061069d565b60ff8411156108bd576108bd610757565b50506001821b61069d565b5060208310610133831016604e8410600b84101617156108eb575081810a61069d565b6108f5838361082d565b806000190482111561090957610909610757565b029392505050565b600061091d8383610870565b9392505050565b60006020828403121561093657600080fd5b505191905056fea2646970667358221220b1091fb12a66dcf9605927ba20821716d3cd4b266cc1f1bc96bee2ea3448ee6464736f6c63430008130033";

    public static async Task<int> Main(string[] args)
    {
        var rpcOption = new Option<string>("--rpc", () => "http://127.0.0.1:8545", "RPC URL");
        var keyOption = new Option<string>("--key", () => "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80", "Private key");
        var iterationsOption = new Option<int>("--iterations", () => 100, "Iterations per benchmark");
        var benchmarkOption = new Option<string>("--benchmark", () => "all", "Benchmark to run: all, arithmetic, storage, memory, keccak, events, loop, fibonacci, complex");

        var rootCommand = new RootCommand("EVM Benchmark Tool")
        {
            rpcOption,
            keyOption,
            iterationsOption,
            benchmarkOption
        };

        rootCommand.SetHandler(async (rpc, key, iterations, benchmark) =>
        {
            await RunBenchmarksAsync(rpc, key, iterations, benchmark);
        }, rpcOption, keyOption, iterationsOption, benchmarkOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunBenchmarksAsync(string rpcUrl, string privateKey, int iterations, string benchmark)
    {
        EthECKey.SignRecoverable = true;

        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    EVM BENCHMARK                              ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ RPC:        {rpcUrl,-50} ║");
        Console.WriteLine($"║ Iterations: {iterations,-50} ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var account = new Account(privateKey, 420420);
        var web3 = new Web3.Web3(account, rpcUrl);
        web3.TransactionManager.UseLegacyAsDefault = true;

        // Deploy contract
        Console.WriteLine("Deploying benchmark contract...");
        var deployTx = new TransactionInput
        {
            From = account.Address,
            Data = ContractBytecode,
            Gas = new HexBigInteger(3000000)
        };

        var deployReceipt = await web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(deployTx);
        var contractAddress = deployReceipt.ContractAddress;
        Console.WriteLine($"Contract deployed at: {contractAddress}");
        Console.WriteLine();

        var contract = web3.Eth.GetContract(GetAbi(), contractAddress);
        var results = new List<BenchmarkResult>();

        // Run benchmarks
        if (benchmark == "all" || benchmark == "arithmetic")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchArithmetic", "Arithmetic (ADD/MUL/SUB/DIV)", iterations, web3));
        }

        if (benchmark == "all" || benchmark == "loop")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchLoop", "Loop Overhead", iterations * 10, web3));
        }

        if (benchmark == "all" || benchmark == "fibonacci")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchFibonacci", "Fibonacci (Stack ops)", iterations, web3));
        }

        if (benchmark == "all" || benchmark == "exp")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchExp", "Exponentiation (EXP)", iterations, web3));
        }

        if (benchmark == "all" || benchmark == "keccak")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchKeccak", "Keccak256 Hashing", iterations, web3));
        }

        if (benchmark == "all" || benchmark == "memory")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchMemory", "Memory Operations", iterations * 32, web3));
        }

        if (benchmark == "all" || benchmark == "storage-write")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchStorageWrite", "Storage Write (SSTORE)", iterations / 10, web3));
        }

        if (benchmark == "all" || benchmark == "storage-read")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchStorageRead", "Storage Read (SLOAD)", iterations / 10, web3));
        }

        if (benchmark == "all" || benchmark == "storage-mixed")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchStorageMixed", "Storage Mixed (R+W)", iterations / 10, web3));
        }

        if (benchmark == "all" || benchmark == "events")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchEvents", "Events (LOG)", iterations / 10, web3));
        }

        if (benchmark == "all" || benchmark == "complex")
        {
            results.Add(await RunBenchmarkAsync(contract, "benchComplex", "Complex Workload", iterations / 10, web3));
        }

        // Print summary
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                              RESULTS SUMMARY                                  ║");
        Console.WriteLine("╠═══════════════════════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Benchmark                    │ Iters  │   Gas Used │ Time (ms) │ Gas/Iter    ║");
        Console.WriteLine("╠══════════════════════════════╪════════╪════════════╪═══════════╪═════════════╣");

        foreach (var result in results)
        {
            var gasPerIter = result.GasUsed / result.Iterations;
            Console.WriteLine($"║ {result.Name,-28} │ {result.Iterations,6} │ {result.GasUsed,10} │ {result.ElapsedMs,9:F1} │ {gasPerIter,11} ║");
        }

        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════╝");

        // Summary metrics
        var totalGas = results.Sum(r => r.GasUsed);
        var totalTime = results.Sum(r => r.ElapsedMs);
        Console.WriteLine();
        Console.WriteLine($"Total Gas Used: {totalGas:N0}");
        Console.WriteLine($"Total Time: {totalTime:F1}ms");
        Console.WriteLine($"Effective Gas/Second: {(totalGas / (totalTime / 1000)):N0}");
    }

    private static async Task<BenchmarkResult> RunBenchmarkAsync(
        Contract contract,
        string functionName,
        string displayName,
        int iterations,
        Web3.Web3 web3)
    {
        Console.Write($"Running {displayName}... ");

        var function = contract.GetFunction(functionName);
        var sw = Stopwatch.StartNew();

        try
        {
            var receipt = await function.SendTransactionAndWaitForReceiptAsync(
                from: web3.TransactionManager.Account.Address,
                gas: new HexBigInteger(30000000),
                value: null,
                functionInput: new object[] { new BigInteger(iterations) });

            sw.Stop();

            var gasUsed = (long)receipt.GasUsed.Value;
            Console.WriteLine($"OK ({sw.ElapsedMilliseconds}ms, gas: {gasUsed:N0})");

            return new BenchmarkResult
            {
                Name = displayName,
                Iterations = iterations,
                GasUsed = gasUsed,
                ElapsedMs = sw.ElapsedMilliseconds,
                Success = true
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Console.WriteLine($"FAILED: {ex.Message}");
            return new BenchmarkResult
            {
                Name = displayName,
                Iterations = iterations,
                ElapsedMs = sw.ElapsedMilliseconds,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static string GetAbi()
    {
        return @"[
            {""inputs"":[{""name"":""iterations"",""type"":""uint256""}],""name"":""benchArithmetic"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""iterations"",""type"":""uint256""}],""name"":""benchStorageWrite"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""iterations"",""type"":""uint256""}],""name"":""benchStorageRead"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""iterations"",""type"":""uint256""}],""name"":""benchStorageMixed"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""size"",""type"":""uint256""}],""name"":""benchMemory"",""outputs"":[{""type"":""bytes32""}],""stateMutability"":""pure"",""type"":""function""},
            {""inputs"":[{""name"":""iterations"",""type"":""uint256""}],""name"":""benchKeccak"",""outputs"":[{""type"":""bytes32""}],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""count"",""type"":""uint256""}],""name"":""benchEvents"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""iterations"",""type"":""uint256""}],""name"":""benchExp"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""iterations"",""type"":""uint256""}],""name"":""benchLoop"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""iterations"",""type"":""uint256""}],""name"":""benchInternalCalls"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""n"",""type"":""uint256""}],""name"":""benchFibonacci"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""iterations"",""type"":""uint256""}],""name"":""benchComplex"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""function""},
            {""inputs"":[{""name"":""value"",""type"":""uint256""}],""name"":""addOne"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""pure"",""type"":""function""},
            {""inputs"":[],""name"":""counter"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},
            {""inputs"":[],""name"":""lastHash"",""outputs"":[{""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function""},
            {""inputs"":[{""name"":"""",""type"":""uint256""}],""name"":""data"",""outputs"":[{""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},
            {""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""id"",""type"":""uint256""},{""indexed"":false,""name"":""value"",""type"":""uint256""}],""name"":""BenchmarkEvent"",""type"":""event""}
        ]";
    }
}

public class BenchmarkResult
{
    public string Name { get; set; } = "";
    public int Iterations { get; set; }
    public long GasUsed { get; set; }
    public double ElapsedMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
