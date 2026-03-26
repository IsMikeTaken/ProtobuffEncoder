```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900H 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.104
  [Host]   : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method       | Mean     | Error      | StdDev   | Gen0   | Gen1   | Allocated |
|------------- |---------:|-----------:|---------:|-------:|-------:|----------:|
| Encode_Small | 602.6 ns | 1,784.5 ns | 97.82 ns | 0.0610 |      - |     792 B |
| Decode_Small | 529.1 ns |   264.2 ns | 14.48 ns | 0.0572 |      - |     736 B |
| Encode_Large | 999.0 ns |   639.3 ns | 35.04 ns | 0.5417 | 0.0038 |    6832 B |
| Decode_Large | 820.5 ns |   669.7 ns | 36.71 ns | 0.3052 |      - |    3832 B |

