using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EfCore.Dynamics.FunctionalTests;

public sealed class AsyncSimpleQueryDynamicsTest : AsyncSimpleQueryTestBase<NorthwindQueryDynamicsFixture<NoopModelCustomizer>>
{
    public AsyncSimpleQueryDynamicsTest(NorthwindQueryDynamicsFixture<NoopModelCustomizer> fixture) : base(fixture)
    {
    }
}