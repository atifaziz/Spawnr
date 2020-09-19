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
        }

        [Test]
        public void Update()
        {
            var psi = new ProcessStartInfo();

            Assert.That(psi.WorkingDirectory, Is.Empty);
            Assert.That(psi.ArgumentList, Is.Empty);

            var options = SpawnOptions.Create().AddArgument("foo", "bar", "baz");
            options.Update(psi);

            Assert.That(psi.WorkingDirectory, Is.EqualTo(options.WorkingDirectory));
            Assert.That(psi.ArgumentList, Is.EqualTo(options.Arguments));

            var env = psi.Environment;
            Assert.That(env.Count, Is.EqualTo(options.Environment.Length));

            foreach (var e in options.Environment)
                Assert.That(env[e.Key], Is.EqualTo(e.Value));
        }
    }
}
