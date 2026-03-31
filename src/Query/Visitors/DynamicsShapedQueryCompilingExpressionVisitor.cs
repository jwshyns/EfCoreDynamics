using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
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
public sealed class DynamicsShapedQueryCompilingExpressionVisitor : ShapedQueryCompilingExpressionVisitor
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
        var dynExpr = (DynamicsQueryExpression)shapedQueryExpression.QueryExpression;
        var entityType = dynExpr.EntityType;
        var clrType = entityType.ClrType;

        var queryContextParam = Expression.Parameter(typeof(QueryContext), "queryContext");
        var dynQueryConst = Expression.Constant(dynExpr);
        var entityTypeConst = Expression.Constant(entityType);

        if (_isAsync)
        {
            var method = typeof(DynamicsQueryExecutor)
                .GetMethod(nameof(DynamicsQueryExecutor.ExecuteAsync))!
                .MakeGenericMethod(clrType);

            return Expression.Lambda(
                Expression.Call(
                    null,
                    method,
                    queryContextParam,
                    dynQueryConst,
                    entityTypeConst,
                    Expression.Constant(CancellationToken.None)
                ),
                queryContextParam
            );
        }
        else
        {
            var method = typeof(DynamicsQueryExecutor)
                .GetMethod(nameof(DynamicsQueryExecutor.Execute))!
                .MakeGenericMethod(clrType);

            return Expression.Lambda(
                Expression.Call(null, method, queryContextParam, dynQueryConst, entityTypeConst),
                queryContextParam
            );
        }
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
        IEntityType entityType)
        where T : class
    {
        var ctx = (DynamicsQueryContext)queryContext;
        var sdkQuery = query.BuildQueryExpression();
        IEnumerable<Entity> rows = ctx.Client
            .QueryAsync(query.EntityLogicalName, sdkQuery)
            .GetAwaiter().GetResult();

        if (query.Skip.HasValue)
            rows = rows.Skip(query.Skip.Value);

        return rows.Select(e => Materialise<T>(e, entityType));
    }

    public static async IAsyncEnumerable<T> ExecuteAsync<T>(
        QueryContext queryContext,
        DynamicsQueryExpression query,
        IEntityType entityType,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where T : class
    {
        var ctx = (DynamicsQueryContext)queryContext;
        var sdkQuery = query.BuildQueryExpression();

        IEnumerable<Entity> rows = await ctx.Client
            .QueryAsync(query.EntityLogicalName, sdkQuery, cancellationToken)
            .ConfigureAwait(false);

        if (query.Skip.HasValue)
            rows = rows.Skip(query.Skip.Value);

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

    public ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        => new DynamicsShapedQueryCompilingExpressionVisitor(_dependencies, queryCompilationContext);
}