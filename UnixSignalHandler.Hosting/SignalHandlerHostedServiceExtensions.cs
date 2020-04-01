using Mono.Unix;
using Mono.Unix.Native;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UnixSignalHandler.Hosting;

namespace Microsoft.Extensions.Hosting
{
    public static class SignalHandlerHostedServiceExtensions
    {
        public static IServiceCollection AddSignalHandler(this IServiceCollection services, Signum[] signal, Action<Signum, CancellationToken> act)
        {
            return services.AddHostedService<SignalHandlerHostedService>(provider =>
                new SignalHandlerHostedService(act, signal));
        }
    }
}