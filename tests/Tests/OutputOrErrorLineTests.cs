namespace Spawnr.Tests
{
    using System;
    using NUnit.Framework;

    public class OutputOrErrorLineTests
    {
        [Test]
        public void Default()
        {
            var output = new OutputOrErrorLine();
            Assert.That(output.Kind, Is.EqualTo(OutputOrErrorKind.Output));
            Assert.That(output.IsOutput(), Is.True);
            Assert.That(output.IsError(), Is.False);
            Assert.That(output.Value, Is.Null);
            Assert.That(output.ToString(), Is.Empty);
        }

        [Test]
        public void Error()
        {
            var error = OutputOrErrorLine.Error("foobar");
            Assert.That(error.Kind, Is.EqualTo(OutputOrErrorKind.Error));
            Assert.That(error.IsOutput(), Is.False);
            Assert.That(error.IsError(), Is.True);
            Assert.That(error.Value, Is.EqualTo("foobar"));
            Assert.That(error.ToString(), Is.EqualTo(error.Value));
            var (kind, line) = error;
            Assert.That(kind, Is.EqualTo(error.Kind));
            Assert.That(line, Is.EqualTo(error.Value));
        }

        [Test]
        public void Output()
        {
            var error = OutputOrErrorLine.Output("foobar");
            Assert.That(error.Kind, Is.EqualTo(OutputOrErrorKind.Output));
            Assert.That(error.IsOutput(), Is.True);
            Assert.That(error.IsError(), Is.False);
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
                OutputOrErrorLine.Output(null!));
            Assert.That(e.ParamName, Is.EqualTo("value"));
        }

        [Test]
        public void ErrorNullLineIsForbidden()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                OutputOrErrorLine.Error(null!));
            Assert.That(e.ParamName, Is.EqualTo("value"));
        }

        [Test]
        public void MatchWithNullError()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                OutputOrErrorLine.Output("foobar").Match(_ => 0, null!));
            Assert.That(e.ParamName, Is.EqualTo("error"));
        }

        [Test]
        public void MatchWithNullOutput()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                OutputOrErrorLine.Output("foobar").Match(null!, _ => 0));
            Assert.That(e.ParamName, Is.EqualTo("output"));
        }

        [Test]
        public void MatchOutput()
        {
            var result =
                OutputOrErrorLine.Output("foobar")
                                 .Match(s => s.ToUpperInvariant(),
                                        _ => throw new NotImplementedException());
            Assert.That(result, Is.EqualTo("FOOBAR"));
        }

        [Test]
        public void MatchError()
        {
            var result =
                OutputOrErrorLine.Error("foobar")
                                 .Match(_ => throw new NotImplementedException(),
                                        s => s.ToUpperInvariant());
            Assert.That(result, Is.EqualTo("FOOBAR"));
        }
    }
}
