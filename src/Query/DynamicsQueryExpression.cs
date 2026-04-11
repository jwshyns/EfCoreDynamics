using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EfCore.Dynamics365.Query.Crm;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Xrm.Sdk.Query;

namespace EfCore.Dynamics365.Query;

/// <summary>
/// Describes one <see cref="LinkEntity"/> join that has been added to the query.
/// <para>
/// A root-level link (<see cref="ParentAlias"/> is <c>null</c>) is added directly to
/// <see cref="QueryExpression.LinkEntities"/>. A nested link is added to the
/// <see cref="LinkEntity.LinkEntities"/> of its parent, enabling <c>ThenInclude</c>-style
/// chains where the join is made from an inner entity rather than the root entity.
/// </para>
/// </summary>
public sealed class LinkInfo
{
    public IEntityType InnerEntityType { get; }
    public string InnerEntityLogicalName { get; }
    public string OuterKeyAttribute { get; }
    public string InnerKeyAttribute { get; }

    /// <summary>Alias assigned to the linked entity, e.g. <c>"customer0"</c>.</summary>
    public string Alias { get; }

    /// <summary>
    /// Alias of the parent <see cref="LinkInfo"/> under which this link is nested, or
    /// <c>null</c> if the link is at the root level.
    /// </summary>
    public string? ParentAlias { get; }

    /// <summary>
    /// The result-selector lambda that combines the current accumulated result with
    /// this link's inner entity to produce the next accumulated result.
    /// Set by <see cref="DynamicsQueryExpression.SetLinkResultSelector"/>.
    /// </summary>
    public LambdaExpression? ResultSelector { get; internal set; }

    public LinkInfo(
        IEntityType innerEntityType,
        string innerEntityLogicalName,
        string outerKeyAttribute,
        string innerKeyAttribute,
        string alias,
        string? parentAlias
    )
    {
        InnerEntityType = innerEntityType;
        InnerEntityLogicalName = innerEntityLogicalName;
        OuterKeyAttribute = outerKeyAttribute;
        InnerKeyAttribute = innerKeyAttribute;
        Alias = alias;
        ParentAlias = parentAlias;
    }
}

/// <summary>
/// Intermediate representation of a Dataverse query accumulated during
/// EF Core LINQ translation. Wraps <see cref="QueryExpression"/> from the
/// CRM SDK and exposes mutation helpers called by the translation visitors.
/// </summary>
public sealed class DynamicsQueryExpression : Expression
{
    private readonly QueryExpression _sdkQuery;
    private readonly List<LinkInfo> _links = [];
    private readonly List<(Expression body, ParameterExpression entityParam)> _predicates = [];

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

    /// <summary>
    /// All joins that have been added to this query, in the order they were added.
    /// Each <see cref="LinkInfo"/> describes one <see cref="LinkEntity"/> and, for
    /// LINQ-join queries, the result selector that combines the accumulated result with
    /// the inner entity.
    /// </summary>
    public IReadOnlyList<LinkInfo> Links => _links;

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

    /// <summary>
    /// Stores a raw predicate expression to be translated at execution time,
    /// after EF Core query-parameter values have been resolved.
    /// </summary>
    public void AddPredicate(Expression body, ParameterExpression entityParam) => _predicates.Add((body, entityParam));

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

    /// <summary>Non-null when a Select projection has been applied.</summary>
    public LambdaExpression? Projection { get; private set; }

    public void SetProjection(LambdaExpression selector) => Projection = selector;

