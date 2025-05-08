public class StressTest
{
    public static async Task RunMemoryPressureTestAsync(int taskCount = 4)
    {
        var cts = new CancellationTokenSource();
        var tasks = new List<Task>();
        for (int t = 0; t < taskCount; t++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var rng = new Random();

                while (!cts.Token.IsCancellationRequested)
                {
                    int size = rng.Next(65 * 1024, 500 * 1024 * 1024);
                    var str = new string('a', size);
                    str = null;

                    if (rng.NextDouble() < 0.05)
                    {
                        await Task.Delay(50, cts.Token);
                    }
                }
            }, cts.Token));
        }

        await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        cts.Cancel();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
