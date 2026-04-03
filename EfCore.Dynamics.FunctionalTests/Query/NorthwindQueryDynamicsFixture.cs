using EfCore.Dynamics.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EfCore.Dynamics.FunctionalTests;

public sealed class NorthwindQueryDynamicsFixture<TModelCustomizer> : NorthwindQueryFixtureBase<TModelCustomizer>
    where TModelCustomizer : IModelCustomizer, new()
{
    protected override ITestStoreFactory TestStoreFactory => DynamicsNorthwindTestStoreFactory.Instance;
}