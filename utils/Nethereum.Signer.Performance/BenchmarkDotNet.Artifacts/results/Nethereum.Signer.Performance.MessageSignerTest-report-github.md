``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.22000
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET Core SDK=6.0.400-preview.22301.10
  [Host]     : .NET Core 6.0.5 (CoreCLR 6.0.522.21309, CoreFX 6.0.522.21309), X64 RyuJIT
  DefaultJob : .NET Core 6.0.5 (CoreCLR 6.0.522.21309, CoreFX 6.0.522.21309), X64 RyuJIT


```
|                    Method |          Mean |      Error |     StdDev |
|-------------------------- |--------------:|-----------:|-----------:|
|         MessageSigningRec |     0.1944 ns |  0.0028 ns |  0.0025 ns |
|      MessageSigningBouncy |     2.5965 ns |  0.0275 ns |  0.0257 ns |
|                  Recovery |     0.2177 ns |  0.0035 ns |  0.0033 ns |
|            RecoveryBouncy |     1.7579 ns |  0.0300 ns |  0.0281 ns |
|              FullRoundRec |     1.7343 ns |  0.0069 ns |  0.0061 ns |
|           FullRoundBouncy |     6.7162 ns |  0.1183 ns |  0.1408 ns |
|       SignFunctionMessage | 4,051.9825 ns | 16.8164 ns | 15.7300 ns |
| SignFunctionMessageBouncy | 4,051.6605 ns | 17.6386 ns | 15.6361 ns |
