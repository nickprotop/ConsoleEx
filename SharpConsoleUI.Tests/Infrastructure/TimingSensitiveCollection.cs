using Xunit;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// Collection definition for timing-sensitive tests that rely on async callbacks,
/// thread wake signals, or tight deadlines. Tests in this collection run sequentially
/// to avoid flaky failures caused by thread pool contention under parallel execution.
/// </summary>
[CollectionDefinition("TimingSensitive", DisableParallelization = true)]
public class TimingSensitiveCollection { }
