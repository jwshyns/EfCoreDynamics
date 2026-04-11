using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EfCore.Dynamics365.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Xrm.Sdk;

namespace EfCore.Dynamics365.Query.Visitors;

/// <summary>
/// Compiles a <see cref="ShapedQueryExpression"/> into an executable delegate
/// that calls the Dataverse Web API and materialises CLR entities.
/// </summary>
internal sealed class DynamicsShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
{
    private readonly bool _isAsync;

    public DynamicsShapedQueryCompilingExpressionVisitor(
        ShapedQueryCompilingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext
    )
        : base(dependencies, queryCompilationContext)
    {
        _isAsync = queryCompilationContext.IsAsync;
    }

    protected override Expression VisitShapedQueryExpression(ShapedQueryExpression shapedQueryExpression)
    {
        var dynExpr = (DynamicsQueryExpression)shapedQueryExpression.QueryExpression;
        var entityType = dynExpr.EntityType;
        var clrType = entityType.ClrType;

        var queryContextParam = QueryCompilationContext.QueryContextParameter;
        var dynQueryConst = Expression.Constant(dynExpr);
        var entityTypeConst = Expression.Constant(entityType);

        // ── Joined / included query path ──────────────────────────────────
        if (dynExpr.Links.Count > 0)
        {
            var entityParam = Expression.Parameter(typeof(Entity), "entity");
            Expression transformerBody;
            Type resultType;

            if (dynExpr.Links.All(IsIncludeLink))
            {
                // Include path: materialise root + linked entities, set reference
                // navigation properties from the EF Core model, return the root entity.
                // (The result selector returns TransparentIdentifier — bypassed entirely.)
                (transformerBody, resultType) = BuildIncludeTransformer(
                    dynExpr,
                    clrType,
                    entityType,
                    entityParam,
                    entityTypeConst
                );
            }
            else
            {
                // Regular LINQ Join path: compose result selectors inline (beta-reduction)
                // so EF Core's outer compilation keeps QueryContext parameters in scope.
                Expression current = Expression.Call(
                    MaterialiseMethod.MakeGenericMethod(clrType),
                    entityParam, entityTypeConst
                );

                foreach (var link in dynExpr.Links)
                {
                    var innerClrType = link.InnerEntityType.ClrType;

                    var innerMaterialised = Expression.Call(
                        MaterialiseLinkedMethod.MakeGenericMethod(innerClrType),
                        entityParam,
                        Expression.Constant(link.InnerEntityType),
                        Expression.Constant(link.Alias)
                    );

                    current = InlineLambda(link.ResultSelector!, current, innerMaterialised);
                }

                transformerBody = current;
                resultType = current.Type;
            }

            var funcType = typeof(Func<,>).MakeGenericType(typeof(Entity), resultType);
            var rowTransformer = Expression.Lambda(funcType, transformerBody, entityParam);

            if (_isAsync)
            {
                var executeMethod = typeof(DynamicsQueryExecutor)
                    .GetMethod(nameof(DynamicsQueryExecutor.ExecuteWithTransformerAsync))!
                    .MakeGenericMethod(resultType);

                return Expression.Call(
                    null,
                    executeMethod,
                    queryContextParam,
                    dynQueryConst,
                    rowTransformer,
                    Expression.Constant(CancellationToken.None) // TODO: figure out how to add cancellation support
                );
            }
            else
            {
                var executeMethod = typeof(DynamicsQueryExecutor)
                    .GetMethod(nameof(DynamicsQueryExecutor.ExecuteWithTransformer))!
                    .MakeGenericMethod(resultType);

                return Expression.Call(
                    null,
                    executeMethod,
                    queryContextParam,
                    dynQueryConst,
                    rowTransformer
                );
            }
        }

        // ── Simple (non-join) path ────────────────────────────────────────
        if (_isAsync)
        {
            var executeMethod = typeof(DynamicsQueryExecutor)
                .GetMethod(nameof(DynamicsQueryExecutor.ExecuteAsync))!
                .MakeGenericMethod(clrType);

            Expression result = Expression.Call(
                null, executeMethod,
                queryContextParam, dynQueryConst, entityTypeConst,
                Expression.Constant(CancellationToken.None));

            if (dynExpr.Projection is { } asyncProjection)
            {
                var projType = asyncProjection.ReturnType;

                var selectAsyncMethod = typeof(DynamicsQueryExecutor)
                    .GetMethod(nameof(DynamicsQueryExecutor.SelectAsync))!
                    .MakeGenericMethod(clrType, projType);

                var compiled = CompileProjection(asyncProjection, clrType, projType);

                result = Expression.Call(
                    null,
                    selectAsyncMethod,
                    result,
                    compiled,
                    Expression.Constant(CancellationToken.None) // TODO: figure out how to add cancellation support
                );
            }

            return result;
        }
        else
        {
            var executeMethod = typeof(DynamicsQueryExecutor)
                .GetMethod(nameof(DynamicsQueryExecutor.Execute))!
                .MakeGenericMethod(clrType);

            Expression result = Expression.Call(
                null, executeMethod,
                queryContextParam, dynQueryConst, entityTypeConst);

            if (dynExpr.Projection is { } projection)
            {
                var projType = projection.ReturnType;

                var selectMethod = typeof(Enumerable)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == nameof(Enumerable.Select)
                                && m.GetParameters().Length == 2
                                && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
                    .MakeGenericMethod(clrType, projType);

                var compiled = CompileProjection(projection, clrType, projType);

                result = Expression.Call(null, selectMethod, result, compiled);
            }

            return result;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Expression CompileProjection(LambdaExpression projection, Type entityType, Type projType)
    {
        var funcType = typeof(Func<,>).MakeGenericType(entityType, projType);
        var typedLambda = Expression.Lambda(funcType, projection.Body, projection.Parameters);
        return Expression.Constant(typedLambda.Compile(), funcType);
    }

    /// <summary>
    /// Beta-reduces <paramref name="lambda"/> by substituting each parameter with the
    /// corresponding argument expression. The result is an inlined expression tree with
    /// no <see cref="InvocationExpression"/> wrapper, so EF Core's outer compilation
    /// retains full access to <c>QueryContext</c> parameter references.
    /// </summary>
    private static Expression InlineLambda(LambdaExpression lambda, params Expression[] args)
    {
        var body = lambda.Body;
        for (var i = 0; i < lambda.Parameters.Count && i < args.Length; i++)
            body = new ParameterReplacer(lambda.Parameters[i], args[i]).Visit(body);
        return body;
    }

    // ── Include helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Builds a row-transformer for a query that uses only Include-style joins.
    /// Materialises the root entity and every linked entity, assigns each to the
    /// appropriate reference navigation property (determined from the EF Core model),
    /// then returns the root entity. The <c>TransparentIdentifier</c> result selectors
    /// produced by EF Core's navigation expander are ignored entirely.
    /// </summary>
    private static (Expression body, Type resultType) BuildIncludeTransformer(
        DynamicsQueryExpression dynExpr,
        Type rootClrType,
        IEntityType rootEntityType,
        ParameterExpression entityParam,
        ConstantExpression rootEntityTypeConst
    )
    {
        var allVars = new List<ParameterExpression>();
        var exprs = new List<Expression>();

        // Root entity variable
        var rootVar = Expression.Variable(rootClrType, "root");
        allVars.Add(rootVar);
        exprs.Add(Expression.Assign(rootVar, Expression.Call(
            MaterialiseMethod.MakeGenericMethod(rootClrType), entityParam, rootEntityTypeConst)));

        // Materialise all linked entities into named variables
        var varByAlias = new Dictionary<string, ParameterExpression>();
        var entityTypeByAlias = new Dictionary<string, IEntityType>();

        foreach (var link in dynExpr.Links)
        {
            var innerClrType = link.InnerEntityType.ClrType;
            var innerVar = Expression.Variable(innerClrType, link.Alias);
            allVars.Add(innerVar);
            varByAlias[link.Alias] = innerVar;
            entityTypeByAlias[link.Alias] = link.InnerEntityType;

            exprs.Add(Expression.Assign(
                    innerVar,
                    Expression.Call(
                        MaterialiseLinkedMethod.MakeGenericMethod(innerClrType),
                        entityParam,
                        Expression.Constant(link.InnerEntityType),
                        Expression.Constant(link.Alias)
                    )
                )
            );
        }

        // Assign navigation properties (after all entities are materialised)
        foreach (var link in dynExpr.Links)
        {
            var parentVar = link.ParentAlias == null ? rootVar : varByAlias[link.ParentAlias];
            var parentEntityType = link.ParentAlias == null
                ? rootEntityType
                : entityTypeByAlias[link.ParentAlias];

            var nav = parentEntityType.GetNavigations()
                .FirstOrDefault(n => IsReferenceNavigation(n) && n.GetTargetType() == link.InnerEntityType);

            if (nav?.PropertyInfo != null)
                exprs.Add(Expression.Assign(
                    Expression.Property(parentVar, nav.PropertyInfo),
                    varByAlias[link.Alias]));
        }

        exprs.Add(rootVar);
        return (Expression.Block(allVars, exprs), rootClrType);
    }

    private static bool IsIncludeLink(LinkInfo link)
        => link.ResultSelector == null || IsTransparentIdentifierType(link.ResultSelector.ReturnType);

    private static bool IsTransparentIdentifierType(Type type)
        => type.IsGenericType && type.Name.StartsWith("TransparentIdentifier", StringComparison.Ordinal);

    private static bool IsReferenceNavigation(INavigation nav)
    {
        var propType = nav.PropertyInfo?.PropertyType;
        if (propType == null || propType == typeof(string)) return true;
        return !typeof(System.Collections.IEnumerable).IsAssignableFrom(propType);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _param;
        private readonly Expression _replacement;

        public ParameterReplacer(ParameterExpression param, Expression replacement)
        {
            _param = param;
            _replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => ReferenceEquals(node, _param) ? _replacement : base.VisitParameter(node);
    }

    // ── Cached MethodInfo for Materialise / MaterialiseLinked ─────────────

    private static readonly MethodInfo MaterialiseMethod =
        typeof(DynamicsQueryExecutor)
            .GetMethod(nameof(DynamicsQueryExecutor.Materialise), BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo MaterialiseLinkedMethod =
        typeof(DynamicsQueryExecutor)
            .GetMethod(nameof(DynamicsQueryExecutor.MaterialiseLinked), BindingFlags.Public | BindingFlags.Static)!;
}

/// <summary>
/// Static helpers invoked by compiled query delegates at runtime.
/// Materialises Dataverse <see cref="Entity"/> objects into CLR entity instances.
/// </summary>
public static class DynamicsQueryExecutor
{
    public static IEnumerable<T> Execute<T>(
        QueryContext queryContext,
        DynamicsQueryExpression query,
        IEntityType entityType
    )
        where T : class
    {
        var ctx = (DynamicsQueryContext)queryContext;
        var sdkQuery = query.BuildQueryExpression(queryContext.ParameterValues);
        var rows = ctx.Client.Query(sdkQuery);

        return rows.Select(e => Materialise<T>(e, entityType));
    }

    public static async IAsyncEnumerable<T> ExecuteAsync<T>(
        QueryContext queryContext,
        DynamicsQueryExpression query,
        IEntityType entityType,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
        where T : class
    {
        var ctx = (DynamicsQueryContext)queryContext;
        var sdkQuery = query.BuildQueryExpression(queryContext.ParameterValues);

        var rows = ctx.Client
            .QueryAsync(sdkQuery, cancellationToken)
            .ConfigureAwait(false);

        await foreach (var entities in rows)
        foreach (var entity in entities)
            yield return Materialise<T>(entity, entityType);
    }

    // ── Join execution ────────────────────────────────────────────────────

    /// <summary>
    /// Executes a query that has one or more <see cref="LinkInfo"/> joins and maps each
    /// result row through <paramref name="rowTransformer"/> to produce the final result.
    /// </summary>
    public static IEnumerable<TResult> ExecuteWithTransformer<TResult>(
        QueryContext queryContext,
        DynamicsQueryExpression query,
        Func<Entity, TResult> rowTransformer
    )
    {
        var ctx = (DynamicsQueryContext)queryContext;
        var sdkQuery = query.BuildQueryExpression(queryContext.ParameterValues);
        var rows = ctx.Client.Query(sdkQuery);

        return rows.Select(rowTransformer);
    }

    /// <inheritdoc cref="ExecuteWithTransformer{TResult}"/>
    public static async IAsyncEnumerable<TResult> ExecuteWithTransformerAsync<TResult>(
        QueryContext queryContext,
        DynamicsQueryExpression query,
        Func<Entity, TResult> rowTransformer,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var ctx = (DynamicsQueryContext)queryContext;
        var sdkQuery = query.BuildQueryExpression(queryContext.ParameterValues);

        var rows = ctx.Client
            .QueryAsync(sdkQuery, cancellationToken)
            .ConfigureAwait(false);

        await foreach (var entities in rows)
        foreach (var entity in entities)
            yield return rowTransformer(entity);
    }

    // ── Materialisation ───────────────────────────────────────────────────

    /// <summary>
    /// Materialises the root entity from a Dataverse <see cref="Entity"/> row.
    /// Called from compiled row-transformer expressions.
    /// </summary>
    public static T Materialise<T>(Entity entity, IEntityType entityType)
        where T : class
    {
        var instance = Activator.CreateInstance<T>();

        foreach (var prop in entityType.GetProperties())
        {
            // Primary key comes from entity.Id, not from attributes.
            if (prop.IsPrimaryKey() && prop.ClrType == typeof(Guid))
            {
                prop.PropertyInfo?.SetValue(instance, entity.Id);
                continue;
            }

            var logicalName = prop.GetAttributeLogicalName();

            if (!entity.Contains(logicalName)) continue;

            try
            {
                var clrValue = ConvertValue(entity[logicalName], prop.ClrType);
                prop.PropertyInfo?.SetValue(instance, clrValue);
            }
            catch
            {
                // Skip attributes that cannot be mapped to the CLR property.
            }
        }

        return instance;
    }

    /// <summary>
    /// Materialises a linked (joined) entity from <see cref="AliasedValue"/> attributes
    /// stored under <c>{alias}.{attributeName}</c> keys in a Dataverse result row.
    /// Returns a default instance when none of the aliased attributes are present
    /// (left-outer-join rows where the inner entity has no match).
    /// Called from compiled row-transformer expressions.
    /// </summary>
    public static TInner MaterialiseLinked<TInner>(Entity entity, IEntityType entityType, string alias)
        where TInner : class
    {
        var prefix = alias + ".";
        var instance = Activator.CreateInstance<TInner>();

        foreach (var prop in entityType.GetProperties())
        {
            var logicalName = prop.GetAttributeLogicalName();
            var aliasedKey = prefix + logicalName;

            if (!entity.Contains(aliasedKey)) continue;

            try
            {
                var rawValue = entity[aliasedKey] is AliasedValue av ? av.Value : entity[aliasedKey];
                var clrValue = ConvertValue(rawValue, prop.ClrType);
                prop.PropertyInfo?.SetValue(instance, clrValue);
            }
            catch
            {
                // Skip attributes that cannot be mapped to the CLR property.
            }
        }

        return instance;
    }

    /// <summary>
    /// Converts a raw Dataverse attribute value to the CLR property type,
    /// unwrapping SDK wrapper types (Money, OptionSetValue, EntityReference, …).
    /// </summary>
    internal static object? ConvertValue(object? rawValue, Type targetType)
    {
        if (rawValue is null) return null;

        rawValue = rawValue switch
        {
            Money m => m.Value,
            OptionSetValue os => os.Value,
            EntityReference er => er.Id,
            AliasedValue av => av.Value,
            _ => rawValue
        };

        if (rawValue is null) return null;

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(Guid)) return rawValue is Guid g ? g : Guid.Parse(rawValue.ToString()!);

        if (underlying.IsEnum) return Enum.ToObject(underlying, Convert.ToInt32(rawValue));

        return rawValue is IConvertible ? Convert.ChangeType(rawValue, underlying) : rawValue;
    }

    public static async IAsyncEnumerable<TResult> SelectAsync<T, TResult>(
        IAsyncEnumerable<T> source,
        Func<T, TResult> selector,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return selector(item);
    }

    private static int? ResolveCount(int? constant, string? paramName, QueryContext ctx)
        => constant ?? (paramName != null ? (int?)ctx.ParameterValues[paramName] : null);
}