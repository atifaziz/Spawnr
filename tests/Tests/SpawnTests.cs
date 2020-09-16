namespace Spawnr.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reactive;
    using NUnit.Framework;
    using static Spawnable;

    public partial class SpawnTests
    {
        static readonly SpawnOptions SpawnOptions = SpawnOptions.Create();

        [Test]
        public void Standard()
        {
            var notifications = new List<Notification<string>>();
            var args = new[] { "foo", "bar", "baz" };

            var subscription = TestSpawn(SpawnOptions.AddArguments(args),
                                         notifications,
                                         s => $"out: {s}",
                                         s => $"err: {s}");

            var process = subscription.Tag;
            Assert.That(process, Is.Not.Null);

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
            var notifications = new List<Notification<string>>();

            using var subscription =
                TestSpawn(SpawnOptions, notifications,
                          s => $"out: {s}",
                          s => $"err: {s}",
                          p => p.TryKillException = new Exception("Some error."));
            subscription.Dispose();

            Assert.Pass();
        }

        [Test]
        public void OutputsAfterDisposeAreSuppressed()
        {
            var notifications = new List<Notification<string>>();
            using var subscription =
                TestSpawn(SpawnOptions, notifications,
                          s => $"out: {s}",
                          s => $"err: {s}");
            subscription.Dispose();
            var process = subscription.Tag;
            process.FireOutputDataReceived("output");
            process.FireErrorDataReceived("error");
            process.End(0);

            Assert.That(notifications, Is.Empty);
        }

        [Test]
        public void ErrorAfterDisposeIsSuppressed()
        {
            var notifications = new List<Notification<string>>();
            using var subscription =
                TestSpawn(SpawnOptions, notifications,
                          s => $"out: {s}",
                          s => $"err: {s}");
            var process = subscription.Tag;
            subscription.Dispose();
            process.End(42);

            Assert.That(notifications, Is.Empty);
        }

        [Test]
        public void ErrorOnStart()
        {
            var notifications = new List<Notification<string>>();
            var error = new Exception("Error starting process.");

            using var subscription = TestSpawn(SpawnOptions, notifications,
                                               s => $"out: {s}",
                                               s => $"err: {s}",
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
            var notifications = new List<Notification<string>>();
            var error = new OutOfMemoryException();

            using var subscription = TestSpawn(SpawnOptions, notifications,
                                               s => $"out: {s}",
                                               s => $"err: {s}",
                                               p => modifier(p, error));

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnError<string>(error)
            }));

            var process = subscription.Tag;
            Assert.That(process.TryKillCalled, Is.True);
        }

        [Test]
        public void ErrorOnBeginErrorReadLineWithSomeOutputDataReceived()
        {
            var notifications = new List<Notification<string>>();
            var error = new OutOfMemoryException();

            using var subscription =
                TestSpawn(SpawnOptions, notifications,
                          s => $"out: {s}",
                          s => $"err: {s}",
                          p => p.EnteringBeginOutputReadLine += (sender, args) =>
                                  p.FireOutputDataReceived("foobar"),
                          p => p.BeginErrorReadLineException = error);

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnNext("out: foobar"),
                Notification.CreateOnError<string>(error)
            }));

            var process = subscription.Tag;
            Assert.That(process.TryKillCalled, Is.True);
        }

        [Test]
        public void NonZeroExitCodeProducesError()
        {
            var notifications = new List<Notification<string>>();

            using var subscription = TestSpawn(SpawnOptions, notifications,
                                               s => $"out: {s}",
                                               s => $"err: {s}");

            var process = subscription.Tag;
            process.End(42);

            Assert.That(notifications.Count, Is.EqualTo(1));

            var notification = notifications.Single();
            Assert.That(notification.Kind, Is.EqualTo(NotificationKind.OnError));
            Assert.That(notification.Exception, Is.TypeOf<ExternalProcessException>());
            Assert.That(notification.Exception.Message,
                        Is.EqualTo("Process \"dummy\" (launched as the ID 123) ended with the non-zero exit code 42."));
        }

        [Test]
        public void SuppressNonZeroExitCodeError()
        {
            var notifications = new List<Notification<string>>();

            using var subscription = TestSpawn(SpawnOptions.SuppressNonZeroExitCodeError(),
                                               notifications,
                                               s => $"out: {s}",
                                               s => $"err: {s}");

            var process = subscription.Tag;
            process.End(42);

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnCompleted<string>()
            }));
        }

        [Test]
        public void JustOutputData()
        {
            var notifications = new List<Notification<string>>();

            using var subscription = TestSpawn(SpawnOptions.SuppressNonZeroExitCodeError(),
                                               notifications, s => $"out: {s}", null);

            var process = subscription.Tag;
            process.FireOutputDataReceived("foo");
            process.FireOutputDataReceived("bar");
            process.FireOutputDataReceived("baz");
            process.End(42);

            Assert.That(process.StartInfo.RedirectStandardError, Is.False);
            Assert.That(process.BeginErrorReadLineCalled, Is.False);
            Assert.That(process.ErrorDataReceivedHandlerCount, Is.Zero);

            Assert.That(process.StartInfo.RedirectStandardOutput, Is.True);
            Assert.That(process.BeginOutputReadLineCalled, Is.True);
            Assert.That(process.OutputDataReceivedHandlerCount, Is.EqualTo(1));

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnNext("out: foo"),
                Notification.CreateOnNext("out: bar"),
                Notification.CreateOnNext("out: baz"),
                Notification.CreateOnCompleted<string>()
            }));
        }

        [Test]
        public void JustErrorData()
        {
            var notifications = new List<Notification<string>>();

            using var subscription = TestSpawn(SpawnOptions.SuppressNonZeroExitCodeError(),
                                               notifications, null, s => $"err: {s}");

            var process = subscription.Tag;
            process.FireErrorDataReceived("foo");
            process.FireErrorDataReceived("bar");
            process.FireErrorDataReceived("baz");
            process.End(42);

            Assert.That(process.StartInfo.RedirectStandardError, Is.True);
            Assert.That(process.BeginErrorReadLineCalled, Is.True);
            Assert.That(process.ErrorDataReceivedHandlerCount, Is.EqualTo(1));

            Assert.That(process.StartInfo.RedirectStandardOutput, Is.False);
            Assert.That(process.BeginOutputReadLineCalled, Is.False);
            Assert.That(process.OutputDataReceivedHandlerCount, Is.Zero);

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnNext("err: foo"),
                Notification.CreateOnNext("err: bar"),
                Notification.CreateOnNext("err: baz"),
                Notification.CreateOnCompleted<string>()
            }));
        }

        [Test]
        public void NoOutput()
        {
            var notifications = new List<Notification<string>>();

            using var subscription = TestSpawn(SpawnOptions.SuppressNonZeroExitCodeError(),
                                               notifications, null, null);

            var process = subscription.Tag;
            process.End(42);

            Assert.That(process.StartInfo.RedirectStandardError, Is.False);
            Assert.That(process.BeginErrorReadLineCalled, Is.False);
            Assert.That(process.ErrorDataReceivedHandlerCount, Is.Zero);

            Assert.That(process.StartInfo.RedirectStandardOutput, Is.False);
            Assert.That(process.BeginOutputReadLineCalled, Is.False);
            Assert.That(process.ErrorDataReceivedHandlerCount, Is.Zero);

            Assert.That(notifications, Is.EqualTo(new[]
            {
                Notification.CreateOnCompleted<string>()
            }));
        }

        static TaggedDisposable<TestProcess>
            TestSpawn<T>(SpawnOptions options,
                         ICollection<Notification<T>> notifications,
                         Func<string, T>? stdoutSelector,
                         Func<string, T>? stderrSelector,
                         params Action<TestProcess>[] processModifiers)
        {
            var observer =
                Observer.Create((T data) => notifications.Add(Notification.CreateOnNext(data)),
                                e => notifications.Add(Notification.CreateOnError<T>(e)),
                                () => notifications.Add(Notification.CreateOnCompleted<T>()));

            TestProcess? process = null;

            var subscription =
                Spawn("dummy", ProgramArguments.Empty, stdoutSelector,
                                                       stderrSelector)
                    .WithOptions(options.WithProcessFactory(psi =>
                    {
                        process = new TestProcess(psi)
                        {
                            Id = 123,
                            StandardInput = System.IO.StreamWriter.Null,
                            StartException = null,
                            BeginErrorReadLineException = null,
                            BeginOutputReadLineException = null,
                            TryKillException = null,
                        };
                        foreach (var modifier in processModifiers)
                            modifier(process);
                        return process;
                    }))
                    .Subscribe(observer);

            Debug.Assert(process is {});
            return TaggedDisposable.Create(subscription, process);
        }
    }
}

