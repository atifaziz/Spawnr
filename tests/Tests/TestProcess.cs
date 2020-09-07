namespace Spawnr.Tests
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    public class TestProcess : IProcess
    {
        public TestProcess(ProcessStartInfo startInfo) =>
            StartInfo = startInfo;

        public ProcessStartInfo StartInfo { get; }

        public int Id { get; set; }
        public int ExitCode { get; set; }
        public bool EnableRaisingEvents { get; set; }

        public event EventHandler? Exited;
        public event DataReceivedEventHandler? ErrorDataReceived;
        public event DataReceivedEventHandler? OutputDataReceived;

        public int ErrorDataReceivedHandlerCount => ErrorDataReceived?.GetInvocationList().Length ?? 0;
        public int OutputDataReceivedHandlerCount => OutputDataReceived?.GetInvocationList().Length ?? 0;

        public void FireExited() =>
            Exited?.Invoke(this, EventArgs.Empty);

        static readonly Lazy<Func<string?, DataReceivedEventArgs>>
            DataReceivedEventArgsFactory = new Lazy<Func<string?, DataReceivedEventArgs>>(() =>
            {
                var ctor = typeof(DataReceivedEventArgs)
                               .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                                               binder: null,
                                               CallingConventions.Any,
                                               new[] { typeof(string) },
                                               modifiers: null);
                if (ctor is null)
                    throw new Exception($"Required {nameof(DataReceivedEventArgs)} constructor not found.");
                return data => (DataReceivedEventArgs)ctor.Invoke(new[] { data });
            });

        bool _isOutputClosed, _isErrorClosed;

        public bool IsOutputClosed => _isOutputClosed;
        public bool IsErrorClosed  => _isErrorClosed;

        public void FireErrorDataReceived(string? data) =>
            FireDataReceived(ref _isErrorClosed, ErrorDataReceived, data);

        public void FireOutputDataReceived(string? data) =>
            FireDataReceived(ref _isOutputClosed, OutputDataReceived, data);

        void FireDataReceived(ref bool isClosed, DataReceivedEventHandler? handler, string? data)
        {
            if (data is null)
            {
                if (isClosed)
                    throw new InvalidOperationException("Output/Error stream was already closed.");
                isClosed = true;
            }
            handler?.Invoke(this, DataReceivedEventArgsFactory.Value(data));
        }

        public event EventHandler? EnteringStart;
        public event EventHandler? LeavingStart;
        public event EventHandler? EnteringBeginErrorReadLine;
        public event EventHandler? LeavingBeginErrorReadLine;
        public event EventHandler? EnteringBeginOutputReadLine;
        public event EventHandler? LeavingBeginOutputReadLine;
        public event EventHandler? EnteringTryKill;
        public event EventHandler? LeavingTryKill;

        public bool BeginErrorReadLineCalled;
        public Exception? BeginErrorReadLineException;
        public bool BeginOutputReadLineCalled;
        public Exception? BeginOutputReadLineException;
        public bool DisposeCalled;
        public bool StartCalled;
        public Exception? StartException;
        public bool TryKillCalled;
        public Exception? TryKillException;

        public void BeginErrorReadLine() =>
            OnCall(ref BeginErrorReadLineCalled,
                   EnteringBeginErrorReadLine, LeavingBeginErrorReadLine,
                   BeginErrorReadLineException);

        public void BeginOutputReadLine() =>
            OnCall(ref BeginOutputReadLineCalled,
                   EnteringBeginOutputReadLine, LeavingBeginOutputReadLine,
                   BeginOutputReadLineException);

        public void Dispose() => OnCall(ref DisposeCalled);

        public void Start() => OnCall(ref StartCalled,
                                      EnteringStart, LeavingStart,
                                      StartException);

        public void End(int exitCode = 0)
        {
            FireErrorDataReceived(null);
            FireOutputDataReceived(null);
            ExitCode = exitCode;
            FireExited();
        }

        public bool TryKill([MaybeNullWhen(true)] out Exception exception)
        {
            OnCall(ref TryKillCalled, EnteringTryKill);
            switch (TryKillException)
            {
                case null:
                    exception = null;
                    OnLeaving();
                    return true;
                case Win32Exception _:
                case InvalidOperationException _:
                    exception = TryKillException;
                    OnLeaving();
                    return false;
                default:
                    OnLeaving();
                    throw TryKillException;
            }

            void OnLeaving() => LeavingTryKill?.Invoke(this, EventArgs.Empty);
        }

        void OnCall(ref bool called,
                    EventHandler? enteringHandler = null,
                    EventHandler? leavingHandler = null,
                    Exception? exception = null)
        {
            enteringHandler?.Invoke(this, EventArgs.Empty);
            called = true;
            if (exception is {} e)
                throw e;
            leavingHandler?.Invoke(this, EventArgs.Empty);
        }
    }
}