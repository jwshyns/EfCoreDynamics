using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EfCore.Dynamics365.Query.Crm;
using EfCore.Dynamics365.Tests.Fixtures;
using EfCore.Dynamics365.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Xunit;

namespace EfCore.Dynamics365.Tests.Unit;

public class CrmFilterExpressionVisitorTests
{
    // ── Shared setup ──────────────────────────────────────────────────────

    private static IEntityType GetAccountEntityType()
    {
        var fakeCtx = Util.BuildContext();
        using var efCtx = EfCoreXrmTestHelper.CreateContext(fakeCtx.GetAsyncOrganizationService2());
        return efCtx.Model.GetEntityTypes().First(e => e.ClrType == typeof(Account));
    }

    private static (CrmFilterExpressionVisitor visitor, ParameterExpression param) CreateVisitor()
    {
        var entityType = GetAccountEntityType();
        var param = Expression.Parameter(typeof(Account), "a");
        return (new CrmFilterExpressionVisitor(entityType, param), param);
    }

    // ── Equality / null ───────────────────────────────────────────────────

    [Fact]
    public void Equal_string_produces_Equal_condition()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.Equal(
            Expression.Property(param, nameof(Account.Name)),
            Expression.Constant("Acme"));

        var filter = visitor.Translate(expr);

