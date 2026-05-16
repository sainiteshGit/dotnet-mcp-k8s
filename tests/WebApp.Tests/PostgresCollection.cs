using WebApp.Tests.Integration;
using Xunit;

namespace WebApp.Tests;

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    // Marker only — xUnit instantiates the fixture once per collection.
}
