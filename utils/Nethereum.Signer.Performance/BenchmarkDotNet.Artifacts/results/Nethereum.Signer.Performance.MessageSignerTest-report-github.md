``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.1256 (1909/November2018Update/19H2)
Intel Core i7-7560U CPU 2.40GHz (Kaby Lake), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=5.0.400-preview.21328.4
  [Host]     : .NET Core 3.1.17 (CoreCLR 4.700.21.31506, CoreFX 4.700.21.31502), X64 RyuJIT
  DefaultJob : .NET Core 3.1.17 (CoreCLR 4.700.21.31506, CoreFX 4.700.21.31502), X64 RyuJIT


```
|               Method |       Mean |     Error |    StdDev |     Median |
|--------------------- |-----------:|----------:|----------:|-----------:|
|    MessageSigningRec |  0.6303 ns | 0.0354 ns | 0.1044 ns |  0.6065 ns |
| MessageSigningBouncy |  5.8634 ns | 0.2128 ns | 0.6072 ns |  5.8039 ns |
|             Recovery |  1.5782 ns | 0.1643 ns | 0.4792 ns |  1.8315 ns |
|       RecoveryBouncy |  4.9051 ns | 0.1879 ns | 0.5450 ns |  4.9124 ns |
|            FullRound |  7.0034 ns | 0.6793 ns | 1.9923 ns |  6.6981 ns |
|      FullRoundBouncy | 16.9897 ns | 0.6262 ns | 1.8462 ns | 17.0129 ns |
