namespace Spawnr.Tests
{
    using System;
    using NUnit.Framework;

    public class OutputLineTests
    {
        [Test]
        public void Default()
        {
            var output = new OutputLine();
            Assert.That(output.Kind, Is.EqualTo(StandardOutputKind.Output));
            Assert.That(output.Value, Is.Null);
            Assert.That(output.ToString(), Is.Empty);
        }

        [Test]
        public void Error()
        {
            var error = OutputLine.Error("foobar");
            Assert.That(error.Kind, Is.EqualTo(StandardOutputKind.Error));
            Assert.That(error.Value, Is.EqualTo("foobar"));
            Assert.That(error.ToString(), Is.EqualTo(error.Value));
            var (kind, line) = error;
            Assert.That(kind, Is.EqualTo(error.Kind));
            Assert.That(line, Is.EqualTo(error.Value));
        }

        [Test]
        public void Output()
        {
            var error = OutputLine.Output("foobar");
            Assert.That(error.Kind, Is.EqualTo(StandardOutputKind.Output));
            Assert.That(error.Value, Is.EqualTo("foobar"));
            Assert.That(error.ToString(), Is.EqualTo(error.Value));
            var (kind, line) = error;
            Assert.That(kind, Is.EqualTo(error.Kind));
            Assert.That(line, Is.EqualTo(error.Value));
        }

        [Test]
        public void OutputNullLineIsForbidden()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                OutputLine.Output(null!));
            Assert.That(e.ParamName, Is.EqualTo("value"));
        }

        [Test]
        public void ErrorNullLineIsForbidden()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                OutputLine.Error(null!));
            Assert.That(e.ParamName, Is.EqualTo("value"));
        }
    }
}