using System;
using System.Linq;
using System.Linq.Expressions;
using EfCore.Dynamics365.Metadata;
using EfCore.Dynamics365.Query.OData;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCore.Dynamics365.Query
{
    /// <summary>
    /// Translates LINQ queryable method calls (Where, Select, Take, Skip, OrderBy, etc.)
    /// into mutations on a <see cref="DynamicsQueryExpression"/>.
    /// </summary>
    public sealed class DynamicsQueryableMethodTranslatingExpressionVisitor
        : QueryableMethodTranslatingExpressionVisitor
    {
        private readonly IModel _model;
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;

        // EF Core 3.1: base(dependencies, bool subquery)
        public DynamicsQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            IModel model)
            : base(dependencies, subquery: false)
        {
            _dependencies = dependencies;
            _model        = model;
        }

        private DynamicsQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            IModel model,
            bool subquery)
            : base(dependencies, subquery)
        {
            _dependencies = dependencies;
            _model        = model;
        }

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
            => new DynamicsQueryableMethodTranslatingExpressionVisitor(
                _dependencies, _model, subquery: true);

        // ── Entry point: scan for entity sets ────────────────────────────────

        protected override ShapedQueryExpression CreateShapedQueryExpression(Type elementType)
        {
            // IModel.FindEntityType(Type) does not exist in EF Core 3.1; use LINQ
            var entityType = _model.GetEntityTypes()
                                   .FirstOrDefault(e => e.ClrType == elementType)
                ?? throw new InvalidOperationException(
                    $"Entity type {elementType.Name} is not registered in the model.");

            var entitySetName = entityType.GetEntitySetName()
                ?? throw new InvalidOperationException(
                    $"Entity type {entityType.Name} has no Dynamics 365 entity-set name configured.");

            var queryExpr = new DynamicsQueryExpression(entityType, entitySetName);

            var shaperExpr = new EntityShaperExpression(
                entityType,
                new ProjectionBindingExpression(queryExpr, new ProjectionMember(), typeof(ValueBuffer)),
                nullable: false);

            return new ShapedQueryExpression(queryExpr, shaperExpr);
        }

        // ── Where ─────────────────────────────────────────────────────────────

        protected override ShapedQueryExpression TranslateWhere(
            ShapedQueryExpression source,
            LambdaExpression predicate)
        {
            var dynQuery   = (DynamicsQueryExpression)source.QueryExpression;
            var entityType = dynQuery.EntityType;
            var visitor    = new ODataFilterExpressionVisitor(entityType, predicate.Parameters[0]);
            var filter     = visitor.Translate(predicate.Body);

            if (!string.IsNullOrWhiteSpace(filter))
                dynQuery.AddFilter(filter);

            return source;
        }

        // ── OrderBy / ThenBy ──────────────────────────────────────────────────

        protected override ShapedQueryExpression TranslateOrderBy(
            ShapedQueryExpression source,
            LambdaExpression keySelector,
            bool ascending)
        {
            var clause = GetOrderByClause(source, keySelector, ascending);
            if (clause != null)
                ((DynamicsQueryExpression)source.QueryExpression).AddOrderBy(clause);
            return source;
        }

        protected override ShapedQueryExpression TranslateThenBy(
            ShapedQueryExpression source,
            LambdaExpression keySelector,
            bool ascending)
        {
            var clause = GetOrderByClause(source, keySelector, ascending);
            if (clause != null)
                ((DynamicsQueryExpression)source.QueryExpression).AddOrderBy(clause);
            return source;
        }

        private static string? GetOrderByClause(
            ShapedQueryExpression source,
            LambdaExpression keySelector,
            bool ascending)
        {
            if (keySelector.Body is MemberExpression m)
            {
                var dynQuery   = (DynamicsQueryExpression)source.QueryExpression;
                var entityType = dynQuery.EntityType;
                var prop       = entityType.FindProperty(m.Member.Name);
                var field      = prop?.GetAttributeLogicalName()
                                 ?? m.Member.Name.ToLowerInvariant();
                return field + (ascending ? " asc" : " desc");
            }
            return null;
        }

        // ── Take / Skip ───────────────────────────────────────────────────────

        protected override ShapedQueryExpression TranslateTake(
            ShapedQueryExpression source,
            Expression count)
        {
            if (count is ConstantExpression { Value: int top })
                ((DynamicsQueryExpression)source.QueryExpression).SetTop(top);
            return source;
        }

        protected override ShapedQueryExpression TranslateSkip(
            ShapedQueryExpression source,
            Expression count)
        {
            if (count is ConstantExpression { Value: int skip })
                ((DynamicsQueryExpression)source.QueryExpression).SetSkip(skip);
            return source;
        }

        // ── First / Single ────────────────────────────────────────────────────

        protected override ShapedQueryExpression TranslateFirstOrDefault(
            ShapedQueryExpression source,
            LambdaExpression predicate,
            Type returnType,
            bool returnDefault)
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
            bool returnDefault)
            => TranslateFirstOrDefault(source, predicate, returnType, returnDefault);

        // ── Count / LongCount ─────────────────────────────────────────────────

        protected override ShapedQueryExpression TranslateCount(
            ShapedQueryExpression source,
            LambdaExpression predicate)
        {
            if (predicate != null)
                TranslateWhere(source, predicate);
            return source;
        }

        protected override ShapedQueryExpression TranslateLongCount(
            ShapedQueryExpression source,
            LambdaExpression predicate)
            => TranslateCount(source, predicate);

        // ── Unsupported (return null → EF Core throws NotSupportedException) ──

        protected override ShapedQueryExpression TranslateAll(ShapedQueryExpression source, LambdaExpression predicate) => null!;
        protected override ShapedQueryExpression TranslateAny(ShapedQueryExpression source, LambdaExpression predicate) => null!;
        protected override ShapedQueryExpression TranslateAverage(ShapedQueryExpression source, LambdaExpression selector, Type resultType) => null!;
        protected override ShapedQueryExpression TranslateCast(ShapedQueryExpression source, Type resultType) => null!;
        protected override ShapedQueryExpression TranslateConcat(ShapedQueryExpression source, ShapedQueryExpression second) => null!;
        protected override ShapedQueryExpression TranslateContains(ShapedQueryExpression source, Expression item) => null!;
        protected override ShapedQueryExpression TranslateDefaultIfEmpty(ShapedQueryExpression source, Expression defaultValue) => null!;
        protected override ShapedQueryExpression TranslateDistinct(ShapedQueryExpression source) => null!;
        protected override ShapedQueryExpression TranslateElementAtOrDefault(ShapedQueryExpression source, Expression index, bool returnDefault) => null!;
        protected override ShapedQueryExpression TranslateExcept(ShapedQueryExpression source, ShapedQueryExpression second) => null!;
        protected override ShapedQueryExpression TranslateGroupBy(ShapedQueryExpression source, LambdaExpression keySelector, LambdaExpression elementSelector, LambdaExpression resultSelector) => null!;
        protected override ShapedQueryExpression TranslateGroupJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector) => null!;
        protected override ShapedQueryExpression TranslateIntersect(ShapedQueryExpression source, ShapedQueryExpression second) => null!;
        protected override ShapedQueryExpression TranslateJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector) => null!;
        protected override ShapedQueryExpression TranslateLastOrDefault(ShapedQueryExpression source, LambdaExpression predicate, Type returnType, bool returnDefault) => null!;
        protected override ShapedQueryExpression TranslateLeftJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector) => null!;
        protected override ShapedQueryExpression TranslateMax(ShapedQueryExpression source, LambdaExpression selector, Type resultType) => null!;
        protected override ShapedQueryExpression TranslateMin(ShapedQueryExpression source, LambdaExpression selector, Type resultType) => null!;
        protected override ShapedQueryExpression TranslateOfType(ShapedQueryExpression source, Type resultType) => null!;
        protected override ShapedQueryExpression TranslateReverse(ShapedQueryExpression source) => null!;
        protected override ShapedQueryExpression TranslateSelect(ShapedQueryExpression source, LambdaExpression selector) => source;
        protected override ShapedQueryExpression TranslateSelectMany(ShapedQueryExpression source, LambdaExpression collectionSelector, LambdaExpression resultSelector) => null!;
        protected override ShapedQueryExpression TranslateSelectMany(ShapedQueryExpression source, LambdaExpression selector) => null!;
        protected override ShapedQueryExpression TranslateSkipWhile(ShapedQueryExpression source, LambdaExpression predicate) => null!;
        protected override ShapedQueryExpression TranslateSum(ShapedQueryExpression source, LambdaExpression selector, Type resultType) => null!;
        protected override ShapedQueryExpression TranslateTakeWhile(ShapedQueryExpression source, LambdaExpression predicate) => null!;
        protected override ShapedQueryExpression TranslateUnion(ShapedQueryExpression source, ShapedQueryExpression second) => null!;
    }

    /// <inheritdoc />
    public sealed class DynamicsQueryableMethodTranslatingExpressionVisitorFactory
        : IQueryableMethodTranslatingExpressionVisitorFactory
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;

        public DynamicsQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        // EF Core 3.1: factory Create takes IModel, not QueryCompilationContext
        public QueryableMethodTranslatingExpressionVisitor Create(IModel model)
            => new DynamicsQueryableMethodTranslatingExpressionVisitor(_dependencies, model);
    }
}
