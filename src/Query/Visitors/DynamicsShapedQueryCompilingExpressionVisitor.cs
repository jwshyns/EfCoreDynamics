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
                result = Expression.Call(null, selectAsyncMethod, result, compiled, Expression.Constant(CancellationToken.None));
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

    private static Expression CompileProjection(LambdaExpression projection, Type entityType, Type projType)
    {
        var funcType = typeof(Func<,>).MakeGenericType(entityType, projType);
        var typedLambda = Expression.Lambda(funcType, projection.Body, projection.Parameters);
        return Expression.Constant(typedLambda.Compile(), funcType);
    }
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
        var sdkQuery = query.BuildQueryExpression();
        IEnumerable<Entity> rows = ctx.Client
            .QueryAsync(query.EntityLogicalName, sdkQuery)
            .GetAwaiter().GetResult();

        if (ResolveCount(query.Skip, query.SkipParameterName, queryContext) is { } skip)
            rows = rows.Skip(skip);
        if (ResolveCount(null, query.TopParameterName, queryContext) is { } top)
            rows = rows.Take(top);

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
        var sdkQuery = query.BuildQueryExpression();

        IEnumerable<Entity> rows = await ctx.Client
            .QueryAsync(query.EntityLogicalName, sdkQuery, cancellationToken)
            .ConfigureAwait(false);

        if (ResolveCount(query.Skip, query.SkipParameterName, queryContext) is { } skip)
            rows = rows.Skip(skip);
        if (ResolveCount(null, query.TopParameterName, queryContext) is { } top)
            rows = rows.Take(top);

        foreach (var e in rows)
            yield return Materialise<T>(e, entityType);
    }

    // ── Materialisation ───────────────────────────────────────────────────

    private static T Materialise<T>(Entity entity, IEntityType entityType)
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

            var logicalName = prop.GetAttributeLogicalName() ?? prop.Name.ToLowerInvariant();

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
    /// Converts a raw Dataverse attribute value to the CLR property type,
    /// unwrapping SDK wrapper types (Money, OptionSetValue, EntityReference, …).
    /// </summary>
    internal static object? ConvertValue(object? rawValue, Type targetType)
    {
        if (rawValue is null) return null;

        // Unwrap Dataverse SDK wrappers
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
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return selector(item);
    }

    private static int? ResolveCount(int? constant, string? paramName, QueryContext ctx)
        => constant ?? (paramName != null ? (int?)ctx.ParameterValues[paramName] : null);
}