namespace Spawnr.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Reactive;
    using NUnit.Framework;

    public class SpawnTests
    {
        static readonly SpawnOptions SpawnOptions = SpawnOptions.Create();

        [Test]
        public void Spawn()
        {
            var processRef = Ref.Create((TestProcess?)null);
            var notifications = new List<Notification<string>>();
            var args = new[] { "foo", "bar", "baz" };

            var subscription = Spawn(SpawnOptions.AddArguments(args),
                                     notifications,
                                     s => $"out: {s}",
                                     s => $"err: {s}",
                                     processRef);

            Assert.That(subscription, Is.Not.Null);
            Assert.That(processRef.Value, Is.Not.Null);

            var process = processRef.Value!;

            var psi = process.StartInfo;
            Assert.That(psi, Is.Not.Null);
            Assert.That(psi.CreateNoWindow, Is.True);
            Assert.That(psi.UseShellExecute, Is.False);
            Assert.That(psi.RedirectStandardError, Is.True);
            Assert.That(psi.RedirectStandardOutput, Is.True);
            Assert.That(psi.RedirectStandardInput, Is.False);
            Assert.That(psi.Arguments, Is.Empty);
            Assert.That(psi.ArgumentList, Is.EqualTo(args));

            Assert.That(process.StartCalled, Is.True);
            Assert.That(process.EnableRaisingEvents, Is.True);
            Assert.That(process.BeginOutputReadLineCalled, Is.True);
            Assert.That(process.BeginErrorReadLineCalled, Is.True);
            Assert.That(process.DisposeCalled, Is.False);

            process.FireOutputDataReceived("foo");
            process.FireOutputDataReceived("bar");
            process.FireOutputDataReceived("baz");

            process.FireErrorDataReceived("foo");
            process.FireErrorDataReceived("bar");
            process.FireErrorDataReceived("baz");

            process.End();

            Assert.That(process.DisposeCalled, Is.True);

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnNext("out: foo"),
                Notification.CreateOnNext("out: bar"),
                Notification.CreateOnNext("out: baz"),
                Notification.CreateOnNext("err: foo"),
                Notification.CreateOnNext("err: bar"),
                Notification.CreateOnNext("err: baz"),
                Notification.CreateOnCompleted<string>(),
            }));
        }

        [Test]
        public void DisposeNeverThrows()
        {
            var processRef = Ref.Create((TestProcess?)null);
            var notifications = new List<Notification<string>>();

            using var subscription = Spawn(SpawnOptions, notifications,
                                           s => $"out: {s}",
                                           s => $"err: {s}",
                                           processRef,
                                           p => p.TryKillException = new Exception("Some error."));
            subscription.Dispose();

            Assert.Pass();
        }

        [Test]
        public void ErrorOnStart()
        {
            var processRef = Ref.Create((TestProcess?)null);
            var notifications = new List<Notification<string>>();
            var error = new Exception("Error starting process.");

            using var subscription = Spawn(SpawnOptions, notifications,
                                           s => $"out: {s}",
                                           s => $"err: {s}",
                                           processRef,
                                           p => p.StartException = error);

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnError<string>(error)
            }));
        }

        static readonly IEnumerable<TestCaseData> ErrorOnBeginReadLineTestCases = new[]
        {
            new TestCaseData(new Action<TestProcess, Exception>((p, e) => p.BeginOutputReadLineException = e))
                .SetArgDisplayNames(nameof(TestProcess.BeginOutputReadLineException)),
            new TestCaseData(new Action<TestProcess, Exception>((p, e) => p.BeginErrorReadLineException = e))
                .SetArgDisplayNames(nameof(TestProcess.BeginErrorReadLineException)),
        };

        [TestCaseSource(nameof(ErrorOnBeginReadLineTestCases))]
        public void ErrorOnBeginReadLine(Action<TestProcess, Exception> modifier)
        {
            var processRef = Ref.Create((TestProcess?)null);
            var notifications = new List<Notification<string>>();
            var error = new OutOfMemoryException();

            using var subscription = Spawn(SpawnOptions, notifications,
                                           s => $"out: {s}",
                                           s => $"err: {s}",
                                           processRef,
                                           p => modifier(p, error));

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnError<string>(error)
            }));

            var process = processRef.Value!;
            Assert.That(process.TryKillCalled, Is.True);
        }

        [Test]
        public void ErrorOnBeginErrorReadLineWithSomeOutputDataReceived()
        {
            var processRef = Ref.Create((TestProcess?)null);
            var notifications = new List<Notification<string>>();
            var error = new OutOfMemoryException();

            using var subscription = Spawn(SpawnOptions, notifications,
                                           s => $"out: {s}",
                                           s => $"err: {s}",
                                           processRef,
                                           p => p.EnteringBeginOutputReadLine += (sender, args) =>
                                                   p.FireOutputDataReceived("foobar"),
                                           p => p.BeginErrorReadLineException = error);

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnNext("out: foobar"),
                Notification.CreateOnError<string>(error)
            }));

            var process = processRef.Value!;
            Assert.That(process.TryKillCalled, Is.True);
        }

        static IDisposable Spawn<T>(SpawnOptions options,
                                    ICollection<Notification<T>> notifications,
                                    Func<string, T>? stdoutSelector,
                                    Func<string, T>? stderrSelector,
                                    Ref<TestProcess?> process,
                                    params Action<TestProcess>[] processModifiers)
        {
            var observer =
                Observer.Create((T data) => notifications.Add(Notification.CreateOnNext(data)),
                                e => notifications.Add(Notification.CreateOnError<T>(e)),
                                () => notifications.Add(Notification.CreateOnCompleted<T>()));

            return Spawner.Default.Spawn("dummy",
                                         options.WithProcessFactory(psi =>
                                         {
                                             var tp = process.Value = new TestProcess(psi)
                                             {
                                                 Id = 123,
                                                 StartException = null,
                                                 BeginErrorReadLineException = null,
                                                 BeginOutputReadLineException = null,
                                                 TryKillException = null,
                                             };
                                             foreach (var modifier in processModifiers)
                                                modifier(tp);
                                             return tp;
                                         }),
                                         stdoutSelector, stderrSelector)
                                  .Subscribe(observer);
        }
    }
}
