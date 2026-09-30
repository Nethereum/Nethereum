namespace Nethereum.EEST.ConformanceRunner
{
    public sealed class ConformanceCaseResult
    {
        public bool Success { get; private init; }
        public string Kind { get; private init; } = "";
        public string Detail { get; private init; } = "";

        public static ConformanceCaseResult Ok() => new() { Success = true, Kind = "pass", Detail = "" };
        public static ConformanceCaseResult Fail(string kind, string detail) => new() { Success = false, Kind = kind, Detail = detail };
    }
}
