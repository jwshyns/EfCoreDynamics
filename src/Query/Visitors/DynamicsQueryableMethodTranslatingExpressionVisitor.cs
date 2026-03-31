using System;
using System.Linq;
using System.Linq.Expressions;
using EfCore.Dynamics365.Metadata;
using EfCore.Dynamics365.Query.Crm;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Query.Visitors;

/// <summary>
/// Translates LINQ queryable method calls (Where, Select, Take, Skip, OrderBy, etc.)
/// into mutations on a <see cref="DynamicsQueryExpression"/>.
/// </summary>
public sealed class DynamicsQueryableMethodTranslatingExpressionVisitor
    : QueryableMethodTranslatingExpressionVisitor
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
        var entityType = _model.GetEntityTypes()
                             .FirstOrDefault(e => e.ClrType == elementType)
                         ?? throw new InvalidOperationException(
                             $"Entity type {elementType.Name} is not registered in the model.");

        var entityLogicalName = entityType.GetEntityLogicalName()
                                ?? entityType.ClrType.Name.ToLowerInvariant();

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
        var prop = dynQuery.EntityType.FindProperty(m.Member.Name);
        var field = prop?.GetAttributeLogicalName() ?? m.Member.Name.ToLowerInvariant();
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
            setParam:    (d, n) => d.SetTopParameterName(n));
        return source;
    }

    protected override ShapedQueryExpression TranslateSkip(
        ShapedQueryExpression source,
        Expression count
    )
    {
        ApplyCountParam(count, (DynamicsQueryExpression)source.QueryExpression,
            setConstant: (d, v) => d.SetSkip(v),
            setParam:    (d, n) => d.SetSkipParameterName(n));
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
        if (predicate != null)
            source = TranslateWhere(source, predicate);
        ((DynamicsQueryExpression)source.QueryExpression).SetSingleRow();
        return source;
    }

    protected override ShapedQueryExpression TranslateSingleOrDefault(
        ShapedQueryExpression source,
        LambdaExpression predicate,
        Type returnType,
        bool returnDefault
    )
        => TranslateFirstOrDefault(source, predicate, returnType, returnDefault);

    // ── Count / LongCount ─────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateCount(
        ShapedQueryExpression source,
        LambdaExpression predicate
    )
    {
        if (predicate != null) TranslateWhere(source, predicate);
        return source;
    }

    protected override ShapedQueryExpression TranslateLongCount(
        ShapedQueryExpression source,
        LambdaExpression predicate
    )
        => TranslateCount(source, predicate);

    // ── Unsupported ───────────────────────────────────────────────────────

    protected override ShapedQueryExpression TranslateAll(ShapedQueryExpression source, LambdaExpression predicate) =>
        null!;

    protected override ShapedQueryExpression TranslateAny(ShapedQueryExpression source, LambdaExpression predicate) =>
        null!;

    protected override ShapedQueryExpression TranslateAverage(
        ShapedQueryExpression source,
        LambdaExpression selector,
        Type resultType
    ) => null!;

    protected override ShapedQueryExpression TranslateCast(ShapedQueryExpression source, Type resultType) => null!;

    protected override ShapedQueryExpression
        TranslateConcat(ShapedQueryExpression source, ShapedQueryExpression second) => null!;

    protected override ShapedQueryExpression TranslateContains(ShapedQueryExpression source, Expression item) => null!;

    protected override ShapedQueryExpression TranslateDefaultIfEmpty(
        ShapedQueryExpression source,
        Expression defaultValue
    ) => null!;

    protected override ShapedQueryExpression TranslateDistinct(ShapedQueryExpression source) => null!;

    protected override ShapedQueryExpression TranslateElementAtOrDefault(
        ShapedQueryExpression source,
        Expression index,
        bool returnDefault
    ) => null!;

    protected override ShapedQueryExpression
        TranslateExcept(ShapedQueryExpression source, ShapedQueryExpression second) => null!;

    protected override ShapedQueryExpression TranslateGroupBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        LambdaExpression elementSelector,
        LambdaExpression resultSelector
    ) => null!;

    protected override ShapedQueryExpression TranslateGroupJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector
    ) => null!;

    protected override ShapedQueryExpression TranslateIntersect(
        ShapedQueryExpression source,
        ShapedQueryExpression second
    ) => null!;

    protected override ShapedQueryExpression TranslateJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector
    ) => null!;

    protected override ShapedQueryExpression TranslateLastOrDefault(
        ShapedQueryExpression source,
        LambdaExpression predicate,
        Type returnType,
        bool returnDefault
    ) => null!;

    protected override ShapedQueryExpression TranslateLeftJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector
    ) => null!;

    protected override ShapedQueryExpression TranslateMax(
        ShapedQueryExpression source,
        LambdaExpression selector,
        Type resultType
    ) => null!;

    protected override ShapedQueryExpression TranslateMin(
        ShapedQueryExpression source,
        LambdaExpression selector,
        Type resultType
    ) => null!;

    protected override ShapedQueryExpression TranslateOfType(ShapedQueryExpression source, Type resultType) => null!;
    protected override ShapedQueryExpression TranslateReverse(ShapedQueryExpression source) => null!;

    protected override ShapedQueryExpression TranslateSelect(ShapedQueryExpression source, LambdaExpression selector) =>
        source;

    protected override ShapedQueryExpression TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression collectionSelector,
        LambdaExpression resultSelector
    ) => null!;

    protected override ShapedQueryExpression TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression selector
    ) => null!;

    protected override ShapedQueryExpression TranslateSkipWhile(
        ShapedQueryExpression source,
        LambdaExpression predicate
    ) => null!;

    protected override ShapedQueryExpression TranslateSum(
        ShapedQueryExpression source,
        LambdaExpression selector,
        Type resultType
    ) => null!;

    protected override ShapedQueryExpression TranslateTakeWhile(
        ShapedQueryExpression source,
        LambdaExpression predicate
    ) => null!;

    protected override ShapedQueryExpression
        TranslateUnion(ShapedQueryExpression source, ShapedQueryExpression second) => null!;

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ApplyCountParam(
        Expression count,
        DynamicsQueryExpression dynExpr,
        Action<DynamicsQueryExpression, int> setConstant,
        Action<DynamicsQueryExpression, string> setParam)
    {
        if (count is ConstantExpression { Value: int value })
            setConstant(dynExpr, value);
        else if (count is ParameterExpression { Name: { } name })
            setParam(dynExpr, name);
    }
}