        filter.Conditions.Count.Should().Be(1);
        var cond = filter.Conditions[0];
        cond.AttributeName.Should().Be("name");
        cond.Operator.Should().Be(ConditionOperator.Equal);
        cond.Values[0].Should().Be("Acme");
    }

    [Fact]
    public void Equal_null_produces_Null_condition()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.Equal(
            Expression.Property(param, nameof(Account.Name)),
            Expression.Constant(null, typeof(string)));

        var filter = visitor.Translate(expr);

        filter.Conditions.Count.Should().Be(1);
        var cond = filter.Conditions[0];
        cond.AttributeName.Should().Be("name");
        cond.Operator.Should().Be(ConditionOperator.Null);
        cond.Values.Count.Should().Be(0);
    }

    [Fact]
    public void Null_equal_field_produces_Null_condition()
    {
        var (visitor, param) = CreateVisitor();
        // null == a.Name  (null on the left)
        var expr = Expression.Equal(
            Expression.Constant(null, typeof(string)),
            Expression.Property(param, nameof(Account.Name)));

        var filter = visitor.Translate(expr);

        filter.Conditions[0].Operator.Should().Be(ConditionOperator.Null);
        filter.Conditions[0].AttributeName.Should().Be("name");
    }

    [Fact]
    public void NotEqual_string_produces_NotEqual_condition()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.NotEqual(
            Expression.Property(param, nameof(Account.Name)),
            Expression.Constant("Acme"));

        var filter = visitor.Translate(expr);

        filter.Conditions.Count.Should().Be(1);
        var cond = filter.Conditions[0];
        cond.AttributeName.Should().Be("name");
        cond.Operator.Should().Be(ConditionOperator.NotEqual);
    }

    [Fact]
    public void NotEqual_null_produces_NotNull_condition()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.NotEqual(
            Expression.Property(param, nameof(Account.Name)),
            Expression.Constant(null, typeof(string)));

        var filter = visitor.Translate(expr);

        filter.Conditions.Count.Should().Be(1);
        filter.Conditions[0].AttributeName.Should().Be("name");
        filter.Conditions[0].Operator.Should().Be(ConditionOperator.NotNull);
    }

    // ── Relational comparisons ────────────────────────────────────────────

    [Fact]
    public void GreaterThan_produces_GreaterThan_condition()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.GreaterThan(
            Expression.Property(param, nameof(Account.Revenue)),
            Expression.Constant((decimal?)100m, typeof(decimal?)));

        var filter = visitor.Translate(expr);

        var cond = filter.Conditions[0];
        cond.AttributeName.Should().Be("revenue");
        cond.Operator.Should().Be(ConditionOperator.GreaterThan);
    }

    [Fact]
    public void GreaterThanOrEqual_produces_GreaterEqual_condition()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.GreaterThanOrEqual(
            Expression.Property(param, nameof(Account.Revenue)),
            Expression.Constant((decimal?)50m, typeof(decimal?)));

        var filter = visitor.Translate(expr);

        filter.Conditions[0].Operator.Should().Be(ConditionOperator.GreaterEqual);
    }

    [Fact]
    public void LessThan_produces_LessThan_condition()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.LessThan(
            Expression.Property(param, nameof(Account.Revenue)),
            Expression.Constant((decimal?)1000m, typeof(decimal?)));

        var filter = visitor.Translate(expr);

        filter.Conditions[0].Operator.Should().Be(ConditionOperator.LessThan);
    }

    [Fact]
    public void LessThanOrEqual_produces_LessEqual_condition()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.LessThanOrEqual(
            Expression.Property(param, nameof(Account.Revenue)),
            Expression.Constant((decimal?)500m, typeof(decimal?)));

        var filter = visitor.Translate(expr);

        filter.Conditions[0].Operator.Should().Be(ConditionOperator.LessEqual);
    }

    [Fact]
    public void Field_on_right_side_flips_directional_operator()
    {
        var (visitor, param) = CreateVisitor();
        // 100 < a.Revenue  →  should produce GreaterThan on revenue
        var expr = Expression.LessThan(
            Expression.Constant((decimal?)100m, typeof(decimal?)),
            Expression.Property(param, nameof(Account.Revenue)));

        var filter = visitor.Translate(expr);

        var cond = filter.Conditions[0];
        cond.AttributeName.Should().Be("revenue");
        cond.Operator.Should().Be(ConditionOperator.GreaterThan);
    }

    // ── Logical operators ─────────────────────────────────────────────────

    [Fact]
    public void AndAlso_merges_simple_conditions_into_flat_And_filter()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.AndAlso(
            Expression.Equal(
                Expression.Property(param, nameof(Account.Name)),
                Expression.Constant("Acme")),
            Expression.Equal(
                Expression.Property(param, nameof(Account.EMailAddress1)),
                Expression.Constant("hello@acme.com")));

        var filter = visitor.Translate(expr);

        filter.FilterOperator.Should().Be(LogicalOperator.And);
        filter.Conditions.Count.Should().Be(2);
        filter.Filters.Count.Should().Be(0);
    }

    [Fact]
    public void OrElse_produces_Or_filter_with_sub_filters()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.OrElse(
            Expression.Equal(
                Expression.Property(param, nameof(Account.Name)),
                Expression.Constant("Acme")),
            Expression.Equal(
                Expression.Property(param, nameof(Account.Name)),
                Expression.Constant("FooBar")));

        var filter = visitor.Translate(expr);

        filter.FilterOperator.Should().Be(LogicalOperator.Or);
        filter.Filters.Count.Should().Be(2);
        filter.Conditions.Count.Should().Be(0);
    }

    [Fact]
    public void Not_over_Equal_produces_NotEqual()
    {
        var (visitor, param) = CreateVisitor();
        var inner = Expression.Equal(
            Expression.Property(param, nameof(Account.Name)),
            Expression.Constant("Acme"));

        var filter = visitor.Translate(Expression.Not(inner));

        filter.Conditions.Count.Should().Be(1);
        filter.Conditions[0].AttributeName.Should().Be("name");
        filter.Conditions[0].Operator.Should().Be(ConditionOperator.NotEqual);
        filter.Conditions[0].Values[0].Should().Be("Acme");
    }

    [Fact]
    public void Not_over_Null_check_produces_NotNull()
    {
        var (visitor, param) = CreateVisitor();
        var inner = Expression.Equal(
            Expression.Property(param, nameof(Account.Name)),
            Expression.Constant(null, typeof(string)));

        var filter = visitor.Translate(Expression.Not(inner));

        filter.Conditions[0].Operator.Should().Be(ConditionOperator.NotNull);
    }

    // ── String methods ────────────────────────────────────────────────────

    [Fact]
    public void String_Contains_produces_Like_with_percent_wildcards()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.Call(
            Expression.Property(param, nameof(Account.Name)),
            typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!,
            Expression.Constant("corp"));

        var filter = visitor.Translate(expr);

        var cond = filter.Conditions[0];
        cond.Operator.Should().Be(ConditionOperator.Like);
        cond.Values[0].Should().Be("%corp%");
    }

    [Fact]
    public void String_StartsWith_produces_BeginsWith()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.Call(
            Expression.Property(param, nameof(Account.Name)),
            typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!,
            Expression.Constant("Ac"));

        var filter = visitor.Translate(expr);

        filter.Conditions[0].Operator.Should().Be(ConditionOperator.BeginsWith);
        filter.Conditions[0].Values[0].Should().Be("Ac");
    }

    [Fact]
    public void String_EndsWith_produces_EndsWith()
    {
        var (visitor, param) = CreateVisitor();
        var expr = Expression.Call(
            Expression.Property(param, nameof(Account.Name)),
            typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!,
            Expression.Constant("Inc"));

        var filter = visitor.Translate(expr);

        filter.Conditions[0].Operator.Should().Be(ConditionOperator.EndsWith);
        filter.Conditions[0].Values[0].Should().Be("Inc");
    }

    // ── Enumerable.Contains ───────────────────────────────────────────────

    [Fact]
    public void Enumerable_Contains_produces_In_condition_with_all_values()
    {
        var (visitor, param) = CreateVisitor();
        var names = new List<string> { "Acme", "FooBar", "Contoso" };
        var containsMethod = typeof(Enumerable)
            .GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));
        var expr = Expression.Call(
            null,
            containsMethod,
            Expression.Constant(names),
            Expression.Property(param, nameof(Account.Name)));

        var filter = visitor.Translate(expr);

        var cond = filter.Conditions[0];
        cond.AttributeName.Should().Be("name");
        cond.Operator.Should().Be(ConditionOperator.In);
        cond.Values.Count.Should().Be(3);
    }

    // ── Logical name from annotation ──────────────────────────────────────

    [Fact]
    public void Uses_annotation_logical_name_for_attribute()
    {
        var (visitor, param) = CreateVisitor();
        // EMailAddress1 is annotated as "emailaddress1"
        var expr = Expression.Equal(
            Expression.Property(param, nameof(Account.EMailAddress1)),
            Expression.Constant("test@example.com"));

        var filter = visitor.Translate(expr);

        filter.Conditions[0].AttributeName.Should().Be("emailaddress1");
    }

    // ── Captured closure value ────────────────────────────────────────────

    [Fact]
    public void Evaluates_captured_closure_variable()
    {
        var (visitor, param) = CreateVisitor();
        const string capturedName = "Closure Corp";
        // Build expression that refers to capturedName via a captured variable
        Expression<Func<Account, bool>> lambda = a => a.Name == capturedName;
        var binaryExpr = (BinaryExpression)lambda.Body;

        var filter = visitor.Translate(binaryExpr);

        filter.Conditions[0].Operator.Should().Be(ConditionOperator.Equal);
        filter.Conditions[0].Values[0].Should().Be("Closure Corp");
    }

    // ── Unsupported node ──────────────────────────────────────────────────

    [Fact]
    public void Unsupported_node_type_throws_NotSupportedException()
    {
        var (visitor, _) = CreateVisitor();
        // Bitwise AND is not a supported logical expression type
        var expr = Expression.And(Expression.Constant(1), Expression.Constant(2));

        Action act = () => visitor.Translate(expr);

        act.Should().Throw<NotSupportedException>();
    }
}