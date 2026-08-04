using Xunit;

namespace SharpConsoleUI.Tests.Infrastructure;

/// <summary>
/// Collection definition for tests that measure <see cref="SharpConsoleUI.Controls.MarkupControl.TotalParseCount"/>.
/// That counter is process-wide, so any other test parsing markup on another thread lands inside the
/// delta being measured and the assertion reads someone else's work. Tests in this collection run
/// sequentially, and not alongside any other collection, so the delta describes only their own frame.
/// </summary>
[CollectionDefinition("ParseCounter", DisableParallelization = true)]
public class ParseCounterCollection { }
