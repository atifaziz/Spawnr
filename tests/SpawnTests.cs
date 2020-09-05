namespace Spawnr.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Reactive;
    using NUnit.Framework;

    public class SpawnTests
    {
        [Test]
        public void Spawn()
        {
            var boxedProcess = new TestProcess?[] { null };
            var options = SpawnOptions.Create();
            var notifications = new List<Notification<string>>();

            var subscription = Spawn(options, notifications,
                                     s => $"out: {s}",
                                     s => $"err: {s}",
                                     boxedProcess);

            Assert.That(subscription, Is.Not.Null);
            Assert.That(boxedProcess[0], Is.Not.Null);

            ref var process = ref boxedProcess[0]!;

            Assert.That(process.StartInfo, Is.Not.Null);
            Assert.That(process.StartCalled, Is.True);
            Assert.That(process.EnableRaisingEvents, Is.True);
            Assert.That(process.BeginOutputReadLineCalled, Is.True);
            Assert.That(process.BeginErrorReadLineCalled, Is.True);
            Assert.That(process.DisposeCalled, Is.False);

            process.FireOutputDataReceived("foo");
            process.FireOutputDataReceived("bar");
            process.FireOutputDataReceived("baz");
            process.FireOutputDataReceived(null);
            process.FireErrorDataReceived("foo");
            process.FireErrorDataReceived("bar");
            process.FireErrorDataReceived("baz");
            process.FireErrorDataReceived(null);
            process.FireExited();

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
            var boxedProcess = new TestProcess?[] { null };
            ref var process = ref boxedProcess[0]!;
            var options = SpawnOptions.Create();
            var notifications = new List<Notification<string>>();

            using (var subscription = Spawn(options, notifications,
                                            s => $"out: {s}",
                                            s => $"err: {s}",
                                            boxedProcess))
            {
                process.TryKillException = new Exception("Some error.");
            }

            Assert.Pass();
        }

        static IDisposable Spawn<T>(SpawnOptions options,
                                    ICollection<Notification<T>> notifications,
                                    Func<string, T>? stdoutSelector,
                                    Func<string, T>? stderrSelector,
                                    TestProcess?[] boxedProcess)
        {
            var observer =
                Observer.Create((T data) => notifications.Add(Notification.CreateOnNext(data)),
                                e => notifications.Add(Notification.CreateOnError<T>(e)),
                                () => notifications.Add(Notification.CreateOnCompleted<T>()));

            return Spawner.Default.Spawn("dummy",
                                         options.WithProcessFactory(psi => boxedProcess[0] = new TestProcess(psi) { TryKillException = null }),
                                         stdoutSelector, stderrSelector)
                                  .Subscribe(observer);
        }
    }
}
