using EfCore.Dynamics365.Extensions;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Dynamics.FunctionalTests.TestUtilities;

internal sealed class DynamicsNorthwindTestStoreFactory : ITestStoreFactory
{
    public static DynamicsNorthwindTestStoreFactory Instance { get; } = new ();

    private DynamicsNorthwindTestStoreFactory()
    {
    }

    public TestStore Create(string storeName) => DynamicsTestStore.Create(storeName);

    public TestStore GetOrCreate(string storeName)
    {
        return Create(storeName);
    }

    public IServiceCollection AddProviderServices(IServiceCollection serviceCollection) =>
        serviceCollection.AddEntityFrameworkDynamics365();

    public ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory) =>
        new TestDynamicsLoggerFactory(shouldLogCategory);
}