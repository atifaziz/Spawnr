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
            Assert.That(output.Line, Is.Null);
            Assert.That(output.ToString(), Is.Empty);
        }

        [Test]
        public void Error()
        {
            var error = OutputLine.Error("foobar");
            Assert.That(error.Kind, Is.EqualTo(StandardOutputKind.Error));
            Assert.That(error.Line, Is.EqualTo("foobar"));
            Assert.That(error.ToString(), Is.EqualTo(error.Line));
            var (kind, line) = error;
            Assert.That(kind, Is.EqualTo(error.Kind));
            Assert.That(line, Is.EqualTo(error.Line));
        }

        [Test]
        public void Output()
        {
            var error = OutputLine.Output("foobar");
            Assert.That(error.Kind, Is.EqualTo(StandardOutputKind.Output));
            Assert.That(error.Line, Is.EqualTo("foobar"));
            Assert.That(error.ToString(), Is.EqualTo(error.Line));
            var (kind, line) = error;
            Assert.That(kind, Is.EqualTo(error.Kind));
            Assert.That(line, Is.EqualTo(error.Line));
        }

        [Test]
        public void OutputNullLineIsForbidden()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                OutputLine.Output(null!));
            Assert.That(e.ParamName, Is.EqualTo("line"));
        }

        [Test]
        public void ErrorNullLineIsForbidden()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                OutputLine.Error(null!));
            Assert.That(e.ParamName, Is.EqualTo("line"));
        }
    }
}
