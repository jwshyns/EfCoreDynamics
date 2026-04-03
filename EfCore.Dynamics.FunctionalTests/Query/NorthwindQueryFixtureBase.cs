using EfCore.Dynamics.FunctionalTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EfCore.Dynamics.FunctionalTests;

public abstract class NorthwindQueryFixtureBase<TModelCustomizer> : SharedStoreFixtureBase<NorthwindContext>, IQueryFixtureBase
    where TModelCustomizer : IModelCustomizer, new()
{
    protected NorthwindQueryFixtureBase()
    {
        var entitySorters = new Dictionary<Type, Func<dynamic, object>>
        {
            { typeof(Customer), e => e?.CustomerID },
            { typeof(CustomerView), e => e?.CompanyName },
            { typeof(Order), e => e?.OrderID },
            { typeof(OrderQuery), e => e?.CustomerID },
            { typeof(Employee), e => e?.EmployeeID },
            { typeof(Product), e => e?.ProductID },
            { typeof(OrderDetail), e => (e?.OrderID.ToString(), e?.ProductID.ToString()) }
        }.ToDictionary(e => e.Key, e => (object)e.Value);

        var entityAsserters = new Dictionary<Type, object>();

        QueryAsserter = new QueryAsserter<NorthwindContext>(
            CreateContext,
            new NorthwindData(),
            entitySorters,
            entityAsserters);
    }

    protected override string StoreName { get; } = "Northwind";

    protected override bool UsePooling => typeof(TModelCustomizer) == typeof(NoopModelCustomizer);

    public QueryAsserterBase QueryAsserter { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        => new TModelCustomizer().Customize(modelBuilder, context);

    protected override void Seed(NorthwindContext context) => NorthwindData.Seed(context);

    protected override Task SeedAsync(NorthwindContext context) => NorthwindData.SeedAsync(context);

    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base.AddOptions(builder).ConfigureWarnings(
            c => c
                .Log(CoreEventId.PossibleUnintendedCollectionNavigationNullComparisonWarning)
                .Log(CoreEventId.PossibleUnintendedReferenceComparisonWarning));
}