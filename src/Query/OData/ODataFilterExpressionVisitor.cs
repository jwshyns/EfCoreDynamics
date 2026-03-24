using System;
using System.Linq;
using System.Linq.Expressions;
using EfCore.Dynamics365.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCore.Dynamics365.Query.OData
{
    /// <summary>
    /// Visits a LINQ predicate expression and translates it to an OData $filter string.
    /// Supports: ==, !=, &lt;, &lt;=, &gt;, &gt;=, AndAlso, OrElse, Not, Contains (string),
    /// StartsWith, EndsWith, null checks.
    /// </summary>
    internal sealed class ODataFilterExpressionVisitor : ExpressionVisitor
    {
        private readonly IEntityType _entityType;
        private readonly ParameterExpression _parameter;
        private System.Text.StringBuilder _sb = new System.Text.StringBuilder();

        public ODataFilterExpressionVisitor(IEntityType entityType, ParameterExpression parameter)
        {
            _entityType = entityType;
            _parameter = parameter;
        }

        public string Translate(Expression expression)
        {
            _sb = new System.Text.StringBuilder();
            Visit(expression);
            return _sb.ToString();
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            _sb.Append('(');

            switch (node.NodeType)
            {
                case ExpressionType.Equal:
                    // Handle null comparisons
                    if (IsNullConstant(node.Right))
                    {
                        Visit(node.Left);
                        _sb.Append(" eq null");
                        break;
                    }

                    if (IsNullConstant(node.Left))
                    {
                        Visit(node.Right);
                        _sb.Append(" eq null");
                        break;
                    }

                    Visit(node.Left);
                    _sb.Append(" eq ");
                    Visit(node.Right);
                    break;

                case ExpressionType.NotEqual:
                    if (IsNullConstant(node.Right))
                    {
                        Visit(node.Left);
                        _sb.Append(" ne null");
                        break;
                    }

                    Visit(node.Left);
                    _sb.Append(" ne ");
                    Visit(node.Right);
                    break;

                case ExpressionType.GreaterThan:
                    Visit(node.Left);
                    _sb.Append(" gt ");
                    Visit(node.Right);
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    Visit(node.Left);
                    _sb.Append(" ge ");
                    Visit(node.Right);
                    break;
                case ExpressionType.LessThan:
                    Visit(node.Left);
                    _sb.Append(" lt ");
                    Visit(node.Right);
                    break;
                case ExpressionType.LessThanOrEqual:
                    Visit(node.Left);
                    _sb.Append(" le ");
                    Visit(node.Right);
                    break;

                case ExpressionType.AndAlso:
                    Visit(node.Left);
                    _sb.Append(" and ");
                    Visit(node.Right);
                    break;
                case ExpressionType.OrElse:
                    Visit(node.Left);
                    _sb.Append(" or ");
                    Visit(node.Right);
                    break;

                case ExpressionType.Add:
                case ExpressionType.AddAssign:
                case ExpressionType.AddAssignChecked:
                case ExpressionType.AddChecked:
                case ExpressionType.And:
                case ExpressionType.AndAssign:
                case ExpressionType.ArrayIndex:
                case ExpressionType.ArrayLength:
                case ExpressionType.Assign:
                case ExpressionType.Block:
                case ExpressionType.Call:
                case ExpressionType.Coalesce:
                case ExpressionType.Conditional:
                case ExpressionType.Constant:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.DebugInfo:
                case ExpressionType.Decrement:
                case ExpressionType.Default:
                case ExpressionType.Divide:
                case ExpressionType.DivideAssign:
                case ExpressionType.Dynamic:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.ExclusiveOrAssign:
                case ExpressionType.Extension:
                case ExpressionType.Goto:
                case ExpressionType.Increment:
                case ExpressionType.Index:
                case ExpressionType.Invoke:
                case ExpressionType.IsFalse:
                case ExpressionType.IsTrue:
                case ExpressionType.Label:
                case ExpressionType.Lambda:
                case ExpressionType.LeftShift:
                case ExpressionType.LeftShiftAssign:
                case ExpressionType.ListInit:
                case ExpressionType.Loop:
                case ExpressionType.MemberAccess:
                case ExpressionType.MemberInit:
                case ExpressionType.Modulo:
                case ExpressionType.ModuloAssign:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyAssign:
                case ExpressionType.MultiplyAssignChecked:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.New:
                case ExpressionType.NewArrayBounds:
                case ExpressionType.NewArrayInit:
                case ExpressionType.Not:
                case ExpressionType.OnesComplement:
                case ExpressionType.Or:
                case ExpressionType.OrAssign:
                case ExpressionType.Parameter:
                case ExpressionType.PostDecrementAssign:
                case ExpressionType.PostIncrementAssign:
                case ExpressionType.Power:
                case ExpressionType.PowerAssign:
                case ExpressionType.PreDecrementAssign:
                case ExpressionType.PreIncrementAssign:
                case ExpressionType.Quote:
                case ExpressionType.RightShift:
                case ExpressionType.RightShiftAssign:
                case ExpressionType.RuntimeVariables:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractAssign:
                case ExpressionType.SubtractAssignChecked:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Switch:
                case ExpressionType.Throw:
                case ExpressionType.Try:
                case ExpressionType.TypeAs:
                case ExpressionType.TypeEqual:
                case ExpressionType.TypeIs:
                case ExpressionType.UnaryPlus:
                case ExpressionType.Unbox:
                default:
                    throw new NotSupportedException(
                        $"Binary operator {node.NodeType} is not supported in Dynamics 365 OData filters.");
            }

            _sb.Append(')');
            return node;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Not:
                    _sb.Append("not (");
                    Visit(node.Operand);
                    _sb.Append(')');
                    return node;
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    Visit(node.Operand);
                    return node;
                case ExpressionType.Add:
                case ExpressionType.AddAssign:
                case ExpressionType.AddAssignChecked:
                case ExpressionType.AddChecked:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.AndAssign:
                case ExpressionType.ArrayIndex:
                case ExpressionType.ArrayLength:
                case ExpressionType.Assign:
                case ExpressionType.Block:
                case ExpressionType.Call:
                case ExpressionType.Coalesce:
                case ExpressionType.Conditional:
                case ExpressionType.Constant:
                case ExpressionType.DebugInfo:
                case ExpressionType.Decrement:
                case ExpressionType.Default:
                case ExpressionType.Divide:
                case ExpressionType.DivideAssign:
                case ExpressionType.Dynamic:
                case ExpressionType.Equal:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.ExclusiveOrAssign:
                case ExpressionType.Extension:
                case ExpressionType.Goto:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Increment:
                case ExpressionType.Index:
                case ExpressionType.Invoke:
                case ExpressionType.IsFalse:
                case ExpressionType.IsTrue:
                case ExpressionType.Label:
                case ExpressionType.Lambda:
                case ExpressionType.LeftShift:
                case ExpressionType.LeftShiftAssign:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.ListInit:
                case ExpressionType.Loop:
                case ExpressionType.MemberAccess:
                case ExpressionType.MemberInit:
                case ExpressionType.Modulo:
                case ExpressionType.ModuloAssign:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyAssign:
                case ExpressionType.MultiplyAssignChecked:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.New:
                case ExpressionType.NewArrayBounds:
                case ExpressionType.NewArrayInit:
                case ExpressionType.NotEqual:
                case ExpressionType.OnesComplement:
                case ExpressionType.Or:
                case ExpressionType.OrAssign:
                case ExpressionType.OrElse:
                case ExpressionType.Parameter:
                case ExpressionType.PostDecrementAssign:
                case ExpressionType.PostIncrementAssign:
                case ExpressionType.Power:
                case ExpressionType.PowerAssign:
                case ExpressionType.PreDecrementAssign:
                case ExpressionType.PreIncrementAssign:
                case ExpressionType.Quote:
                case ExpressionType.RightShift:
                case ExpressionType.RightShiftAssign:
                case ExpressionType.RuntimeVariables:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractAssign:
                case ExpressionType.SubtractAssignChecked:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Switch:
                case ExpressionType.Throw:
                case ExpressionType.Try:
                case ExpressionType.TypeAs:
                case ExpressionType.TypeEqual:
                case ExpressionType.TypeIs:
                case ExpressionType.UnaryPlus:
                case ExpressionType.Unbox:
                default:
                    throw new NotSupportedException(
                        $"Unary operator {node.NodeType} is not supported.");
            }
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == _parameter
                || (node.Expression is UnaryExpression u && u.Operand == _parameter))
            {
                var logicalName = GetLogicalName(node.Member.Name);
                _sb.Append(logicalName);
                return node;
            }

            // Evaluate captured variables
            var value = EvaluateExpression(node);
            AppendValue(value);
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            AppendValue(node.Value);
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // string.Contains, StartsWith, EndsWith
            if (node.Object != null && node.Object.Type == typeof(string))
            {
                var memberName = GetMemberName(node.Object);
                var logicalName = memberName != null ? GetLogicalName(memberName) : null;
                var arg = node.Arguments.Count > 0 ? EvaluateExpression(node.Arguments[0]) : null;

                if (logicalName != null && arg is string strArg)
                {
                    var escaped = EscapeODataString(strArg);

                    switch (node.Method.Name)
                    {
                        case "Contains":
                            _sb.Append($"contains({logicalName},'{escaped}')");
                            return node;
                        case "StartsWith":
                            _sb.Append($"startswith({logicalName},'{escaped}')");
                            return node;
                        case "EndsWith":
                            _sb.Append($"endswith({logicalName},'{escaped}')");
                            return node;
                    }
                }
            }

            // Enumerable.Contains(collection, member) - translates to "field in (v1,v2,...)"
            if (node.Method.DeclaringType == typeof(Enumerable) && node.Method.Name == "Contains"
                                                                && node.Arguments.Count == 2)
            {
                var collection = EvaluateExpression(node.Arguments[0]);
                var memberExpr = node.Arguments[1];
                var memberName = GetMemberName(memberExpr);
                if (memberName != null && collection is System.Collections.IEnumerable enumerable)
                {
                    var logicalName = GetLogicalName(memberName);
                    var values = (from object? item in enumerable select FormatValue(item)).ToList();
                    _sb.Append(logicalName + " in (" + string.Join(",", values) + ")");
                    return node;
                }
            }

            // Fallback: evaluate the whole expression as a constant
            var constVal = EvaluateExpression(node);
            AppendValue(constVal);
            return node;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string GetLogicalName(string clrPropertyName)
        {
            var prop = _entityType.FindProperty(clrPropertyName);
            if (prop != null)
                return prop.GetAttributeLogicalName() ?? clrPropertyName.ToLowerInvariant();

            return clrPropertyName.ToLowerInvariant();
        }

        private static string? GetMemberName(Expression expr)
        {
            return expr switch
            {
                MemberExpression m => m.Member.Name,
                UnaryExpression { Operand: MemberExpression m2 } => m2.Member.Name,
                _ => null
            };
        }

        private void AppendValue(object? value) => _sb.Append(FormatValue(value));

        private static string FormatValue(object? value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string s:
                    return "'" + EscapeODataString(s) + "'";
                case bool b:
                    return b ? "true" : "false";
                case Guid g:
                    return g.ToString();
                case DateTime dt:
                    return dt.ToString("o");
                case DateTimeOffset dto:
                    return dto.ToString("o");
                case int _:
                case long _:
                case decimal _:
                case float _:
                case double _:
                    return Convert.ToString(value,
                        System.Globalization.CultureInfo.InvariantCulture) ?? "null";
                case Enum e:
                    return Convert.ToInt32(e).ToString();
                default:
                    return "'" + EscapeODataString(value.ToString() ?? string.Empty) + "'";
            }
        }

        private static string EscapeODataString(string s) => s.Replace("'", "''");

        private static bool IsNullConstant(Expression expr) => expr is ConstantExpression { Value: null };

        private static object? EvaluateExpression(Expression expr)
        {
            if (expr is ConstantExpression c) return c.Value;
            try
            {
                return Expression.Lambda(expr).Compile().DynamicInvoke();
            }
            catch
            {
                return null;
            }
        }
    }
}