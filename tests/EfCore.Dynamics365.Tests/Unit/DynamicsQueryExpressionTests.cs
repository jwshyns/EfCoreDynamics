using FluentAssertions;
using Microsoft.Xrm.Sdk.Query;
using EfCore.Dynamics365.Query;
using Xunit;

namespace EfCore.Dynamics365.Tests.Unit;

public class DynamicsQueryExpressionTests
{
    private static DynamicsQueryExpression Create(string logicalName = "account") => new(null!, logicalName);

    // ── Initial state ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_initialises_AllColumns_true()
    {
        var dqe = Create();

        dqe.BuildQueryExpression().ColumnSet.AllColumns.Should().BeTrue();
    }

    [Fact]
    public void Constructor_sets_EntityLogicalName()
    {
        var dqe = Create("contact");

        dqe.EntityLogicalName.Should().Be("contact");
        dqe.BuildQueryExpression().EntityName.Should().Be("contact");
    }

    [Fact]
    public void Constructor_has_no_TopCount_by_default()
    {
        var dqe = Create();

        dqe.BuildQueryExpression().TopCount.Should().BeNull();
    }

    // ── AddSelectField ────────────────────────────────────────────────────

    [Fact]
    public void AddSelectField_switches_from_AllColumns_to_explicit()
    {
        var dqe = Create();
        dqe.AddSelectField("name");

        var qs = dqe.BuildQueryExpression();
        qs.ColumnSet.AllColumns.Should().BeFalse();
        qs.ColumnSet.Columns.Should().Contain("name");
    }

    [Fact]
    public void AddSelectField_multiple_fields_are_accumulated()
    {
        var dqe = Create();
        dqe.AddSelectField("name");
        dqe.AddSelectField("revenue");

        dqe.BuildQueryExpression().ColumnSet.Columns.Should().BeEquivalentTo("name", "revenue");
    }

    [Fact]
    public void AddSelectField_deduplicates()
    {
        var dqe = Create();
        dqe.AddSelectField("name");
        dqe.AddSelectField("name");

        dqe.BuildQueryExpression().ColumnSet.Columns.Count.Should().Be(1);
    }

    // ── AddFilter ─────────────────────────────────────────────────────────

    [Fact]
    public void AddFilter_appends_to_Criteria()
    {
        var dqe    = Create();
        var filter = new FilterExpression(LogicalOperator.And);
        filter.AddCondition("name", ConditionOperator.Equal, "Acme");
        dqe.AddFilter(filter);

        dqe.BuildQueryExpression().Criteria.Filters.Count.Should().Be(1);
    }

    [Fact]
    public void AddFilter_multiple_filters_are_all_present()
    {
        var dqe = Create();
        var f1  = new FilterExpression(LogicalOperator.And);
        f1.AddCondition("name", ConditionOperator.Equal, "Acme");
        var f2  = new FilterExpression(LogicalOperator.And);
        f2.AddCondition("revenue", ConditionOperator.GreaterThan, 100m);
        dqe.AddFilter(f1);
        dqe.AddFilter(f2);

        dqe.BuildQueryExpression().Criteria.Filters.Count.Should().Be(2);
    }

    // ── AddOrderBy ────────────────────────────────────────────────────────

    [Fact]
    public void AddOrderBy_ascending_adds_correct_OrderExpression()
    {
        var dqe = Create();
        dqe.AddOrderBy("name", ascending: true);

        var order = dqe.BuildQueryExpression().Orders[0];
        order.AttributeName.Should().Be("name");
        order.OrderType.Should().Be(OrderType.Ascending);
    }

    [Fact]
    public void AddOrderBy_descending_adds_Descending_OrderExpression()
    {
        var dqe = Create();
        dqe.AddOrderBy("revenue", ascending: false);

        dqe.BuildQueryExpression().Orders[0].OrderType.Should().Be(OrderType.Descending);
    }

    // ── SetTop ────────────────────────────────────────────────────────────

    [Fact]
    public void SetTop_sets_TopCount()
    {
        var dqe = Create();
        dqe.SetTop(10);

        dqe.BuildQueryExpression().TopCount.Should().Be(10);
    }

    [Fact]
    public void SetTop_second_call_keeps_minimum()
    {
        var dqe = Create();
        dqe.SetTop(10);
        dqe.SetTop(5);

        dqe.BuildQueryExpression().TopCount.Should().Be(5);
    }

    [Fact]
    public void SetTop_larger_second_value_does_not_increase_TopCount()
    {
        var dqe = Create();
        dqe.SetTop(3);
        dqe.SetTop(100);

        dqe.BuildQueryExpression().TopCount.Should().Be(3);
    }

    // ── SetSkip ───────────────────────────────────────────────────────────

    [Fact]
    public void SetSkip_sets_Skip_property()
    {
        var dqe = Create();
        dqe.SetSkip(7);

        dqe.Skip.Should().Be(7);
    }

    // ── SetSingleRow ──────────────────────────────────────────────────────

    [Fact]
    public void SetSingleRow_marks_IsSingleRow_and_forces_TopCount_1()
    {
        var dqe = Create();
        dqe.SetSingleRow();

        dqe.IsSingleRow.Should().BeTrue();
        dqe.BuildQueryExpression().TopCount.Should().Be(1);
    }

    [Fact]
    public void SetSingleRow_after_larger_Take_still_forces_TopCount_1()
    {
        var dqe = Create();
        dqe.SetTop(50);
        dqe.SetSingleRow();

        dqe.BuildQueryExpression().TopCount.Should().Be(1);
    }
}