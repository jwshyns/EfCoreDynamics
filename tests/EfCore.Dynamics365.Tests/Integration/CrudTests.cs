using System;
using System.Linq;
using EfCore.Dynamics365.Tests.Fixtures;
using EfCore.Dynamics365.Tests.Helpers;
using FluentAssertions;
using Microsoft.Xrm.Sdk.Query;
using Xunit;

namespace EfCore.Dynamics365.Tests.Integration;

public class CrudTests
{
    // ── Create (Add + SaveChanges) ─────────────────────────────────────────

    [Fact]
    public void Add_and_SaveChanges_creates_record_in_fake_service()
    {
        var xrmCtx = Util.BuildContext();
        var orgService = xrmCtx.GetAsyncOrganizationService2();
        using var ctx = EfCoreXrmTestHelper.CreateContext(orgService);

        ctx.Accounts.Add(new Account { Name = "New Corp" });
        ctx.SaveChanges();

        var all = EfCoreXrmTestHelper.RetrieveAll(orgService, "account");
        all.Entities.Should().HaveCount(1);
        all.Entities[0]["name"].Should().Be("New Corp");
    }

    [Fact]
    public void Add_and_SaveChanges_writes_back_generated_primary_key()
    {
        var xrmCtx = Util.BuildContext();
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var account = new Account { Name = "Generated ID Test" };
        ctx.Accounts.Add(account);
        ctx.SaveChanges();

        account.AccountId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Add_contact_and_SaveChanges_stores_firstname_lastname()
    {
        var xrmCtx = Util.BuildContext();
        var orgService = xrmCtx.GetAsyncOrganizationService2();
        using var ctx = EfCoreXrmTestHelper.CreateContext(orgService);

        ctx.Contacts.Add(new Contact { FirstName = "Jane", LastName = "Smith" });
        ctx.SaveChanges();

        var all = EfCoreXrmTestHelper.RetrieveAll(orgService, "contact");
        all.Entities.Should().HaveCount(1);
        all.Entities[0]["firstname"].Should().Be("Jane");
        all.Entities[0]["lastname"].Should().Be("Smith");
    }

    // ── Update (Modify + SaveChanges) ─────────────────────────────────────

    [Fact]
    public void Modify_tracked_entity_and_SaveChanges_updates_record()
    {
        var id = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        var orgService = xrmCtx.GetAsyncOrganizationService2();
        xrmCtx.Initialize(new[] { EfCoreXrmTestHelper.AccountEntity(id, "Old Name") });
        using var ctx = EfCoreXrmTestHelper.CreateContext(orgService);

        var account = ctx.Accounts.First(a => a.AccountId == id);
        account.Name = "New Name";
        ctx.SaveChanges();

        var updated = orgService.Retrieve("account", id, new ColumnSet(true));
        updated["name"].Should().Be("New Name");
    }

    // ── Delete (Remove + SaveChanges) ─────────────────────────────────────

    [Fact]
    public void Remove_tracked_entity_and_SaveChanges_deletes_record()
    {
        var id = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        var orgService = xrmCtx.GetAsyncOrganizationService2();
        xrmCtx.Initialize(new[] { EfCoreXrmTestHelper.AccountEntity(id, "To Delete") });
        using var ctx = EfCoreXrmTestHelper.CreateContext(orgService);

        var account = ctx.Accounts.First(a => a.AccountId == id);
        ctx.Accounts.Remove(account);
        ctx.SaveChanges();

        EfCoreXrmTestHelper.RetrieveAll(orgService, "account").Entities.Should().BeEmpty();
    }

    // ── Multiple operations in one SaveChanges ────────────────────────────

    [Fact]
    public void SaveChanges_handles_add_and_delete_in_same_call()
    {
        var existingId = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        var orgService = xrmCtx.GetAsyncOrganizationService2();
        xrmCtx.Initialize(new[] { EfCoreXrmTestHelper.AccountEntity(existingId, "Old") });
        using var ctx = EfCoreXrmTestHelper.CreateContext(orgService);

        // Delete the existing record
        var existing = ctx.Accounts.First(a => a.AccountId == existingId);
        ctx.Accounts.Remove(existing);

        // Add a new record
        ctx.Accounts.Add(new Account { Name = "Brand New" });

        ctx.SaveChanges();

        var all = EfCoreXrmTestHelper.RetrieveAll(orgService, "account");
        all.Entities.Should().HaveCount(1);
        all.Entities[0]["name"].Should().Be("Brand New");
    }
}