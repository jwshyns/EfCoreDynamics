using System;
using EfCore.Dynamics365.Client;
using EfCore.Dynamics365.Extensions;
using EfCore.Dynamics365.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EfCore.Dynamics365.Tests.Helpers;

/// <summary>
/// Wires a <see cref="TestCrmContext"/> backed by a supplied
/// <see cref="IOrganizationService"/> (typically from FakeXrmEasy).
/// </summary>
public static class EfCoreXrmTestHelper
{
    public static TestCrmContext CreateContext(IOrganizationServiceAsync2 orgService)
    {
        return new TestCrmContext(
            new DbContextOptionsBuilder<TestCrmContext>()
                .UseDynamics365(orgService)
                .Options);
    }

    // ── Entity factory helpers ────────────────────────────────────────────

    public static Entity AccountEntity(
        Guid id,
        string name,
        decimal? revenue = null,
        int? numberOfEmployees = null)
    {
        var e = new Entity("account", id);
        e.Attributes["accountid"] = id;
        e.Attributes["name"] = name;
        if (revenue.HasValue)
            e.Attributes["revenue"] = revenue.Value;
        if (numberOfEmployees.HasValue)
            e.Attributes["numberofemployees"] = numberOfEmployees.Value;
        return e;
    }

    public static Entity ContactEntity(Guid id, string firstName, string lastName)
    {
        var e = new Entity("contact", id);
        e.Attributes["contactid"] = id;
        e.Attributes["firstname"] = firstName;
        e.Attributes["lastname"] = lastName;
        return e;
    }

    /// <summary>
    /// Retrieves all records of a given logical name from the fake context
    /// via a plain RetrieveMultiple — useful for verifying CRUD outcomes.
    /// </summary>
    public static EntityCollection RetrieveAll(IOrganizationService svc, string logicalName)
        => svc.RetrieveMultiple(new QueryExpression(logicalName)
        {
            ColumnSet = new ColumnSet(true),
        });
}