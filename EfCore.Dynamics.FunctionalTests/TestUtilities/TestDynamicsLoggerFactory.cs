using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EfCore.Dynamics.FunctionalTests.TestUtilities;

internal sealed class TestDynamicsLoggerFactory : ListLoggerFactory
{
    public TestDynamicsLoggerFactory(Func<string, bool> shouldLogCategory) : base(shouldLogCategory)
    {
    }
}