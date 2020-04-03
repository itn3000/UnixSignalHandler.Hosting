using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Mono.Unix.Native;
using System.Threading;
using System.Reactive;

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
        static IDisposable SetDiagnosticListener()
        {
            return System.Diagnostics.DiagnosticListener.AllListeners.Subscribe((listener) =>
            {
                if(listener.Name == UnixSignalHandler.Hosting.SignalHandlerHostedService.DiagnosticName)
                {
                    listener.Subscribe(ev =>
                    {
                        Console.WriteLine($"{ev.Key}, {ev.Value}");
                    });
                }
            });
        }
        static async Task Main(string[] args)
        {
            using(SetDiagnosticListener())
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
}
