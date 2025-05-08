# .NET 9 Deserialization Latency Issue due to GC changes
This repo showcases a perf issue deserializing objects under stress conditions when a process is doing lots of large memory allocations. We observe that using the newer regions-based Garbage Collector (GC) introduced in .NET 7 and later shows a significant performance regression compared to the previous segments-based GC for this particular scenario.

## Background
The scenario in question is based on a real-world large-scale web service.

The web service requests result in a lot of Large Object Heap (LOH) allocations. About 50% of all allocations are strings, as the service does a lot of heavy JSON processing and produces large JSON responses to send back to the caller.

## Issue after moving from .NET 6 to .NET 9.
The service showed a decrease in availability (measured by number of failed requests) due to requests timing out, shortly after moving to .NET 9. Requests are required to complete in a few seconds, so any introduced latency can cause timeouts to become more frequent, which was the case here.

In particular, it was observed that JSON serialization and deserialization operations that should normally complete in tens of milliseconds at most were increasingly taking multiple seconds, failing the call.

## Simulating the serialization issue
The stress test program in this repo attempts to simulate similar conditions to what the web service is experiencing, by attempting as many deserializations of a large payload as possible, while other threads are concurrently doing a lot of LOH allocations.

The program uses background GC to match the web service configuration.

Running on .NET 9 with the normal GC and then running again with .NET 9 but loading the .NET 6 GC, we see a stark difference in performance.

### .NET 9 results (5 minute run)
```
--------------------------------------------------
Total deserializations: 134338.
Deserializations per second: 446.61/s
Total slow deserialization: 76.
Min deserialization time: 0.28 ms.
Avg deserialization time: 2.10 ms.
Max deserialization time: 2531.10 ms.
Total GC events: 2501
GC Start events: 1251
GC End events: 1250
Total system memory: 63.44 GB
Average memory usage: 53.42 GB (84.21%)
Peak memory usage: 63.29 GB (99.76%)
--------------------------------------------------
```

### .NET 9 with Sements GC (clrgc.dll) results (5 minute run)
```
--------------------------------------------------
Total deserializations: 240808.
Deserializations per second: 801.77/s
Total slow deserialization: 20.
Min deserialization time: 0.28 ms.
Avg deserialization time: 1.22 ms.
Max deserialization time: 5723.17 ms.
Total GC events: 762
GC Start events: 381
GC End events: 381
Total system memory: 63.44 GB
Average memory usage: 29.24 GB (46.09%)
Peak memory usage: 61.6 GB (97.10%)
--------------------------------------------------
```
We can observe that with the older GC, we get about 1.7x more deserializations per-second. The average memory usage is also about 45% lower.

## Steps to repro:
.NET 9:
```ps
cd DeserializationStress
$env:DOTNET_GCName=""
dotnet run --configuration Release 300
```

.NET 9 with .NET 6 GC:
```ps
cd DeserializationStress
$env:DOTNET_GCName="clrgc.dll"
dotnet run --configuration Release 300
```
