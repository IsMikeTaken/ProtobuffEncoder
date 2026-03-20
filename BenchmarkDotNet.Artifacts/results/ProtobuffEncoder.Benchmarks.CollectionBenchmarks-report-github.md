```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i9-12900H 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.104
  [Host]   : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.5 (10.0.5, 10.0.526.15411), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method             | Mean     | Error    | StdDev    | Gen0   | Allocated |
|------------------- |---------:|---------:|----------:|-------:|----------:|
| Encode_Collections | 2.518 μs | 2.686 μs | 0.1472 μs | 0.4921 |   6.07 KB |
| Decode_Collections | 4.039 μs | 2.034 μs | 0.1115 μs | 0.9460 |  11.67 KB |
