using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.Json;

class Program
{
    /// <summary>
    /// Simulates a scenario where a large JSON document is deserialized repeatedly while other
    /// threads in the process are concurrently allocating lots of short-lived Large Object Heap (LOH)
    /// allocations. This mimics a real-world production scenario for a web server during peak traffic volumes.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A Task that completes when the program is finished executing.</returns>
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var totalRunTime = args.Length == 0
         ? TimeSpan.FromSeconds(60)
         : TimeSpan.FromSeconds(int.Parse(args[0]));

        await Task.Yield();

        var gcListener = new GcEventListener();
        gcListener.Start();

        var exeDir = AppContext.BaseDirectory;
        var jsonPath = Path.Combine(exeDir, "StressTestDocument_300KB.json");
        var json = File.ReadAllText(jsonPath);

        var swTotal = Stopwatch.StartNew();
        var totalSlowDeserialization = 0;
        var totalDeserializations = 0;
        var minDeserializationTime = double.MaxValue;
        var maxDeserializationTime = 0D;

        var memoryReadings = new List<long>();
        var peakMemoryUsed = 0L;

        var memoryCheckTimer = new Timer(_ =>
        {
            var memoryUsed = GetCurrentlyUsedSystemMemory();
            memoryReadings.Add(memoryUsed);
            peakMemoryUsed = Math.Max(peakMemoryUsed, memoryUsed);
        }, null, 0, 1000);

        while (true)
        {
            var start = DateTimeOffset.UtcNow;
            var sw = Stopwatch.StartNew();
            _ = JsonSerializer.Deserialize<StressTestDocument>(json);
            var elapsed = sw.Elapsed.TotalMilliseconds;
            var end = DateTimeOffset.UtcNow;
            totalDeserializations++;
            minDeserializationTime = Math.Min(minDeserializationTime, elapsed);
            maxDeserializationTime = Math.Max(maxDeserializationTime, elapsed);

            if (elapsed > 300)
            {
                totalSlowDeserialization++;
                Console.WriteLine($"🔥 Slow Deserialization detected: start={start:yyyy-MM-dd HH:mm:ss.fff}, end={end:yyyy-MM-dd HH:mm:ss.fff}, duration={elapsed:F2} ms");
            }

            _ = StressTest.RunMemoryPressureTestAsync(32);

            if (swTotal.Elapsed > totalRunTime)
            {
                break;
            }
        }

        await memoryCheckTimer.DisposeAsync();

        var avgMemoryUsed = memoryReadings.Count > 0 ? memoryReadings.Average() : 0;
        var systemMemory = GetSystemMemory();
        var avgMemoryPercent = avgMemoryUsed / systemMemory * 100;
        var peakMemoryPercent = peakMemoryUsed / (double)systemMemory * 100;

        Console.WriteLine($"🏁 Finished after {swTotal.Elapsed}.");
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"Total deserializations: {totalDeserializations}.");
        Console.WriteLine($"Total slow deserialization: {totalSlowDeserialization}.");
        Console.WriteLine($"Min deserialization time: {minDeserializationTime:F2} ms.");
        Console.WriteLine($"Max deserialization time: {maxDeserializationTime:F2} ms.");
        Console.WriteLine($"Total system memory: {FormatByteSize(systemMemory)}");
        Console.WriteLine($"Average memory usage: {FormatByteSize(avgMemoryUsed)} ({avgMemoryPercent:F2}%)");
        Console.WriteLine($"Peak memory usage: {FormatByteSize(peakMemoryUsed)} ({peakMemoryPercent:F2}%)");
        Console.WriteLine("--------------------------------------------------");
    }

    private static string FormatByteSize(double bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        while (bytes >= 1024 && order < sizes.Length - 1)
        {
            order++;
            bytes /= 1024;
        }

        return $"{bytes:0.##} {sizes[order]}";
    }

    private static long GetCurrentlyUsedSystemMemory()
    {
        var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
        foreach (var os in searcher.Get())
        {
            if (os["TotalVisibleMemorySize"] is ulong totalMemory &&
                os["FreePhysicalMemory"] is ulong freeMemory)
            {
                return (long)((totalMemory - freeMemory) * 1024);
            }
        }

        return 0;
    }

    private static ulong GetSystemMemory()
    {
        var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
        foreach (var os in searcher.Get())
        {
            var size = os["TotalVisibleMemorySize"] switch
            {
                ulong totalMemory => totalMemory,
                _ => 0UL
            };

            return size * 1024;
        }

        return 0;
    }
}

