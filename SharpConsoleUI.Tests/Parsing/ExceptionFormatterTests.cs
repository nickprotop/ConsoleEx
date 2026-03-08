using System;
using System.IO;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing
{
    public class ExceptionFormatterTests
    {
        [Fact]
        public void SimpleException_ContainsTypeNameAndMessage()
        {
            var ex = new InvalidOperationException("Something went wrong");
            var output = CaptureOutput(ex);

            Assert.Contains("System.InvalidOperationException:", output);
            Assert.Contains("Something went wrong", output);
        }

        [Fact]
        public void ExceptionWithStackTrace_ContainsAtAndMethodNames()
        {
            var ex = CreateExceptionWithStackTrace();
            var output = CaptureOutput(ex);

            Assert.Contains("at", output);
            Assert.Contains("CreateExceptionWithStackTrace", output);
        }

        [Fact]
        public void NestedInnerException_BothExceptionsAppear()
        {
            var inner = new NullReferenceException("Object reference not set");
            var outer = new InvalidOperationException("Outer failed", inner);
            var output = CaptureOutput(outer);

            Assert.Contains("InvalidOperationException", output);
            Assert.Contains("Outer failed", output);
            Assert.Contains("--- Inner Exception ---", output);
            Assert.Contains("NullReferenceException", output);
            Assert.Contains("Object reference not set", output);
        }

        [Fact]
        public void AggregateException_AllInnerExceptionsAppear()
        {
            var ex = new AggregateException("Multiple failures",
                new ArgumentException("Bad argument"),
                new IOException("IO failed"));
            var output = CaptureOutput(ex);

            Assert.Contains("AggregateException", output);
            Assert.Contains("ArgumentException", output);
            Assert.Contains("Bad argument", output);
            Assert.Contains("IOException", output);
            Assert.Contains("IO failed", output);
        }

        [Fact]
        public void NullException_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ExceptionFormatter.WriteException(null!));
        }

        [Fact]
        public void OutputGoesToProvidedTextWriter()
        {
            var ex = new Exception("test message");
            using var writer = new StringWriter();

            ExceptionFormatter.WriteException(ex, writer);

            var output = writer.ToString();
            Assert.Contains("test message", output);
        }

        [Fact]
        public void OutputContainsAnsiReset()
        {
            var ex = new Exception("test");
            var output = CaptureOutput(ex);

            Assert.EndsWith("\x1b[0m", output);
        }

        [Fact]
        public void ExceptionWithNoStackTrace_JustTypeAndMessage()
        {
            var ex = new Exception("no stack");
            var output = CaptureOutput(ex);

            Assert.Contains("System.Exception:", output);
            Assert.Contains("no stack", output);
            Assert.DoesNotContain("   at", output);
        }

        private static string CaptureOutput(Exception ex)
        {
            using var writer = new StringWriter();
            ExceptionFormatter.WriteException(ex, writer);
            return writer.ToString();
        }

        private static Exception CreateExceptionWithStackTrace()
        {
            try
            {
                throw new InvalidOperationException("Test exception");
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
