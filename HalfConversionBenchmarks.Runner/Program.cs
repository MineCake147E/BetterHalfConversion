using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

using HalfConversionBenchmarks;

BenchmarkSwitcher
            .FromAssembly(typeof(HalfToSingleConversionBenchmarks).Assembly)
            .Run(args, DefaultConfig.Instance.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(256)).AddDiagnoser(new DisassemblyDiagnoser(new(int.MaxValue)))
            );
Console.Write("Press any key to exit:");
Console.ReadKey();