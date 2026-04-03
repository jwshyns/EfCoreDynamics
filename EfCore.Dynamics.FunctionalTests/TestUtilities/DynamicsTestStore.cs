using EfCore.Dynamics365.Extensions;
using FakeXrmEasy.Abstractions.Enums;
using FakeXrmEasy.Middleware;
using FakeXrmEasy.Middleware.Crud;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace EfCore.Dynamics.FunctionalTests.TestUtilities;

internal sealed class DynamicsTestStore : TestStore
{
    private IOrganizationServiceAsync2 OrganizationServiceAsync2 { get; }

    private DynamicsTestStore(string name) : base(name, true)
    {
        var fakedContext = MiddlewareBuilder
            .New()
            .AddCrud()
            .UseCrud()
            .SetLicense(FakeXrmEasyLicense.NonCommercial)
            .Build();
        
        OrganizationServiceAsync2 = fakedContext.GetAsyncOrganizationService2();
    }

    public static DynamicsTestStore Create(string name) => new(name);

    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder) =>
        builder.UseDynamics365(OrganizationServiceAsync2);

    public override void Clean(DbContext context)
    {
        // TODO:
    }
}