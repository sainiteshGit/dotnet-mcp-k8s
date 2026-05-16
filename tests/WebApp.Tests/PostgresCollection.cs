using WebApp.Tests.Integration;
using Xunit;

namespace WebApp.Tests;

[CollectionDefinition("Postgres")]
#pragma warning disable CA1711 // xUnit collection-fixture marker types conventionally end in 'Collection'
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    // Marker only — xUnit instantiates the fixture once per collection.
}
#pragma warning restore CA1711
