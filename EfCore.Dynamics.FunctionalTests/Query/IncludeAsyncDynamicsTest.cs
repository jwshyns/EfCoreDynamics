using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EfCore.Dynamics.FunctionalTests;

public sealed class IncludeAsyncDynamicsTest : IncludeAsyncTestBase<NorthwindQueryDynamicsFixture<NoopModelCustomizer>>
{
    public IncludeAsyncDynamicsTest(NorthwindQueryDynamicsFixture<NoopModelCustomizer> fixture) : base(fixture)
    {
    }
}