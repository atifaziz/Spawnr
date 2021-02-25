namespace Spawnr.Tests
{
    using System;
    using NUnit.Framework;

    public class ExitCodeTests
    {
        [Test]
        public void DefaultValueIsSuccess()
        {
            Assert.That(default(ExitCode).IsSuccess, Is.True);
        }

        [Test]
        public void Zero()
        {
            var zero = new ExitCode(0);
            Assert.That(zero.IsSuccess, Is.True);
            Assert.That(zero.IsError, Is.False);
            Assert.That((int)zero, Is.Zero);
            Assert.IsTrue(zero == 0);
            if (zero)
                Assert.Pass();
            else
                Assert.Fail();
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void NonZero(int code)
        {
            ExitCode nonZero = code;
            Assert.That(nonZero.IsSuccess, Is.False);
            Assert.That(nonZero.IsError, Is.True);
            Assert.That((int)nonZero, Is.EqualTo(code));
            Assert.IsTrue(nonZero != 0);
            if (nonZero)
                Assert.Fail();
            else
                Assert.Pass();
        }

        [Test]
        public void StringRepresentation()
        {
            ExitCode code = 42;
            Assert.That(code.ToString(), Is.EqualTo("42"));
        }

        public class ThrowIfError
        {
            [Test]
            public void Success()
            {
                ExitCode code = 0;
                void Act() => code.ThrowIfError();
                Assert.DoesNotThrow(Act);
            }

            [Test]
            public void Error()
            {
                ExitCode code = 42;
                void Act() => code.ThrowIfError();
                var e = Assert.Throws<ExternalProcessException>(Act);
                Assert.That(e?.ExitCode, Is.EqualTo(code));
            }
        }
    }
}
