using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EfCore.Dynamics.FunctionalTests;

public class AsyncSimpleQueryDynamicsTest : AsyncSimpleQueryTestBase<NorthwindQueryDynamicsFixture<NoopModelCustomizer>>
{
    public AsyncSimpleQueryDynamicsTest(NorthwindQueryDynamicsFixture<NoopModelCustomizer> fixture) : base(fixture)
    {
    }
}