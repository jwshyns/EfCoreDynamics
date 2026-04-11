using System;
using System.Linq;
using System.Threading.Tasks;
using EfCore.Dynamics365.Tests.Fixtures;
using EfCore.Dynamics365.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EfCore.Dynamics365.Tests.Integration;

public class QueryTests
{
    [Fact]
    public async Task Include_includes()
    {
        var contact = new Contact
        {
            ContactId = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Smith"
        };

        var account = new Account
        {
            AccountId = Guid.NewGuid(),
            Name = "Acme",
            ContactId = contact.ContactId
        };

        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([contact, account]);
        await using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var result = await ctx.Accounts.Include(x => x.Contact).ToListAsync();

        result
            .Should()
            .BeEquivalentTo([
                new Account
                {
                    AccountId = account.AccountId,
                    Name = account.Name,
                    ContactId = contact.ContactId,
                    Contact = contact
                }
            ]);
    }

    // ── ToList / basic materialisation ────────────────────────────────────

    [Fact]
    public void ToList_returns_all_seeded_accounts()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([
            EfCoreXrmTestHelper.AccountEntity(id1, "Acme"),
            EfCoreXrmTestHelper.AccountEntity(id2, "FooBar")
        ]);
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var accounts = ctx.Accounts.ToList();

        accounts.Should().HaveCount(2);
        accounts.Select(a => a.AccountId).Should().BeEquivalentTo([id1, id2]);
    }

    [Fact]
    public void ToList_materialises_string_property()
    {
        var id = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([EfCoreXrmTestHelper.AccountEntity(id, "Contoso")]);
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var account = ctx.Accounts.ToList()[0];

        account.Name.Should().Be("Contoso");
    }

    [Fact]
    public void ToList_materialises_nullable_decimal_property()
    {
        var id = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([EfCoreXrmTestHelper.AccountEntity(id, "X", revenue: 99.50m)]);
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var account = ctx.Accounts.ToList()[0];

        account.Revenue.Should().Be(99.50m);
    }

    [Fact]
    public void ToList_materialises_nullable_int_property()
    {
        var id = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([
            EfCoreXrmTestHelper.AccountEntity(id, "X", numberOfEmployees: 42)
        ]);
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        ctx.Accounts.ToList()[0].NumberOfEmployees.Should().Be(42);
    }

    [Fact]
    public void Contact_ToList_materialises_firstname_and_lastname()
    {
        var id = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([EfCoreXrmTestHelper.ContactEntity(id, "John", "Doe")]);
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var contacts = ctx.Contacts.ToList();

        contacts.Should().HaveCount(1);
        contacts[0].ContactId.Should().Be(id);
        contacts[0].FirstName.Should().Be("John");
        contacts[0].LastName.Should().Be("Doe");
    }

    // ── Where ─────────────────────────────────────────────────────────────

    [Fact]
    public void Where_equal_filters_by_name()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([
            EfCoreXrmTestHelper.AccountEntity(id1, "Acme"),
            EfCoreXrmTestHelper.AccountEntity(id2, "FooBar")
        ]);
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var results = ctx.Accounts.Where(a => a.Name == "Acme").ToList();

        results.Should().HaveCount(1);
        results[0].AccountId.Should().Be(id1);
    }

    [Fact]
    public void Where_by_primary_key_returns_single_record()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([
            EfCoreXrmTestHelper.AccountEntity(id1, "Alpha"),
            EfCoreXrmTestHelper.AccountEntity(id2, "Beta")
        ]);
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var results = ctx.Accounts.Where(a => a.AccountId == id1).ToList();

        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Alpha");
    }

    // ── Take / Skip ───────────────────────────────────────────────────────

    [Fact]
    public void Take_limits_number_of_results()
    {
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize(Enumerable.Range(1, 5)
            .Select(i => EfCoreXrmTestHelper.AccountEntity(Guid.NewGuid(), $"Account{i}")));
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var results = ctx.Accounts.Take(3).ToList();

        results.Should().HaveCount(3);
    }

    [Fact]
    public void Skip_offsets_results_in_memory()
    {
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([
            EfCoreXrmTestHelper.AccountEntity(Guid.NewGuid(), "A"),
            EfCoreXrmTestHelper.AccountEntity(Guid.NewGuid(), "B"),
            EfCoreXrmTestHelper.AccountEntity(Guid.NewGuid(), "C")
        ]);
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var results = ctx.Accounts.Skip(2).ToList();

        results.Should().HaveCount(1);
    }

    // ── Async ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ToListAsync_returns_all_records()
    {
        var id = Guid.NewGuid();
        var xrmCtx = Util.BuildContext();
        xrmCtx.Initialize([EfCoreXrmTestHelper.AccountEntity(id, "AsyncTest")]);
        await using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        var results = await ctx.Accounts.ToListAsync();

        results.Should().HaveCount(1);
        results[0].AccountId.Should().Be(id);
    }

    // ── Empty result set ──────────────────────────────────────────────────

    [Fact]
    public void ToList_with_no_seeded_data_returns_empty_list()
    {
        var xrmCtx = Util.BuildContext();
        using var ctx = EfCoreXrmTestHelper.CreateContext(xrmCtx.GetAsyncOrganizationService2());

        ctx.Accounts.ToList().Should().BeEmpty();
    }
}