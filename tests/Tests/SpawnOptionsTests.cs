namespace Spawnr.Tests
{
    using System;
    using System.Diagnostics;
    using NUnit.Framework;

    public class SpawnOptionsTests
    {
        [Test]
        public void Create()
        {
            var options = SpawnOptions.Create();

            Assert.That(options.Arguments.Count, Is.Zero);
            Assert.That(options.WorkingDirectory, Is.EqualTo(Environment.CurrentDirectory));

            var env = Environment.GetEnvironmentVariables();
            Assert.That(options.Environment.Length, Is.EqualTo(env.Count));

            foreach (var e in options.Environment)
                Assert.That(env[e.Key], Is.EqualTo(e.Value));

            Assert.That(options.CreateNoWindow, Is.False);
        }

        [Test]
        public void Update()
        {
            var psi = new ProcessStartInfo();

            Assert.That(psi.CreateNoWindow, Is.False);
            Assert.That(psi.WorkingDirectory, Is.Empty);
            Assert.That(psi.ArgumentList, Is.Empty);

            var options = SpawnOptions.Create()
                                      .AddArgument("foo", "bar", "baz")
                                      .CreateNoWindow();
            options.Update(psi);

            Assert.That(psi.CreateNoWindow, Is.True);
            Assert.That(psi.WorkingDirectory, Is.EqualTo(options.WorkingDirectory));
            Assert.That(psi.ArgumentList, Is.EqualTo(options.Arguments));

            var env = psi.Environment;
            Assert.That(env.Count, Is.EqualTo(options.Environment.Length));

            foreach (var e in options.Environment)
                Assert.That(env[e.Key], Is.EqualTo(e.Value));
        }

        [Test]
        public void ExitCodeErrorFunction_IsDefaulted()
        {
            var options = SpawnOptions.Create();

            Assert.That(options.ExitCodeErrorFunction, Is.Not.Null);
            Assert.That(options.ExitCodeErrorFunction, Is.SameAs(SpawnOptions.DefaultExitCodeErrorFunction));
        }

        [TestCase(0)]
        [TestCase(42)]
        public void DefaultExitCodeErrorFunction(int exitCode)
        {
            var ex = SpawnOptions.DefaultExitCodeErrorFunction(new ExitCodeErrorArgs("app", ProgramArguments.Var("arg1", "arg2"), 123, exitCode));

            Assert.That(ex.Message, Is.EqualTo(FormattableString.Invariant($"""Process "app" (launched as the ID 123) ended with the non-zero exit code {exitCode}.""")));
            Assert.That((int)ex.ExitCode, Is.EqualTo(exitCode));
        }

        [Test]
        public void IgnoreExitCode()
        {
            var options = SpawnOptions.Create();
            var before = options.ExitCodeErrorFunction;
            var after = options.IgnoreExitCode().ExitCodeErrorFunction;

            Assert.That(before, Is.Not.Null);
            Assert.That(after, Is.Null);
        }

        [Test]
        public void RequireZeroExitCode_ResetsToDefaultExitCodeErrorFunction()
        {
            var options = SpawnOptions.Create().IgnoreExitCode();
            var before = options.ExitCodeErrorFunction;
            var after = options.RequireZeroExitCode().ExitCodeErrorFunction;

            Assert.That(before, Is.Not.SameAs(after));
            Assert.That(after, Is.SameAs(SpawnOptions.DefaultExitCodeErrorFunction));
        }
    }
}
