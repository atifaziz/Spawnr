namespace Spawnr.Tests
{
    using System;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using NUnit.Framework;

    public class ExternalProcessExceptionTests
    {
        [Test]
        public void Init()
        {
            var e = new ExternalProcessException(42);

            Assert.That((int)e.ExitCode, Is.EqualTo(42));
            Assert.That(e.Message, Is.EqualTo("External process terminated with an exit code of 42."));
            Assert.That(e.InnerException, Is.Null);
        }

        [Test]
        public void InitWithMessage()
        {
            var e = new ExternalProcessException(42, "External process failed.");

            Assert.That((int)e.ExitCode, Is.EqualTo(42));
            Assert.That(e.Message, Is.EqualTo("External process failed."));
            Assert.That(e.InnerException, Is.Null);
        }

        [Test]
        public void InitWithMessageAndInnerException()
        {
            var inner = new Exception();
            var e = new ExternalProcessException(42, "External process failed.", inner);

            Assert.That((int)e.ExitCode, Is.EqualTo(42));
            Assert.That(e.Message, Is.EqualTo("External process failed."));
            Assert.That(e.InnerException, Is.SameAs(inner));
        }
    }
}
