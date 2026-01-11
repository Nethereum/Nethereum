using Nethereum.DevChain.IntegrationDemo.Tests;

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine(@" _   _      _   _");
Console.WriteLine(@"| \ | | ___| |_| |__   ___ _ __ ___ _   _ _ __ ___");
Console.WriteLine(@"|  \| |/ _ \ __| '_ \ / _ \ '__/ _ \ | | | '_ ` _ \");
Console.WriteLine(@"| |\  |  __/ |_| | | |  __/ | |  __/ |_| | | | | | |");
Console.WriteLine(@"|_| \_|\___|\__|_| |_|\___|_|  \___|\__,_|_| |_| |_|");
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("        DevChain Integration Demo v1.0.0");
Console.ResetColor();
Console.WriteLine();
Console.WriteLine("============================================");
Console.WriteLine();

var stopwatch = System.Diagnostics.Stopwatch.StartNew();

try
{
    await ServerConnectionTests.RunAsync();
    await ContractDeploymentTests.RunAsync();
    await ContractInteractionTests.RunAsync();
    await ForkingTests.RunAsync();

    stopwatch.Stop();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("============================================");
    Console.WriteLine($"All Tests Completed Successfully!");
    Console.WriteLine($"Total Time: {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine("============================================");
    Console.ResetColor();
}
catch (Exception ex)
{
    stopwatch.Stop();

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("============================================");
    Console.WriteLine("TEST FAILED!");
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine("============================================");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Stack Trace:");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}
