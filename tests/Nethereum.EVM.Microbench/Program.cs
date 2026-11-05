using System.Diagnostics;
using Nethereum.EVM;
using Nethereum.EVM.Microbench;
using Nethereum.EVM.Precompiles;

var filter = args.Length > 0 ? args[0] : "";
int repetitions = args.Length > 1 ? int.Parse(args[1]) : 5;
var sampler = args.Length > 2 && args[2] == "types" ? new AllocationSampler() : null;
Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
var executor = new TransactionExecutor(DefaultHardforkConfigs.Osaka);

Console.WriteLine($"{"case",-40} {"ops",8} {"med us/op",10} {"min us/op",10} {"B/op",10} {"gas",10} status");
var filters = filter.Split('|');
foreach (var benchCase in Cases.All().Where(c => filters.Any(f => c.Name.Contains(f, StringComparison.OrdinalIgnoreCase))))
{
    var times = new List<double>();
    long allocated = 0;
    long gas = 0;
    string status = "ok";
    for (int run = 0; run <= repetitions; run++)
    {
        var execute = benchCase.Prepare(executor);
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        var result = await execute();
        watch.Stop();
        long after = GC.GetAllocatedBytesForCurrentThread();
        if (!result.Success) status = "FAIL " + result.Error;
        if (run == 0) continue;
        times.Add(watch.Elapsed.TotalMilliseconds * 1000.0);
        allocated = after - before;
        gas = result.GasUsed;
    }
    times.Sort();
    if (sampler != null) { Console.WriteLine(benchCase.Name); sampler.Report(); sampler.Reset(); }
    double median = times[times.Count / 2];
    Console.WriteLine($"{benchCase.Name,-40} {benchCase.Operations,8} {median / benchCase.Operations,10:F3} {times[0] / benchCase.Operations,10:F3} {allocated / (double)benchCase.Operations,10:F0} {gas,10} {status}");
}
