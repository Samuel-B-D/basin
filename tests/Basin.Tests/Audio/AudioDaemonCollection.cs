using Xunit;

namespace Basin.Tests;

[CollectionDefinition(Name)]
public sealed class AudioDaemonCollection : ICollectionFixture<AudioDaemonFixture>
{
    public const string Name = "private PipeWire daemon";
}
