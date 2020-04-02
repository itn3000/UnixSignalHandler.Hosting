using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Mono.Unix.Native;
using System.Threading;

namespace BasicSample
{
    class MyService : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine("a");
                    await Task.Delay(10000, stoppingToken);
                }
                catch (TaskCanceledException)
                {

                }
                catch (OperationCanceledException)
                {

                }
            }
            throw new NotImplementedException();
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            await new HostBuilder()
                .ConfigureServices((ctx, services) => services.AddSignalHandler(new Signum[] { Signum.SIGUSR1, Signum.SIGHUP }, (s, c) =>
                     {
                         Console.WriteLine($"{s}");
                     }).AddHostedService<MyService>())
                .RunConsoleAsync();
        }
    }
}
