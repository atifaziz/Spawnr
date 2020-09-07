namespace Spawnr.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reactive;
    using NUnit.Framework;
    using static SpawnModule;

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
            Assert.That(notification.Exception, Is.TypeOf<Exception>());
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
    using System.Reflection;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using NUnit.Framework.Constraints;
    using static MoreLinq.Extensions.PartitionExtension;
    using static SpawnModule;

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

            static ISpawnable<(Std Stream, string Line)> TestAppStreams() =>
                Spawn("dotnet", ProgramArguments.Var(_testAppPath!),
                      s => (Std.Out, s), s => (Std.Err, s));

            static ISpawnable<string> TestAppOutput() =>
                Spawn("dotnet", ProgramArguments.Var(_testAppPath!));

            static ISpawnable<string> TestAppError() =>
                Spawn("dotnet", ProgramArguments.Var(_testAppPath!),
                      stdout: null, stderr: s => s);

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
                var e = Assert.Throws<Exception>(() =>
                    TestAppStreams().AddArgument("error").ToArray());

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
                                    .Partition(s => s.Stream == Std.Out);

                Assert.That(stdout, Is.EqualTo(new[]
                {
                    (Std.Out, "Lorem ipsum dolor sit amet, consectetur adipiscing elit."),
                    (Std.Out, "Nullam suscipit nunc non nulla euismod ornare."),
                    (Std.Out, "Ut auctor felis lectus, eu cursus dolor ullamcorper ac."),
                    (Std.Out, "Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus."),
                    (Std.Out, "Cras at ligula ut odio molestie egestas."),
                    (Std.Out, "Sed sit amet dui porttitor, bibendum libero sed, porta velit."),
                    (Std.Out, "Donec tristique risus vulputate elit hendrerit rutrum."),
                }));

                Assert.That(stderr, Is.EqualTo(new[]
                {
                    (Std.Err, "Nam nec gravida justo."),
                    (Std.Err, "Cras sed semper elit."),
                }));
            }
        }
    }
}
