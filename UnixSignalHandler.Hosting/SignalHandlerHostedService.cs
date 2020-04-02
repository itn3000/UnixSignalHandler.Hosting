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
    public class HandlerErrorEventArgs
    {
        public Signum Signal { get; }
        public Exception Error { get; }
        public HandlerErrorEventArgs(Signum signal, Exception exception)
        {
            Signal = signal;
            Error = exception;
        }
    }
    public class SignalSentEventArgs
    {
        public Signum Signal { get; }
        public bool IsTimeout { get; }
        public SignalSentEventArgs(Signum signal, bool isTimeout)
        {
            Signal = signal;
            IsTimeout = isTimeout;
        }
    }
    public class SignalHandlerHostedService : IHostedService
    {
        public const string WorkerErrorEventName = "worker_error";
        public const string HandlerErrorEventName = "handler_error";
        public const string OperationCancelledEventName = "operation_cancelled";
        public const string SignalSentEventName = "signal_sent";
        public const string DiagnosticName = nameof(SignalHandlerHostedService);
        Thread _signalTask;
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
                try
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
                catch (OperationCanceledException)
                {
                    if (_diag.IsEnabled(OperationCancelledEventName))
                    {
                        _diag.Write(OperationCancelledEventName, _signals);
                    }
                }
                catch (Exception e)
                {
                    if (_diag.IsEnabled(WorkerErrorEventName))
                    {
                        _diag.Write(WorkerErrorEventName, e);
                    }
                }
            }
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = new CancellationTokenSource();
            _channel = Channel.CreateUnbounded<Signum>();
            _signalhandle = _signals.Select(s => new UnixSignal(s)).ToArray();
            _signalTask = new Thread(() =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var idx = UnixSignal.WaitAny(_signalhandle, TimeSpan.FromSeconds(1));
                        if (idx >= 0 && idx < _signalhandle.Length)
                        {
                            foreach(var s in _signalhandle)
                            {
                                if(s.IsSet)
                                {
                                    s.Reset();
                                    _channel.Writer.TryWrite(s.Signum);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"signaltask error: {e}");
                    }
                }
            });
            _signalTask.Start();
            // _signalTask = Task.WhenAll(_signalhandle.Select(async (s) =>
            // {
            //     while(true)
            //     {
            //         await Task.Yield();
            //         try
            //         {
            //             if(s.WaitOne(500, false))
            //             {
            //                 s.Reset();
            //                 _channel.Writer.TryWrite(s.Signum);
            //             }
            //         }
            //         catch(Exception e)
            //         {
            //             Console.WriteLine($"signaltask error: {e}");
            //             break;
            //         }
            //     }
            // }));
            // _registeredhandle = _signalhandle.Select(s => (s, ThreadPool.RegisterWaitForSingleObject(s,
            //     (state, timedOut) =>
            //     {
            //         if(_diag.IsEnabled(SignalSentEventName))
            //         {
            //             _diag.Write(SignalSentEventName, new SignalSentEventArgs(s.Signum, timedOut));
            //         }
            //         if (!timedOut)
            //         {
            //             _channel.Writer.TryWrite(s.Signum);
            //         }
            //     }, null, 1000, false))).ToArray();
            _task = Worker();
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            _channel.Writer.Complete();
            // foreach (var (s, r) in _registeredhandle)
            // {
            //     r.Unregister(s);
            //     s.Close();
            // }
            await _task;
            foreach (var s in _signalhandle)
            {
                s.Close();
            }
            _signalTask.Join();
            // await _signalTask;
            _cts.Dispose();
        }
    }
}