    /// <summary>
    /// Adds a join to the query and wires the corresponding <see cref="LinkEntity"/>
    /// into the underlying <see cref="QueryExpression"/>.
    /// </summary>
    /// <param name="innerEntityType">EF Core entity type of the entity being joined to.</param>
    /// <param name="innerEntityLogicalName">Dataverse logical name of that entity.</param>
    /// <param name="outerKeyAttribute">Attribute on the <em>from</em> entity used as the join key.</param>
    /// <param name="innerKeyAttribute">Attribute on the <em>to</em> entity used as the join key.</param>
    /// <param name="joinOperator">Inner or left-outer.</param>
    /// <param name="parentAlias">
    /// Alias of the existing <see cref="LinkInfo"/> under which this link should be nested,
    /// or <c>null</c> to add the link at the root level.
    /// </param>
    /// <returns>The alias assigned to the new link entity (e.g. <c>"customer0"</c>).</returns>
    public string AddLink(
        IEntityType innerEntityType,
        string innerEntityLogicalName,
        string outerKeyAttribute,
        string innerKeyAttribute,
        JoinOperator joinOperator,
        string? parentAlias = null
    )
    {
        var alias = $"{innerEntityLogicalName}{_links.Count}";

        string linkFromEntityName;
        if (parentAlias == null)
        {
            linkFromEntityName = EntityLogicalName;
        }
        else
        {
            var parentInfo = _links.First(l => l.Alias == parentAlias);
            linkFromEntityName = parentInfo.InnerEntityLogicalName;
        }

        var linkEntity = new LinkEntity
        {
            LinkFromEntityName = linkFromEntityName,
            LinkFromAttributeName = outerKeyAttribute,
            LinkToEntityName = innerEntityLogicalName,
            LinkToAttributeName = innerKeyAttribute,
            JoinOperator = joinOperator,
            EntityAlias = alias,
            Columns = new ColumnSet(true),
        };

        if (parentAlias == null)
            _sdkQuery.LinkEntities.Add(linkEntity);
        else
            FindSdkLinkEntity(_sdkQuery.LinkEntities, parentAlias)!.LinkEntities.Add(linkEntity);

        _links.Add(new LinkInfo(innerEntityType, innerEntityLogicalName, outerKeyAttribute, innerKeyAttribute, alias,
            parentAlias));
        return alias;
    }

    /// <summary>
    /// Stores the result selector lambda on the link identified by <paramref name="alias"/>.
    /// </summary>
    public void SetLinkResultSelector(string alias, LambdaExpression selector)
    {
        var link = _links.FirstOrDefault(l => l.Alias == alias)
                   ?? throw new InvalidOperationException($"No link with alias '{alias}' found.");
        link.ResultSelector = selector;
    }

    public void ReverseOrders()
    {
        foreach (var order in _sdkQuery.Orders)
            order.OrderType = order.OrderType == OrderType.Ascending
                ? OrderType.Descending
                : OrderType.Ascending;
    }

    // ── SDK query access ──────────────────────────────────────────────────

    /// <summary>
    /// Builds and returns the <see cref="QueryExpression"/> ready to send to Dataverse.
    /// Predicates stored via <see cref="AddPredicate"/> are translated here, after
    /// EF Core query-parameter values have been resolved from <paramref name="parameterValues"/>.
    /// </summary>
    public QueryExpression BuildQueryExpression(IReadOnlyDictionary<string, object> parameterValues)
    {
        // Rebuild criteria fresh on every execution so the same compiled query
        // instance can be called multiple times with different parameter values.
        _sdkQuery.Criteria = new FilterExpression(LogicalOperator.And);

        foreach (var (body, entityParam) in _predicates)
        {
            var resolvedBody = new ParameterResolvingVisitor(parameterValues).Visit(body);
            var filterVisitor = new CrmFilterExpressionVisitor(EntityType, entityParam);
            var filter = filterVisitor.Translate(resolvedBody);
            _sdkQuery.Criteria.AddFilter(filter);
        }

        return _sdkQuery;
    }

    protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

    // ── Helpers ───────────────────────────────────────────────────────────

    private static LinkEntity? FindSdkLinkEntity(IEnumerable<LinkEntity> links, string alias)
    {
        foreach (var link in links)
        {
            if (link.EntityAlias == alias) return link;
            var found = FindSdkLinkEntity(link.LinkEntities, alias);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// Replaces EF Core's <c>queryContext.GetParameterValue("name")</c> call expressions
    /// with <see cref="ConstantExpression"/> nodes whose values come from
    /// <paramref name="parameterValues"/> at execution time.
    /// </summary>
    private sealed class ParameterResolvingVisitor : ExpressionVisitor
    {
        private readonly IReadOnlyDictionary<string, object> _parameterValues;

        public ParameterResolvingVisitor(IReadOnlyDictionary<string, object> parameterValues)
            => _parameterValues = parameterValues;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Match: queryContext.ParameterValues["paramName"]
            // EF Core generates this as a dictionary indexer call.
            if (node.Method.Name == "get_Item"
                && node.Arguments.Count == 1
                && node.Arguments[0] is ConstantExpression { Value: string paramName }
                && node.Object is MemberExpression
                {
                    Member.Name: "ParameterValues", Expression: ParameterExpression { Type: var ownerType }
                }
                && typeof(QueryContext).IsAssignableFrom(ownerType)
                && _parameterValues.TryGetValue(paramName, out var value))
                return Constant(value, node.Type);

            return base.VisitMethodCall(node);
        }
    }
}