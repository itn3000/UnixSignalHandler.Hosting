using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using Mono.Unix;
using Mono.Unix.Native;
using System.Threading.Channels;
using System.Linq;

namespace UnixSignalHandler.Hosting
{
    class HandlerErrorEventArgs
    {
        public Signum Signal { get; }
        public Exception Error { get; }
        public HandlerErrorEventArgs(Signum signal, Exception exception)
        {
            Signal = signal;
            Error = exception;
        }
    }
    public class SignalHandlerHostedService : IHostedService
    {
        public const string HandlerErrorEventName = "handler_error";
        public const string DiagnosticName = nameof(SignalHandlerHostedService);
        private static readonly DiagnosticListener _diag = new DiagnosticListener(DiagnosticName);
        ValueTask _task;
        Action<Signum, CancellationToken> _action;
        CancellationTokenSource _cts;
        UnixSignal[] _signalhandle;
        Signum[] _signals;
        (UnixSignal, RegisteredWaitHandle)[] _registeredhandle;
        Channel<Signum> _channel;
        public SignalHandlerHostedService(Action<Signum, CancellationToken> act, Signum[] signals)
        {
            _action = act;
            _signals = signals;
        }
        async ValueTask Worker()
        {
            while (!_cts.IsCancellationRequested)
            {
                if (await _channel.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out var signal) && !_cts.IsCancellationRequested)
                    {
                        try
                        {
                            _action(signal, _cts.Token);
                        }
                        catch (Exception e)
                        {
                            if (_diag.IsEnabled(HandlerErrorEventName))
                            {
                                _diag.Write(HandlerErrorEventName, new HandlerErrorEventArgs(signal, e));
                            }
                        }
                    }
                }
            }
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = new CancellationTokenSource();
            _channel = Channel.CreateUnbounded<Signum>();
            _signalhandle = _signals.Select(s => new UnixSignal(s)).ToArray();
            _registeredhandle = _signalhandle.Select(s => (s, ThreadPool.RegisterWaitForSingleObject(s,
                (state, timedOut) =>
                {
                    if (!timedOut)
                    {
                        _channel.Writer.TryWrite(s.Signum);
                    }
                }, null, -1, false))).ToArray();
            _task = Worker();
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            _channel.Writer.Complete();
            foreach (var (s, r) in _registeredhandle)
            {
                r.Unregister(s);
                s.Close();
            }
            await _task;
            foreach (var s in _signalhandle)
            {
                s.Dispose();
            }
            _cts.Dispose();
        }
    }
}