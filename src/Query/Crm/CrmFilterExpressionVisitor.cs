using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using EfCore.Dynamics365.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace EfCore.Dynamics365.Query.Crm;

/// <summary>
/// Recursively walks a LINQ predicate expression and produces a
/// <see cref="FilterExpression"/> suitable for a Dataverse <see cref="QueryExpression"/>.
///
/// Supported:
///   == / != (incl. null checks)  &gt;  &gt;=  &lt;  &lt;=
///   &amp;&amp;  ||  !
///   string.Contains / StartsWith / EndsWith
///   Enumerable.Contains → In / NotIn
///   captured closure values
/// </summary>
internal sealed class CrmFilterExpressionVisitor
{
    private readonly IEntityType _entityType;
    private readonly ParameterExpression _parameter;

    public CrmFilterExpressionVisitor(IEntityType entityType, ParameterExpression parameter)
    {
        _entityType = entityType;
        _parameter = parameter;
    }

    public FilterExpression Translate(Expression expression) => BuildFilter(expression);

    // ── Recursive dispatch ────────────────────────────────────────────────

    private FilterExpression BuildFilter(Expression expr)
    {
        // Strip type conversions transparently
        while (expr is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u)
            expr = u.Operand;

        switch (expr.NodeType)
        {
            case ExpressionType.AndAlso:
            {
                var b = (BinaryExpression)expr;
                var filter = new FilterExpression(LogicalOperator.And);
                MergeInto(filter, BuildFilter(b.Left));
                MergeInto(filter, BuildFilter(b.Right));
                return filter;
            }

            case ExpressionType.OrElse:
            {
                var b = (BinaryExpression)expr;
                var filter = new FilterExpression(LogicalOperator.Or);
                filter.AddFilter(BuildFilter(b.Left));
                filter.AddFilter(BuildFilter(b.Right));
                return filter;
            }

            case ExpressionType.Not:
            {
                var u = (UnaryExpression)expr;
                return NegateFilter(BuildFilter(u.Operand));
            }

            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.GreaterThan:
            case ExpressionType.GreaterThanOrEqual:
            case ExpressionType.LessThan:
            case ExpressionType.LessThanOrEqual:
                return BuildComparisonFilter((BinaryExpression)expr);

            case ExpressionType.Call:
                return BuildMethodFilter((MethodCallExpression)expr);

            default:
                throw new NotSupportedException(
                    $"Expression node {expr.NodeType} is not supported in Dynamics 365 filters.");
        }
    }

    /// <summary>
    /// Merges a child filter's conditions directly into the parent when they share
    /// the same logical operator and the child has no nested sub-filters.
    /// This keeps the tree flat for simple AND chains.
    /// </summary>
    private static void MergeInto(FilterExpression parent, FilterExpression child)
    {
        if (child.FilterOperator == parent.FilterOperator && child.Filters.Count == 0)
        {
            foreach (var c in child.Conditions) parent.AddCondition(c);
        }
        else
        {
            parent.AddFilter(child);
        }
    }

    // ── Comparison ────────────────────────────────────────────────────────

    private FilterExpression BuildComparisonFilter(BinaryExpression node)
    {
        var filter = new FilterExpression(LogicalOperator.And);

        // Null checks ──────────────────────────────────────────────────────
        if (node.NodeType == ExpressionType.Equal)
        {
            if (IsNullConstant(node.Right))
            {
                filter.AddCondition(FieldName(node.Left), ConditionOperator.Null);
                return filter;
            }

            if (IsNullConstant(node.Left))
            {
                filter.AddCondition(FieldName(node.Right), ConditionOperator.Null);
                return filter;
            }
        }

        if (node.NodeType == ExpressionType.NotEqual)
        {
            if (IsNullConstant(node.Right))
            {
                filter.AddCondition(FieldName(node.Left), ConditionOperator.NotNull);
                return filter;
            }

            if (IsNullConstant(node.Left))
            {
                filter.AddCondition(FieldName(node.Right), ConditionOperator.NotNull);
                return filter;
            }
        }

        // Normal value comparison ─────────────────────────────────────────
        var leftIsMember = IsMemberOnParameter(node.Left);

        var fieldExpr = leftIsMember ? node.Left : node.Right;
        var valueExpr = leftIsMember ? node.Right : node.Left;

        // Flip directional operators when the field is on the right side
        var nodeType = node.NodeType;
        if (!leftIsMember)
            nodeType = nodeType switch
            {
                ExpressionType.GreaterThan => ExpressionType.LessThan,
                ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
                ExpressionType.LessThan => ExpressionType.GreaterThan,
                ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
                _ => nodeType,
            };

        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        var op = nodeType switch
        {
            ExpressionType.Equal => ConditionOperator.Equal,
            ExpressionType.NotEqual => ConditionOperator.NotEqual,
            ExpressionType.GreaterThan => ConditionOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => ConditionOperator.GreaterEqual,
            ExpressionType.LessThan => ConditionOperator.LessThan,
            ExpressionType.LessThanOrEqual => ConditionOperator.LessEqual,
            _ => throw new NotSupportedException($"Operator {nodeType} is not supported."),
        };

        filter.AddCondition(FieldName(fieldExpr), op, Evaluate(valueExpr));
        return filter;
    }

    // ── Method calls ──────────────────────────────────────────────────────

