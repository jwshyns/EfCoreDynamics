using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCore.Dynamics365.Query
{
    /// <summary>
    /// Intermediate representation of an OData query built up during
    /// EF Core LINQ translation. Equivalent to SQL SelectExpression in relational providers.
    /// </summary>
    public sealed class DynamicsQueryExpression : Expression
    {
        private readonly List<string> _selectFields = new List<string>();
        private readonly List<string> _orderByClauses = new List<string>();
        private readonly List<string> _filterClauses = new List<string>();

        public IEntityType EntityType { get; }

        /// <summary>The OData entity-set name used in the URL, e.g. "accounts".</summary>
        public string EntitySetName { get; }

        /// <summary>Fields for $select (empty = all fields).</summary>
        public IReadOnlyList<string> SelectFields => _selectFields;

        /// <summary>OData $filter string segments (joined with " and ").</summary>
        public IReadOnlyList<string> FilterClauses => _filterClauses;

        /// <summary>OData $orderby segments.</summary>
        public IReadOnlyList<string> OrderByClauses => _orderByClauses;

        /// <summary>OData $top value; null means no limit.</summary>
        public int? Top { get; private set; }

        /// <summary>OData $skip value; null means no skip.</summary>
        public int? Skip { get; private set; }

        /// <summary>Whether First/Single semantics apply (forces Top=1).</summary>
        public bool IsSingleRow { get; private set; }

        // EF Core expression infrastructure
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type { get; }

        public DynamicsQueryExpression(IEntityType entityType, string entitySetName)
        {
            EntityType    = entityType;
            EntitySetName = entitySetName;
            Type          = typeof(object); // overridden by shaper
        }

        // ── Mutation helpers (called by visitor) ──────────────────────────────

        public void AddFilter(string odataFilter)
        {
            if (!string.IsNullOrWhiteSpace(odataFilter))
                _filterClauses.Add("(" + odataFilter + ")");
        }

        public void AddSelectField(string logicalName)
        {
            if (!_selectFields.Contains(logicalName))
                _selectFields.Add(logicalName);
        }

        public void AddOrderBy(string clause) => _orderByClauses.Add(clause);

        public void SetTop(int top)
        {
            Top = Top.HasValue ? Math.Min(Top.Value, top) : top;
        }

        public void SetSkip(int skip) => Skip = skip;

        public void SetSingleRow() { IsSingleRow = true; SetTop(1); }

        // ── OData URL building ─────────────────────────────────────────────────

        /// <summary>
        /// Serialises the query into a URL query string (the part after '?').
        /// </summary>
        public string BuildODataQueryString()
        {
            var parts = new List<string>();

            if (_filterClauses.Count > 0)
                parts.Add("$filter=" + Uri.EscapeDataString(
                    string.Join(" and ", _filterClauses)));

            if (_selectFields.Count > 0)
                parts.Add("$select=" + string.Join(",", _selectFields));

            if (_orderByClauses.Count > 0)
                parts.Add("$orderby=" + Uri.EscapeDataString(
                    string.Join(",", _orderByClauses)));

            if (Top.HasValue)
                parts.Add("$top=" + Top.Value);

            if (Skip.HasValue)
                parts.Add("$skip=" + Skip.Value);

            return string.Join("&", parts);
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
    }
}
