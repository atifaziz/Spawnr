namespace Spawnr.Tests
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    class TestProcess : IProcess
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

        public void FireErrorDataReceived(string? data) =>
            ErrorDataReceived?.Invoke(this, DataReceivedEventArgsFactory.Value(data));

        public void FireOutputDataReceived(string? data) =>
            OutputDataReceived?.Invoke(this, DataReceivedEventArgsFactory.Value(data));

        public bool BeginErrorReadLineCalled;
        public bool BeginOutputReadLineCalled;
        public bool DisposeCalled;
        public bool StartCalled;
        public bool TryKillCalled;
        public Exception? TryKillException;

        public void BeginErrorReadLine() => BeginErrorReadLineCalled = true;
        public void BeginOutputReadLine() => BeginOutputReadLineCalled = true;
        public void Dispose() => DisposeCalled = true;
        public void Start() => StartCalled = true;

        public bool TryKill([MaybeNullWhen(true)] out Exception exception)
        {
            TryKillCalled = true;
            switch (TryKillException)
            {
                case null:
                    exception = null;
                    return true;
                case Win32Exception _:
                case InvalidOperationException _:
                    exception = TryKillException;
                    return false;
                default:
                    throw TryKillException;
            }
        }
    }
}