    private FilterExpression BuildMethodFilter(MethodCallExpression node)
    {
        var filter = new FilterExpression(LogicalOperator.And);

        // string.Contains / StartsWith / EndsWith
        if (node.Object?.Type == typeof(string) && node.Arguments.Count >= 1)
        {
            var memberName = MemberName(node.Object);
            if (memberName != null)
            {
                var logicalName = LogicalName(memberName);
                if (Evaluate(node.Arguments[0]) is string arg)
                {
                    switch (node.Method.Name)
                    {
                        case "Contains":
                            filter.AddCondition(logicalName, ConditionOperator.Like, $"%{arg}%");
                            return filter;
                        case "StartsWith":
                            filter.AddCondition(logicalName, ConditionOperator.BeginsWith, arg);
                            return filter;
                        case "EndsWith":
                            filter.AddCondition(logicalName, ConditionOperator.EndsWith, arg);
                            return filter;
                    }
                }
            }
        }

        // Enumerable.Contains(collection, member)  →  field in (…)
        if (node.Method.DeclaringType == typeof(Enumerable)
            && node.Method.Name == "Contains"
            && node.Arguments.Count == 2)
        {
            var collection = Evaluate(node.Arguments[0]);
            var memberName = MemberName(node.Arguments[1]);
            if (memberName != null && collection is IEnumerable enumerable)
            {
                var logicalName = LogicalName(memberName);
                var values = enumerable.Cast<object>().ToArray();
                filter.AddCondition(logicalName, ConditionOperator.In, values);
                return filter;
            }
        }

        throw new NotSupportedException(
            $"Method {node.Method.DeclaringType?.Name}.{node.Method.Name} is not supported in Dynamics 365 filters.");
    }

    // ── NOT ───────────────────────────────────────────────────────────────

    private static FilterExpression NegateFilter(FilterExpression filter)
    {
        // Works for a single-condition filter (the common NOT case).
        if (filter.Conditions.Count == 1 && filter.Filters.Count == 0)
        {
            var cond = filter.Conditions[0];
            var negated = NegateOperator(cond.Operator);
            if (!negated.HasValue)
                throw new NotSupportedException(
                    "NOT expressions are only supported over simple equality / null / in conditions. " +
                    "Complex NOT (e.g. NOT over AND/OR) is not supported.");
            
            var result = new FilterExpression(LogicalOperator.And);
            if (cond.Values.Count > 0)
                result.AddCondition(cond.AttributeName, negated.Value,
                    cond.Values.Cast<object>().ToArray());
            else
                result.AddCondition(cond.AttributeName, negated.Value);
            return result;
        }

        throw new NotSupportedException(
            "NOT expressions are only supported over simple equality / null / in conditions. " +
            "Complex NOT (e.g. NOT over AND/OR) is not supported.");
    }

    private static ConditionOperator? NegateOperator(ConditionOperator op) => op switch
    {
        ConditionOperator.Equal => ConditionOperator.NotEqual,
        ConditionOperator.NotEqual => ConditionOperator.Equal,
        ConditionOperator.Null => ConditionOperator.NotNull,
        ConditionOperator.NotNull => ConditionOperator.Null,
        ConditionOperator.Like => ConditionOperator.NotLike,
        ConditionOperator.NotLike => ConditionOperator.Like,
        ConditionOperator.In => ConditionOperator.NotIn,
        ConditionOperator.NotIn => ConditionOperator.In,
        ConditionOperator.BeginsWith => ConditionOperator.DoesNotBeginWith,
        ConditionOperator.DoesNotBeginWith => ConditionOperator.BeginsWith,
        ConditionOperator.EndsWith => ConditionOperator.DoesNotEndWith,
        ConditionOperator.DoesNotEndWith => ConditionOperator.EndsWith,
        _ => null,
    };

    // ── Helpers ───────────────────────────────────────────────────────────

    private string FieldName(Expression expr)
    {
        if (MemberName(expr) is { } memberName) return LogicalName(memberName);
        
        throw new NotSupportedException($"Cannot extract a field name from: {expr}");
    }

    private string LogicalName(string clrPropertyName)
    {
        var prop = _entityType.FindProperty(clrPropertyName);
        return prop?.GetAttributeLogicalName() ?? clrPropertyName.ToLowerInvariant();
    }

    private bool IsMemberOnParameter(Expression expr) =>
        expr is MemberExpression m
        && (IsEntityParameter(m.Expression)
            || (m.Expression is UnaryExpression u && IsEntityParameter(u.Operand)));

    private bool IsEntityParameter(Expression? expr) =>
        expr is ParameterExpression p && p.Type == _entityType.ClrType;

    private static string? MemberName(Expression? expr) => expr switch
    {
        MemberExpression m => m.Member.Name,
        UnaryExpression { Operand: MemberExpression m2 } => m2.Member.Name,
        _ => null
    };

    private static bool IsNullConstant(Expression expr) => expr is ConstantExpression { Value: null };

    private static object? Evaluate(Expression expr)
    {
        while (true)
        {
            switch (expr)
            {
                case ConstantExpression c:
                    return c.Value;
                case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u:
                    expr = u.Operand;
                    continue;
                default:
                    try
                    {
                        return Expression.Lambda(expr).Compile().DynamicInvoke();
                    }
                    catch
                    {
                        return null;
                    }
            }
        }
    }
}