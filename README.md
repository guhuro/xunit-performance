# xunit.performance

Build | Status
------------ | -------------
Release | [![Build Status](https://ci.dot.net/job/Microsoft_xunit-performance/job/master/job/LinuxFlow_Ubuntu_release/badge/icon)](https://ci.dot.net/job/Microsoft_xunit-performance/job/master/job/LinuxFlow_Ubuntu_release/)
Debug | [![Build Status](https://ci.dot.net/job/Microsoft_xunit-performance/job/master/job/LinuxFlow_Ubuntu_debug/badge/icon)](https://ci.dot.net/job/Microsoft_xunit-performance/job/master/job/LinuxFlow_Ubuntu_debug/)

Provides extensions over xUnit to author performance tests.

## Authoring benchmarks

1. Create a new class library project
2. Add a reference to the "xUnit" NuGet package
3. Add a reference to the latest [xunit.performance.api.dll](https://dotnet.myget.org/feed/dotnet-core/package/nuget/xunit.performance.api)
4. Tag your test methods with [Benchmark] instead of [Fact]
5. Make sure that each [Benchmark]-annotated test contains a loop of this form:

```csharp
[Benchmark]
void TestMethod()
{
    // Any per-test-case setup can go here.
    foreach (var iteration in Benchmark.Iterations)
    {
        // Any per-iteration setup can go here.
        using (iteration.StartMeasurement())
        {
            // Code to be measured goes here.
        }
        // ...per-iteration cleanup
    }
    // ...per-test-case cleanup
}
```

The simplest possible benchmark is therefore:

```csharp
[Benchmark]
void EmptyBenchmark()
{
    foreach (var iteration in Benchmark.Iterations)
        using (iteration.StartMeasurement())
            ; //do nothing
}
```

Which can also be written as:

```csharp
[Benchmark]
void EmptyBenchmark()
{
    Benchmark.Iterate(() => { /*do nothing*/ });
}
```

In addition, you can add inner iterations to the code to be measured.

1. Add the for loop using Benchmark.InnerIterationCount as the number of loop iterations
2. Specify the value of InnerIterationCount using the [Benchmark] attribute

```csharp
[Benchmark(InnerIterationCount=500)]
void TestMethod()
{
    // The first iteration is the "warmup" iteration, where all performance
    // metrics are discarded. Subsequent iterations are measured.
    foreach (var iteration in Benchmark.Iterations)
        using (iteration.StartMeasurement())
            // Inner iterations are recommended for fast running benchmarks
            // that complete very quickly (microseconds). This ensures that
            // the benchmark code runs long enough to dominate the harness's
            // overhead.
            for (int i=0; i<Benchmark.InnerIterationCount; i++)
                // test code here
}
```

If you need to execute different permutation of the same benchmark, then you can use this approach:

```csharp
public static IEnumerable<object[]> InputData()
{
    var args = new string[] { "foo", "bar", "baz" };
    foreach (var arg in args)
        // Currently, the only limitation of this approach is that the
        // types passed to the [Benchmark]-annotated test must be serializable.
        yield return new object[] { new string[] { arg } };
}

// NoInlining prevents aggressive optimizations that
// could render the benchmark meaningless
[MethodImpl(MethodImplOptions.NoInlining)]
private static string FormattedString(string a, string b, string c, string d)
{
    return string.Format("{0}{1}{2}{3}", a, b, c, d);
}

// This benchmark will be executed 3 different times,
// with { "foo" }, { "bar" }, and { "baz" } as args.
[MeasureGCCounts]
[Benchmark(InnerIterationCount = 10)]
[MemberData(nameof(InputData))]
public static void TestMultipleStringInputs(string[] args)
{
    foreach (BenchmarkIteration iter in Benchmark.Iterations)
    {
        using (iter.StartMeasurement())
        {
            for (int i = 0; i < Benchmark.InnerIterationCount; i++)
            {
                FormattedString(args[0], args[0], args[0], args[0]);
            }
        }
    }
}
```

## Creating a simple harness to execute the API

### Option #1: Creating a self containing harness + benchmark

```csharp
using Microsoft.Xunit.Performance;
using Microsoft.Xunit.Performance.Api;
using System.Reflection;

public class Program
{
    public static void Main(string[] args)
    {
        using (XunitPerformanceHarness p = new XunitPerformanceHarness(args))
        {
            string entryAssemblyPath = Assembly.GetEntryAssembly().Location;
            p.RunBenchmarks(entryAssemblyPath);
        }
    }

    [Benchmark(InnerIterationCount=10000)]
    public void TestBenchmark()
    {
        foreach(BenchmarkIteration iter in Benchmark.Iterations)
        {
            using(iter.StartMeasurement())
            {
                for(int i=0; i<Benchmark.InnerIterationCount; i++)
                {
                    string.Format("{0}{1}{2}{3}", "a", "b", "c", "d");
                }
            }
        }
    }
}

```

### Option #2: Creating a harness that iterates through a list of .NET assemblies containing the benchmarks.

```csharp
using System.IO;
using System.Reflection;
using Microsoft.Xunit.Performance.Api;

namespace SampleApiTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using (var harness = new XunitPerformanceHarness(args))
            {
                foreach(var testName in GetTestNames())
                {
                    // Here, the example assumes that the list of .NET
                    // assemblies are dropped side-by-side with harness
                    // (the current executing assembly)
                    var currentDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                    var assemblyPath = Path.Combine(currentDirectory, $"{testName}.dll");

                    // Execute the benchmarks, if any, in this assembly.
                    harness.RunBenchmarks(assemblyPath);
                }
            }
        }

        private static string[] GetTestNames()
        {
            return new [] {
                "Benchmarks",
                "System.Binary.Base64.Tests",
                "System.Text.Primitives.Performance.Tests",
                "System.Slices.Tests"
            };
        }
    }
}
```

## Supported metrics

Currently, the API collect the following data **:

Metric                                | Type                                     | Description
------------------------------------- | ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------
**Allocated Bytes in Current Thread** | GC API call                              | Calls `GC.GetAllocatedBytesForCurrentThread` around the benchmark (Enabled if available on the target .NET runtime)
**Branch Mispredictions**             | Performance Monitor Counter              | Enabled if the counter is available on the machine
**Cache Misses**                      | Performance Monitor Counter              | Enabled if the counter is available on the machine
**Duration**                          | Benchmark execution time in milliseconds | Always enabled
**GC Allocations**                    | GC trace event                           | Use the `[MeasureGCAllocations]` attribute in the source code
**GC Count**                          | GC trace event                           | Use the `[MeasureGCCounts]` attribute in the source code
**Instructions Retired**              | Performance Monitor Counter              | Use the `[MeasureInstructionsRetired]` attribute in the source code

**The default metrics are subject to change, and we are currently working on enabling more metrics and adding support to have more control around the metrics being captured.

## Collected data

Currently the API generates different output files with the collected data:

Format | Data
:----: | ----
  csv  | File contaning statistics of the collected metrics
  etl  | Trace file (Windows only)
  md   | Markdown file with statistics rendered as a table (github friendly)
  xml  | Serialized raw data of all of the tests with their respective metrics

## Authoring Scenario-based Benchmarks

A Scenario-based benchmark is one that runs in a separate process. Therefore, in order to author this kind of test you need to provide an executable, as well as some information for xunit-performance to run. You are responsible for all the measurements, not only to decide what to measure but also to get the actual numbers.

1. Create a new Console Application project
2. Add a reference to the "xUnit" NuGet package
3. Add a reference to the latest [xunit.performance.api.dll](https://dotnet.myget.org/feed/dotnet-core/package/nuget/xunit.performance.api)
4. Define PreIteration and PostIteration delegates
5. Define PostRun Delegate
6. In the main function of your project, specify a ProcessStartInfo for your executable and provide it to xunit-performance.

You have the option of doing all the setup for your executable (downloading a repository, building, doing a restore, etc.) or you can indicate the location of your pre-compiled executable.

PreIteration and PostIteration are delegates that will be called once per run of your app, before and after respectively.
PostRun is a delegate that will be called after all the iterations are complete, and should return an object of type ScenarioBenchmark filled with your tests and metrics names, as well as the numbers you obtained.

### Example

In this example, HelloWorld is a simple program that does some stuff, measures how much time it spent, and outputs this number to a txt file. The test author decide it only has one Test, called "Doing Stuff" and this test has only one metric to measure, "Execution Time"

Authoring it might look something like this.

```csharp
public static void Main(string[] args)
{
  // Optional setup steps
  CloneRepo();
  Build();
  
  using (var h = new XunitPerformanceHarness(args))
  {
    var processStartInfo = new ProcessStartInfo();
    processStartInfo.FileName = Path.Combine(h._PerformanceTestConfig.TemporaryDirectory, “helloWorld.exe“);
    h.RunScenario(processStartInfo, PreIteration, PostIteration, PostRun);
  }
}
```
Where the delegates are defined as follow

```csharp
public void PreIteration() {} //We don't need this
```
```csharp
public void PostIteration()
{
  //Read measurements from txt file
  //Append measurements to buffer   
} 
```

And the PostRun one. We create the ScenarioBenchmark object, and we add only one test with one metric. Then we add one Iteration for each iteration that run.
```csharp
public ScenarioBenchmark PostRun()
{
  var sb = new ScenarioBenchmark(“HelloWorld”);
  var DoStuff = new ScenarioTestModel("Doing Stuff");
  var ExTime = new MetricModel(){Name = "ExecutionTime", DisplayName = "Execution Time", Unit = "ms"};
  DoStuff.Metrics.Add(ExTime);
  for each iteration:
    var it = new IterationModel{Iteration=new Dictionary<string, double>()} // Fill in with results
    DoStuff.Performance.IterationModels.Add(it);
  sb.Tests.Add(DoStuff)
  return sb;
} 
```


Once you create an instance of the XunitPerformanceHarness, it comes with a configuration object of type ScenarioTestConfiguration, which has default values that you can edit to properly apply to your test requirements.

```csharp
public class ScenarioTestConfiguration
{
  public TimeSpan TimeoutPerIteration {get; set;}
  public int Iterations {get; set;}
  public string TemporaryDirectory {get; set;}
} 
```