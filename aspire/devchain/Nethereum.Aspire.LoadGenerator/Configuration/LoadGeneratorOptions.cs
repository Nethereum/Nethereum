namespace Nethereum.Aspire.LoadGenerator.Configuration;

public class LoadGeneratorOptions
{
    public const string SectionName = "LoadGenerator";

    public string ScenarioType { get; set; } = "transfer";
    public int Concurrency { get; set; } = 5;
    public int DurationSeconds { get; set; } = 0;
    public int WarmupSeconds { get; set; } = 3;
    public int? TargetTps { get; set; }
    public string PrivateKey { get; set; } = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    public int ChainId { get; set; } = 31337;
    public int AccountCount { get; set; } = 10;
}
