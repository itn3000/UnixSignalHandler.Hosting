using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Mono.Unix.Native;

namespace BasicSample
{

    class Program
    {
        static async Task Main(string[] args)
        {
            await new HostBuilder()
                .ConfigureServices((ctx, services) => services.AddSignalHandler(new Signum[]{ Signum.SIGUSR1 }, (s, c) =>
                    {
                        Console.WriteLine($"{s}");
                    }))
                .RunConsoleAsync();
        }
    }
}
