using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace EfCore.Dynamics365.Query;

/// <summary>
/// Intermediate representation of a Dataverse query accumulated during
/// EF Core LINQ translation. Wraps <see cref="QueryExpression"/> from the
/// CRM SDK and exposes mutation helpers called by the translation visitors.
/// </summary>
public sealed class DynamicsQueryExpression : Expression
{
    private readonly QueryExpression _sdkQuery;

    public IEntityType EntityType { get; }

    /// <summary>Dataverse entity logical name, e.g. "account".</summary>
    public string EntityLogicalName { get; }

    /// <summary>Whether First/Single semantics apply (forces TopCount = 1).</summary>
    public bool IsSingleRow { get; private set; }

    /// <summary>In-memory skip value (QueryExpression has no native offset).</summary>
    public int? Skip { get; private set; }

    /// <summary>Name of the QueryContext parameter holding the skip value, when Skip is not a compile-time constant.</summary>
    public string? SkipParameterName { get; private set; }

    /// <summary>Name of the QueryContext parameter holding the top/take value, when it is not a compile-time constant.</summary>
    public string? TopParameterName { get; private set; }

    // EF Core expression infrastructure
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type { get; }

    public DynamicsQueryExpression(IEntityType entityType, string entityLogicalName)
    {
        EntityType = entityType;
        EntityLogicalName = entityLogicalName;
        Type = typeof(object);

        _sdkQuery = new QueryExpression(entityLogicalName)
        {
            ColumnSet = new ColumnSet(true), // all columns unless AddSelectField is called
        };
    }

    // ── Mutation helpers (called by translation visitors) ──────────────────

    public void AddFilter(FilterExpression filter)
    {
        _sdkQuery.Criteria.AddFilter(filter);
    }

    public void AddSelectField(string logicalName)
    {
        if (_sdkQuery.ColumnSet.AllColumns)
            _sdkQuery.ColumnSet = new ColumnSet();

        if (!_sdkQuery.ColumnSet.Columns.Contains(logicalName))
            _sdkQuery.ColumnSet.AddColumn(logicalName);
    }

    public void AddOrderBy(string logicalName, bool ascending)
    {
        _sdkQuery.Orders.Add(new OrderExpression(
            logicalName,
            ascending ? OrderType.Ascending : OrderType.Descending));
    }

    public void SetTop(int top)
    {
        _sdkQuery.TopCount = _sdkQuery.TopCount.HasValue
            ? Math.Min(_sdkQuery.TopCount.Value, top)
            : top;
    }

    public void SetSkip(int skip) => Skip = skip;
    public void SetSkipParameterName(string name) => SkipParameterName = name;
    public void SetTopParameterName(string name) => TopParameterName = name;

    public void SetSingleRow()
    {
        IsSingleRow = true;
        SetTop(1);
    }

    // ── SDK query access ──────────────────────────────────────────────────

    /// <summary>Returns the fully-built <see cref="QueryExpression"/> ready to send to Dataverse.</summary>
    public QueryExpression BuildQueryExpression() => _sdkQuery;

    protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
}