namespace Spawnr.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reactive.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using NUnit.Framework.Constraints;
    using static MoreLinq.Extensions.PartitionExtension;
    using static Spawnable;

    partial class SpawnTests
    {
        public class TestApp
        {
            static string? _testAppPath;

            [OneTimeSetUp]
            public static void Init()
            {
                var config = (AssemblyConfigurationAttribute?)Attribute.GetCustomAttribute(typeof(TestApp).Assembly, typeof(AssemblyConfigurationAttribute));
                if (config is null)
                    throw new Exception("Unknown assembly build configuration.");
                var testDirPath = TestContext.CurrentContext.TestDirectory;
                _testAppPath =
                    Path.Combine(
                        new[] { testDirPath }
                            .Concat(Enumerable.Repeat("..", 4))
                            .Append("TestApp")
                            .Concat(testDirPath.Split(Path.DirectorySeparatorChar).TakeLast(3))
                            .Append("TestApp.dll")
                            .ToArray());
            }

            enum Std { Out, Err }

            static ISpawnable SpawnTestApp() =>
                Spawn("dotnet", ProgramArguments.Var(_testAppPath!));

            static ISpawnable<OutputOrErrorLine> TestAppStreams() =>
                SpawnTestApp().CaptureOutputs();

            static ISpawnable<string> TestAppOutput(IObservable<string>? stdin = null) =>
                TestAppStreams()
                    .Input(stdin?.AsOutput())
                    .FilterOutput();

            static ISpawnable<string> TestAppError(IObservable<string>? stdin = null) =>
                TestAppStreams()
                    .Input(stdin?.AsOutput())
                    .FilterError();

            [Test]
            public void Nop()
            {
                var output = TestAppStreams().AddArgument("nop");
                Assert.That(output, Is.Empty);
            }

            RegexConstraint DoesMatchExitCodeErrorMessage(int exitCode) =>
                Does.Match(@"^"
                            + Regex.Escape(@"Process ""dotnet"" (launched as the ID ")
                            + @"[1-9][0-9]*"
                            + Regex.Escape(
                                    FormattableString.Invariant(
                                        $@") ended with the non-zero exit code {exitCode}."))
                            + @"$");

            [Test]
            public void Error()
            {
                var e = Assert.Throws<ExternalProcessException>(() =>
                    TestAppStreams().AddArgument("error").AsEnumerable().ToArray());

                Assert.That(e.Message, DoesMatchExitCodeErrorMessage(0xbd));
            }

            [Test]
            public void LoremOutput()
            {
                var result = TestAppOutput().AddArgument("lorem", "3");

                Assert.That(result, Is.EqualTo(new[]
                {
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
                    "Nullam suscipit nunc non nulla euismod ornare.",
                    "Ut auctor felis lectus, eu cursus dolor ullamcorper ac.",
                }));
            }

            [Test]
            public void LoremError()
            {
                var result = TestAppError().AddArgument("lorem", "0", "3");

                Assert.That(result, Is.EqualTo(new[]
                {
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
                    "Nullam suscipit nunc non nulla euismod ornare.",
                    "Ut auctor felis lectus, eu cursus dolor ullamcorper ac.",
                }));
            }

            [Test]
            public void Lorem()
            {
                var (stdout, stderr) =
                    TestAppStreams().AddArgument("lorem", "3", "2", "4")
                                    .Partition(e => e is (OutputOrErrorKind.Output, _),
                                               (stdout, stderr) => (from e in stdout select e.Value,
                                                                    from e in stderr select e.Value));

                Assert.That(stdout, Is.EqualTo(new[]
                {
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
                    "Nullam suscipit nunc non nulla euismod ornare.",
                    "Ut auctor felis lectus, eu cursus dolor ullamcorper ac.",
                    "Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus.",
                    "Cras at ligula ut odio molestie egestas.",
                    "Sed sit amet dui porttitor, bibendum libero sed, porta velit.",
                    "Donec tristique risus vulputate elit hendrerit rutrum.",
                }));

                Assert.That(stderr, Is.EqualTo(new[]
                {
                    "Nam nec gravida justo.",
                    "Cras sed semper elit.",
                }));
            }

            [Test]
            public void Input()
            {
                var commands = new[]
                {
                    "nop",
                    "lorem 3 2 4",
                    "nop",
                    "exit",
                };

                var stdin = commands.ToObservable();

                var (stdout, stderr) =
                    TestAppStreams().Input(stdin)
                                    .Partition(e => e is (OutputOrErrorKind.Output, _),
                                               (stdout, stderr) => (from e in stdout select e.Value,
                                                                    from e in stderr select e.Value));


                Assert.That(stdout, Is.EqualTo(new[]
                {
                    "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
                    "Nullam suscipit nunc non nulla euismod ornare.",
                    "Ut auctor felis lectus, eu cursus dolor ullamcorper ac.",
                    "Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus.",
                    "Cras at ligula ut odio molestie egestas.",
                    "Sed sit amet dui porttitor, bibendum libero sed, porta velit.",
                    "Donec tristique risus vulputate elit hendrerit rutrum.",
                }));

                Assert.That(stderr, Is.EqualTo(new[]
                {
                    "Nam nec gravida justo.",
                    "Cras sed semper elit.",
                }));
            }

            [Test]
            public void Chain()
            {
                var commands = new[]
                {
                    "lorem 3 2 1",
                    "exit",
                };

                var (stdout, stderr) =
                    commands.ToObservable()
                            .Pipe(TestAppStreams())
                            .Pipe(TestAppStreams().AddArgument("upper"))
                            .Pipe(TestAppStreams().AddArgument("prefix", "> "))
                            .Partition(e => e is (OutputOrErrorKind.Output, _),
                                       (stdout, stderr) => (from e in stdout select e.Value,
                                                            from e in stderr select e.Value));

                Assert.That(stdout, Is.EqualTo(new[]
                {
                    "> LOREM IPSUM DOLOR SIT AMET, CONSECTETUR ADIPISCING ELIT.",
                    "> NULLAM SUSCIPIT NUNC NON NULLA EUISMOD ORNARE.",
                    "> UT AUCTOR FELIS LECTUS, EU CURSUS DOLOR ULLAMCORPER AC.",
                    "> ORCI VARIUS NATOQUE PENATIBUS ET MAGNIS DIS PARTURIENT MONTES, NASCETUR RIDICULUS MUS."
                }));

                Assert.That(stderr, Is.EqualTo(new[]
                {
                    "Nam nec gravida justo.",
                    "Cras sed semper elit.",
                }));
            }

            [Test]
            public async Task AsyncZeroExit()
            {
                var exitCode = await SpawnTestApp().AddArgument("nop").Async();
                Assert.That(exitCode, Is.Zero);
            }
            [Test]

            public async Task AsyncNonZeroExit()
            {
                var exitCode = await SpawnTestApp().AddArgument("exit", "42").Async();
                Assert.That(exitCode, Is.EqualTo(42));
            }

            [Test]
            public void AsyncCancellation()
            {
                var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(0.5));
                var e = Assert.ThrowsAsync<TaskCanceledException>(async () =>
                    _ = await SpawnTestApp().AddArgument("sleep", "3").Async(cts.Token));
                Assert.That(e.Task, Is.Not.Null);
            }
        }
    }
}
