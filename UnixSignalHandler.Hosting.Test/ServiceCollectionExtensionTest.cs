using System;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mono.Unix.Native;

namespace UnixSignalHandler.Hosting.Test
{
    public class ServiceCollectionExtensionTest
    {
        [Fact]
        public void AddSignalHandlerTest()
        {
            var services = new ServiceCollection();
            services.AddSignalHandler(new Signum[] { Signum.SIGUSR1 }, (s, c) => Console.WriteLine($"{s}"))
                .AddSignalHandler(new Signum[]{ Signum.SIGUSR2 }, (s, c) => Console.WriteLine($"{s}"));
        }
    }
}
