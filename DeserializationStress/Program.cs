using System.Diagnostics;
using System.Text.Json;

class Program
{
    /// <summary>
    /// Simulates a scenario where a large JSON document is deserialized repeatedly while other
    /// threads in the process are concurrently allocating lots of short-lived Large Object Heap (LOH)
    /// allocations.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A Task that completes when the program is finished executing.</returns>
    static async Task Main(string[] args)
    {
        var totalRunTime = args.Length == 0 ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(int.Parse(args[0]));

        await Task.Yield();

        var gcListener = new GcEventListener();
        gcListener.Start();

        var exeDir = AppContext.BaseDirectory;
        var jsonPath = Path.Combine(exeDir, "StressTestDocument_300KB.json");
        var json = File.ReadAllText(jsonPath);

        var swTotal = Stopwatch.StartNew();
        var totalSlowDeserialization = 0;
        var maxDeserializationTime = 0;

        while (true)
        {
            var start = DateTimeOffset.UtcNow;
            var sw = Stopwatch.StartNew();
            _ = JsonSerializer.Deserialize<StressTestDocument>(json);
            var elapsed = sw.ElapsedMilliseconds;
            var end = DateTimeOffset.UtcNow;
            if (elapsed > 300)
            {
                totalSlowDeserialization++;
                maxDeserializationTime = Math.Max(maxDeserializationTime, (int)elapsed);
                Console.WriteLine($"Slow Deserialization: start={start:yyyy-MM-dd HH:mm:ss.fff}, end={end:yyyy-MM-dd HH:mm:ss.fff}, duration={elapsed} ms");
            }

            _ = StressTest.RunMemoryPressureTestAsync(32);

            if (swTotal.Elapsed > totalRunTime)
            {
                break;
            }
        }

        Console.WriteLine($"Finished after {swTotal.Elapsed}.");
        Console.WriteLine($"Total slow deserialization: {totalSlowDeserialization}.");
        Console.WriteLine($"Max deserialization time: {maxDeserializationTime} ms.");
    }
}

