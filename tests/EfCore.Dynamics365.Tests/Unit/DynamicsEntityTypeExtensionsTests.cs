using EfCore.Dynamics365.Metadata;
using EfCore.Dynamics365.Tests.Fixtures;
using EfCore.Dynamics365.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Linq;
using Xunit;

namespace EfCore.Dynamics365.Tests.Unit;

public class DynamicsEntityTypeExtensionsTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an IEntityType built from TestCrmContext (has full Dynamics annotations).
    /// </summary>
    private static IEntityType GetAnnotatedEntityType<T>() where T : class
    {
        var fakeCtx = Util.BuildContext();
        using var efCtx = EfCoreXrmTestHelper.CreateContext(fakeCtx.GetAsyncOrganizationService2());
        return efCtx.Model.GetEntityTypes().First(e => e.ClrType == typeof(T));
    }

    /// <summary>
    /// Builds a minimal entity type with no Dynamics annotations (convention-only).
    /// Uses an empty ConventionSet so no rules run automatically.
    /// </summary>
    private static IEntityType BuildConventionEntityType<T>(string keyPropertyName)
        where T : class
    {
        var mb = new ModelBuilder(new ConventionSet());
        mb.Entity<T>(b =>
        {
            b.Property<Guid>(keyPropertyName);
            b.HasKey(keyPropertyName);
        });
        return mb.FinalizeModel().FindEntityType(typeof(T))!;
    }

    // ── GetEntitySetName — annotation wins ────────────────────────────────

    [Fact]
    public void GetEntitySetName_returns_annotation_value_for_Account()
    {
        var entityType = GetAnnotatedEntityType<Account>();
        entityType.GetEntitySetName().Should().Be("accounts");
    }

    [Fact]
    public void GetEntitySetName_returns_annotation_value_for_Contact()
    {
        var entityType = GetAnnotatedEntityType<Contact>();
        entityType.GetEntitySetName().Should().Be("contacts");
    }

    // ── GetEntitySetName — convention pluralisation ───────────────────────

    [Fact]
    public void GetEntitySetName_convention_y_becomes_ies()
    {
        // Category → categories
        var et = BuildConventionEntityType<Category>("CategoryId");
        et.GetEntitySetName().Should().Be("categories");
    }

    [Fact]
    public void GetEntitySetName_convention_x_becomes_xes()
    {
        // Box → boxes
        var et = BuildConventionEntityType<Box>("BoxId");
        et.GetEntitySetName().Should().Be("boxes");
    }

    [Fact]
    public void GetEntitySetName_convention_ch_becomes_ches()
    {
        // Church → churches
        var et = BuildConventionEntityType<Church>("ChurchId");
        et.GetEntitySetName().Should().Be("churches");
    }

    [Fact]
    public void GetEntitySetName_convention_sh_becomes_shes()
    {
        // Dish → dishes
        var et = BuildConventionEntityType<Dish>("DishId");
        et.GetEntitySetName().Should().Be("dishes");
    }

    [Fact]
    public void GetEntitySetName_convention_s_becomes_ses()
    {
        // Status → statuses
        var et = BuildConventionEntityType<Status>("StatusId");
        et.GetEntitySetName().Should().Be("statuses");
    }

    // ── GetEntityLogicalName ──────────────────────────────────────────────

    [Fact]
    public void GetEntityLogicalName_returns_annotation_value()
    {
        var entityType = GetAnnotatedEntityType<Account>();
        entityType.GetEntityLogicalName().Should().Be("account");
    }

    [Fact]
    public void GetEntityLogicalName_convention_is_lowercase_type_name()
    {
        var et = BuildConventionEntityType<Category>("CategoryId");
        et.GetEntityLogicalName().Should().Be("category");
    }

    // ── GetAttributeLogicalName ───────────────────────────────────────────

    [Fact]
    public void GetAttributeLogicalName_returns_annotation_value()
    {
        var entityType = GetAnnotatedEntityType<Account>();
        var prop       = entityType.FindProperty(nameof(Account.AccountId))!;
        prop.GetAttributeLogicalName().Should().Be("accountid");
    }

    [Fact]
    public void GetAttributeLogicalName_Name_property_maps_correctly()
    {
        var entityType = GetAnnotatedEntityType<Account>();
        var prop       = entityType.FindProperty(nameof(Account.Name))!;
        prop.GetAttributeLogicalName().Should().Be("name");
    }

    [Fact]
    public void GetAttributeLogicalName_convention_is_lowercase_property_name()
    {
        var et   = BuildConventionEntityType<Category>("CategoryId");
        var prop = et.FindProperty("CategoryId")!;
        prop.GetAttributeLogicalName().Should().Be("categoryid");
    }
}