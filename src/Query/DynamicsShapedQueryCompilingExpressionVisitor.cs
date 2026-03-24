using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using EfCore.Dynamics365.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Newtonsoft.Json.Linq;

namespace EfCore.Dynamics365.Query
{
    /// <summary>
    /// Compiles a <see cref="ShapedQueryExpression"/> (containing a
    /// <see cref="DynamicsQueryExpression"/>) into an executable delegate that
    /// hits the Dataverse Web API and materialises CLR entities.
    /// </summary>
    public sealed class DynamicsShapedQueryCompilingExpressionVisitor
        : ShapedQueryCompilingExpressionVisitor
    {
        private readonly bool _isAsync;

        public DynamicsShapedQueryCompilingExpressionVisitor(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, queryCompilationContext)
        {
            _isAsync = queryCompilationContext.IsAsync;
        }

        protected override Expression VisitShapedQueryExpression(
            ShapedQueryExpression shapedQueryExpression)
        {
            var dynExpr    = (DynamicsQueryExpression)shapedQueryExpression.QueryExpression;
            var shaperExpr = shapedQueryExpression.ShaperExpression;
            var entityType = dynExpr.EntityType;

            // Build the call: DynamicsQueryExecutor.ExecuteAsync<T>(context, queryExpr)
            // or  DynamicsQueryExecutor.Execute<T>(context, queryExpr)
            var clrType = entityType.ClrType;

            var queryContextParam = Expression.Parameter(typeof(QueryContext), "queryContext");
            var dynQueryConst     = Expression.Constant(dynExpr);
            var entityTypeConst   = Expression.Constant(entityType);

            if (_isAsync)
            {
                var method = typeof(DynamicsQueryExecutor)
                    .GetMethod(nameof(DynamicsQueryExecutor.ExecuteAsync))!
                    .MakeGenericMethod(clrType);

                return Expression.Lambda(
                    Expression.Call(null, method,
                        queryContextParam, dynQueryConst, entityTypeConst,
                        Expression.Constant(CancellationToken.None)),
                    queryContextParam);
            }
            else
            {
                var method = typeof(DynamicsQueryExecutor)
                    .GetMethod(nameof(DynamicsQueryExecutor.Execute))!
                    .MakeGenericMethod(clrType);

                return Expression.Lambda(
                    Expression.Call(null, method,
                        queryContextParam, dynQueryConst, entityTypeConst),
                    queryContextParam);
            }
        }
    }

    /// <summary>
    /// Static helpers invoked by compiled query delegates at runtime.
    /// </summary>
    public static class DynamicsQueryExecutor
    {
        public static IEnumerable<T> Execute<T>(
            QueryContext queryContext,
            DynamicsQueryExpression query,
            IEntityType entityType)
            where T : class
        {
            var ctx    = (DynamicsQueryContext)queryContext;
            var rows   = ctx.Client.QueryAsync(
                query.EntitySetName,
                query.BuildODataQueryString()).GetAwaiter().GetResult();

            return rows.Select(row => Materialise<T>(row, entityType));
        }

        public static async IAsyncEnumerable<T> ExecuteAsync<T>(
            QueryContext queryContext,
            DynamicsQueryExpression query,
            IEntityType entityType,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
            where T : class
        {
            var ctx  = (DynamicsQueryContext)queryContext;
            var rows = await ctx.Client.QueryAsync(
                query.EntitySetName,
                query.BuildODataQueryString(),
                cancellationToken).ConfigureAwait(false);

            foreach (var row in rows)
                yield return Materialise<T>(row, entityType);
        }

        // ── Materialisation ───────────────────────────────────────────────────

        private static T Materialise<T>(JObject row, IEntityType entityType)
            where T : class
        {
            var instance = Activator.CreateInstance<T>();

            foreach (var prop in entityType.GetProperties())
            {
                var logicalName = prop.GetAttributeLogicalName() ?? prop.Name.ToLowerInvariant();

                // Try direct name then logical name
                if (!row.TryGetValue(prop.Name, StringComparison.OrdinalIgnoreCase, out var token))
                    row.TryGetValue(logicalName, StringComparison.OrdinalIgnoreCase, out token);

                if (token == null || token.Type == JTokenType.Null)
                    continue;

                try
                {
                    var clrValue = ConvertToken(token, prop.ClrType);
                    prop.PropertyInfo?.SetValue(instance, clrValue);
                }
                catch
                {
                    // Skip properties that cannot be mapped
                }
            }

            return instance;
        }

        private static object? ConvertToken(JToken token, Type targetType)
        {
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlying == typeof(Guid))
                return Guid.Parse(token.Value<string>()!);
            if (underlying == typeof(DateTime))
                return token.Value<DateTime>();
            if (underlying == typeof(DateTimeOffset))
                return token.Value<DateTimeOffset>();
            if (underlying == typeof(bool))
                return token.Value<bool>();
            if (underlying == typeof(int))
                return token.Value<int>();
            if (underlying == typeof(long))
                return token.Value<long>();
            if (underlying == typeof(decimal))
                return token.Value<decimal>();
            if (underlying == typeof(double))
                return token.Value<double>();
            if (underlying == typeof(float))
                return token.Value<float>();
            if (underlying == typeof(string))
                return token.Value<string>();

            if (underlying.IsEnum)
                return Enum.ToObject(underlying, token.Value<int>());

            return token.ToObject(targetType);
        }
    }

    /// <inheritdoc />
    public sealed class DynamicsShapedQueryCompilingExpressionVisitorFactory
        : IShapedQueryCompilingExpressionVisitorFactory
    {
        private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies;

        public DynamicsShapedQueryCompilingExpressionVisitorFactory(
            ShapedQueryCompilingExpressionVisitorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public ShapedQueryCompilingExpressionVisitor Create(
            QueryCompilationContext queryCompilationContext)
            => new DynamicsShapedQueryCompilingExpressionVisitor(
                _dependencies, queryCompilationContext);
    }
}
