using System;
using System.Linq;
using System.Linq.Expressions;
using EfCore.Dynamics365.Metadata;
using EfCore.Dynamics365.Query.Crm;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Query.Visitors;

/// <summary>
/// Translates LINQ queryable method calls (Where, Select, Take, Skip, OrderBy, etc.)
/// into mutations on a <see cref="DynamicsQueryExpression"/>.
/// </summary>
internal sealed class DynamicsQueryableMethodTranslatingExpressionVisitor : QueryableMethodTranslatingExpressionVisitor
{
    private readonly IModel _model;
    private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;

    public DynamicsQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        IModel model
    )
        : base(dependencies, subquery: false)
    {
        _dependencies = dependencies;
        _model = model;
    }

    private DynamicsQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        IModel model,
        bool subquery
    )
        : base(dependencies, subquery)
    {
        _dependencies = dependencies;
        _model = model;
    }

    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new DynamicsQueryableMethodTranslatingExpressionVisitor(_dependencies, _model, subquery: true);

    // ── Entry point ───────────────────────────────────────────────────────

    protected override ShapedQueryExpression CreateShapedQueryExpression(Type elementType)
    {
        var entityType = _model.GetEntityTypes().FirstOrDefault(e => e.ClrType == elementType)
                         ?? throw new InvalidOperationException(
                             $"Entity type {elementType.Name} is not registered in the model.");

        var entityLogicalName = entityType.GetEntityLogicalName();

        var queryExpr = new DynamicsQueryExpression(entityType, entityLogicalName);
        var shaperExpr = new EntityShaperExpression(
            entityType,
            new ProjectionBindingExpression(queryExpr, new ProjectionMember(), typeof(ValueBuffer)),
            nullable: false);

        return new ShapedQueryExpression(queryExpr, shaperExpr);
    }

    // ── Where ─────────────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateWhere(
        ShapedQueryExpression source,
        LambdaExpression predicate
    )
    {
        var dynQuery = (DynamicsQueryExpression)source.QueryExpression;
        var visitor = new CrmFilterExpressionVisitor(dynQuery.EntityType, predicate.Parameters[0]);
        var filter = visitor.Translate(predicate.Body);
        dynQuery.AddFilter(filter);
        return source;
    }

    // ── OrderBy / ThenBy ──────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateOrderBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        bool ascending
    )
    {
        ApplyOrderBy(source, keySelector, ascending);
        return source;
    }

    protected override ShapedQueryExpression TranslateThenBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        bool ascending
    )
    {
        ApplyOrderBy(source, keySelector, ascending);
        return source;
    }

    private static void ApplyOrderBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        bool ascending
    )
    {
        if (keySelector.Body is not MemberExpression m) return;

        var dynQuery = (DynamicsQueryExpression)source.QueryExpression;
        var prop = dynQuery.EntityType.FindProperty(m.Member.Name) ?? throw new Exception();
        var field = prop.GetAttributeLogicalName();
        dynQuery.AddOrderBy(field, ascending);
    }

    // ── Take / Skip ───────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateTake(
        ShapedQueryExpression source,
        Expression count
    )
    {
        ApplyCountParam(count, (DynamicsQueryExpression)source.QueryExpression,
            setConstant: (d, v) => d.SetTop(v),
            setParam: (d, n) => d.SetTopParameterName(n));
        return source;
    }

    protected override ShapedQueryExpression TranslateSkip(
        ShapedQueryExpression source,
        Expression count
    )
    {
        ApplyCountParam(count, (DynamicsQueryExpression)source.QueryExpression,
            setConstant: (d, v) => d.SetSkip(v),
            setParam: (d, n) => d.SetSkipParameterName(n));
        return source;
    }

    // ── First / Single ────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateFirstOrDefault(
        ShapedQueryExpression source,
        LambdaExpression predicate,
        Type returnType,
        bool returnDefault
    )
    {
        source = TranslateWhere(source, predicate);
        ((DynamicsQueryExpression)source.QueryExpression).SetSingleRow();
        return source;
    }

    protected override ShapedQueryExpression TranslateSingleOrDefault(
        ShapedQueryExpression source,
        LambdaExpression predicate,
        Type returnType,
        bool returnDefault
    ) => TranslateFirstOrDefault(source, predicate, returnType, returnDefault);

    // ── Count / LongCount ─────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateCount(
        ShapedQueryExpression source,
        LambdaExpression predicate
    )
    {
        TranslateWhere(source, predicate);
        return source;
    }

    protected override ShapedQueryExpression TranslateLongCount(
        ShapedQueryExpression source,
        LambdaExpression predicate
    ) => TranslateCount(source, predicate);

    // ── Any ───────────────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateAny(ShapedQueryExpression source, LambdaExpression predicate)
    {
        source = TranslateWhere(source, predicate);
        ((DynamicsQueryExpression)source.QueryExpression).SetSingleRow();
        return source;
    }

    // ── Last / LastOrDefault ──────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateLastOrDefault(
        ShapedQueryExpression source,
        LambdaExpression predicate,
        Type returnType,
        bool returnDefault
    )
    {
        source = TranslateWhere(source, predicate);
        var dynQuery = (DynamicsQueryExpression)source.QueryExpression;
        dynQuery.ReverseOrders();
        dynQuery.SetSingleRow();
        return source;
    }

    // ── Reverse ───────────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateReverse(ShapedQueryExpression source)
    {
        ((DynamicsQueryExpression)source.QueryExpression).ReverseOrders();
        return source;
    }

    // ── Cast / OfType ─────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateCast(ShapedQueryExpression source, Type resultType) => source;

    protected override ShapedQueryExpression TranslateOfType(ShapedQueryExpression source, Type resultType) => source;

    // ── DefaultIfEmpty ────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateDefaultIfEmpty(
        ShapedQueryExpression source,
        Expression defaultValue
    ) => source;

    // ── Select ────────────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateSelect(ShapedQueryExpression source, LambdaExpression selector)
    {
        // Identity projection — nothing to do.
        if (selector.Body is ParameterExpression p && p == selector.Parameters[0])
            return source;

        var dynQuery = (DynamicsQueryExpression)source.QueryExpression;

        // Narrow the Dataverse ColumnSet to only the properties actually referenced.
        ExtractMemberAccesses(selector.Body, selector.Parameters[0], dynQuery);

        // Store the projection so the compilation visitor can apply it in-memory.
        dynQuery.SetProjection(selector);

        return source;
    }

    private static void ExtractMemberAccesses(
        Expression body,
        ParameterExpression param,
        DynamicsQueryExpression dynQuery
    )
    {
        while (true)
        {
            switch (body)
            {
                case MemberExpression m when m.Expression == param:
                {
                    if (dynQuery.EntityType.FindNavigation(m.Member.Name) != null)
                        throw new NotSupportedException(
                            $"Navigation property '{m.Member.Name}' in Select projections is not supported by the Dynamics 365 provider.");

                    var prop = dynQuery.EntityType.FindProperty(m.Member.Name);
                    if (prop != null) dynQuery.AddSelectField(prop.GetAttributeLogicalName());
                    break;
                }
                case NewExpression ne:
                    foreach (var arg in ne.Arguments) 
                        ExtractMemberAccesses(arg, param, dynQuery);
                    break;
                case MemberInitExpression mi:
                    ExtractMemberAccesses(mi.NewExpression, param, dynQuery);
                    foreach (var binding in mi.Bindings.OfType<MemberAssignment>())
                        ExtractMemberAccesses(binding.Expression, param, dynQuery);
                    break;
                case UnaryExpression u:
                    body = u.Operand;
                    continue;
                case ConditionalExpression c:
                    ExtractMemberAccesses(c.IfTrue, param, dynQuery);
                    body = c.IfFalse;
                    continue;
                case BinaryExpression b:
                    ExtractMemberAccesses(b.Left, param, dynQuery);
                    body = b.Right;
                    continue;
            }

            break;
        }
    }

    // ── Unsupported ───────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateAll(ShapedQueryExpression source, LambdaExpression predicate) =>
        throw new NotSupportedException("All() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateAverage(
        ShapedQueryExpression source,
        LambdaExpression selector,
        Type resultType
    ) => throw new NotSupportedException("Average() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateConcat(
        ShapedQueryExpression source,
        ShapedQueryExpression second
    ) => throw new NotSupportedException("Concat() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateContains(ShapedQueryExpression source, Expression item) =>
        throw new NotSupportedException("Contains() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateDistinct(ShapedQueryExpression source) =>
        throw new NotSupportedException("Distinct() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateElementAtOrDefault(
        ShapedQueryExpression source,
        Expression index,
        bool returnDefault
    ) => throw new NotSupportedException("ElementAt() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateExcept(
        ShapedQueryExpression source,
        ShapedQueryExpression second
    ) => throw new NotSupportedException("Except() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateGroupBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        LambdaExpression elementSelector,
        LambdaExpression resultSelector
    ) => throw new NotSupportedException("GroupBy() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateGroupJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector
    ) => throw new NotSupportedException("GroupJoin() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateIntersect(
        ShapedQueryExpression source,
        ShapedQueryExpression second
    ) => throw new NotSupportedException("Intersect() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector
    ) => throw new NotSupportedException("Join() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateLeftJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector
    ) => throw new NotSupportedException("LeftJoin() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateMax(
        ShapedQueryExpression source,
        LambdaExpression selector,
        Type resultType
    ) => throw new NotSupportedException("Max() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateMin(
        ShapedQueryExpression source,
        LambdaExpression selector,
        Type resultType
    ) => throw new NotSupportedException("Min() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression collectionSelector,
        LambdaExpression resultSelector
    ) => throw new NotSupportedException("SelectMany() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression selector
    ) => throw new NotSupportedException("SelectMany() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateSkipWhile(
        ShapedQueryExpression source,
        LambdaExpression predicate
    ) => throw new NotSupportedException("SkipWhile() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateSum(
        ShapedQueryExpression source,
        LambdaExpression selector,
        Type resultType
    ) => throw new NotSupportedException("Sum() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateTakeWhile(
        ShapedQueryExpression source,
        LambdaExpression predicate
    ) => throw new NotSupportedException("TakeWhile() is not supported by the Dynamics 365 provider.");

    protected override ShapedQueryExpression TranslateUnion(
        ShapedQueryExpression source,
        ShapedQueryExpression second
    ) => throw new NotSupportedException("Union() is not supported by the Dynamics 365 provider.");

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ApplyCountParam(
        Expression count,
        DynamicsQueryExpression dynExpr,
        Action<DynamicsQueryExpression, int> setConstant,
        Action<DynamicsQueryExpression, string> setParam
    )
    {
        switch (count)
        {
            case ConstantExpression { Value: int value }:
                setConstant(dynExpr, value);
                break;
            case ParameterExpression { Name: { } name }:
                setParam(dynExpr, name);
                break;
        }
    }
}