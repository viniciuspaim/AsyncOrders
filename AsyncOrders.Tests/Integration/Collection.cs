using Xunit;

namespace AsyncOrders.Tests.Integration;

[CollectionDefinition("containers")]
public sealed class ContainersCollection : ICollectionFixture<ContainersFixture> { }
