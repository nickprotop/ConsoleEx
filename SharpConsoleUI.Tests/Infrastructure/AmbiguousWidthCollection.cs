using Xunit;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// Collection definition for tests that mutate the process-wide ambiguous-width capability.
/// That flag changes what every width measurement in the library returns, so a concurrent test
/// measuring a string would see whichever policy these tests happened to have set. They run
/// sequentially, and not alongside any other collection.
/// </summary>
[CollectionDefinition("AmbiguousWidth", DisableParallelization = true)]
public class AmbiguousWidthCollection { }
