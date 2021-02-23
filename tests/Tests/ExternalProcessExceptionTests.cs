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

            Assert.That(e.ExitCode, Is.EqualTo(42));
            Assert.That(e.Message, Is.EqualTo("External process terminated with an exit code of 42."));
            Assert.That(e.InnerException, Is.Null);
        }

        [Test]
        public void InitWithMessage()
        {
            var e = new ExternalProcessException(42, "External process failed.");

            Assert.That(e.ExitCode, Is.EqualTo(42));
            Assert.That(e.Message, Is.EqualTo("External process failed."));
            Assert.That(e.InnerException, Is.Null);
        }

        [Test]
        public void InitWithMessageAndInnerException()
        {
            var inner = new Exception();
            var e = new ExternalProcessException(42, "External process failed.", inner);

            Assert.That(e.ExitCode, Is.EqualTo(42));
            Assert.That(e.Message, Is.EqualTo("External process failed."));
            Assert.That(e.InnerException, Is.SameAs(inner));
        }

        [Test]
        public void Serialization()
        {
            using var ms = new MemoryStream();
            var formatter = new BinaryFormatter();
            var e = new ExternalProcessException(42, "External process failed.", new Exception());
#pragma warning disable SYSLIB0011 // Type or member is obsolete
            formatter.Serialize(ms, e);
            ms.Position = 0;
            var de = (ExternalProcessException)formatter.Deserialize(ms);
#pragma warning restore SYSLIB0011 // Type or member is obsolete

            Assert.That(de.ExitCode, Is.EqualTo(e.ExitCode));
            Assert.That(de.Message, Is.EqualTo(e.Message));
            Assert.That(de.InnerException, Is.Not.Null);
        }
    }
}
