﻿using System;
using System.Linq.Expressions;
using System.Text;

namespace Stellar
{
    internal class QueryFormatter : DbExpressionVisitor
    {
        StringBuilder _sb;
        int _depth;

        internal QueryFormatter() { }

        internal string Format(Expression expression)
        {
            _sb = new StringBuilder();
            Visit(expression);
            return _sb.ToString();
        }

        protected enum Indentation
        {
            Same,
            Inner,
            Outer
        }

        internal int IndentionWidth { get; set; } = 2;

        private void AppendNewLine(Indentation style)
        {
            _sb.AppendLine();
            if (style == Indentation.Inner)
                _depth++;
            else if (style == Indentation.Outer)
            {
                _depth--;
                System.Diagnostics.Debug.Assert(_depth >= 0);
            }
            for (int i = 0, n = _depth * IndentionWidth; i < n; i++)
            {
                _sb.Append(" ");
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            if (m.Method.Name == "Contains" && m.NodeType == ExpressionType.Call && m.Arguments.Count == 1)
            {
                _sb.Append("ARRAY_CONTAINS(t0.");
                var memberAccess = m.Object as MemberExpression;
                var name = char.ToLowerInvariant(memberAccess.Member.Name[0]) + memberAccess.Member.Name.Substring(1);
                _sb.Append(name);
                _sb.Append(", ");
                Visit(m.Arguments[0]);
                _sb.Append(")");
                return m;
            }
            else if (m.Method.Name == "Contains" && m.NodeType == ExpressionType.Call && m.Arguments.Count == 2)
            {
                _sb.Append("ARRAY_CONTAINS(t0.");
                var memberAccess = m.Arguments[0] as MemberExpression;
                var name = char.ToLowerInvariant(memberAccess.Member.Name[0]) + memberAccess.Member.Name.Substring(1);
                _sb.Append(name);
                _sb.Append(", ");
                Visit(m.Arguments[1]);
                _sb.Append(")");
                return m;
            }
            else if (m.Method.Name == "Count" && m.NodeType == ExpressionType.Call && m.Arguments.Count == 1)
            {
                _sb.Append("ARRAY_LENGTH(t0.");
                var memberAccess = m.Arguments[0] as MemberExpression;
                var name = char.ToLowerInvariant(memberAccess.Member.Name[0]) + memberAccess.Member.Name.Substring(1);
                _sb.Append(name);
                _sb.Append(")");
                return m;
            }
            throw new NotSupportedException($"The method '{m.Method.Name}' is not supported");
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    _sb.Append(" NOT ");
                    Visit(u.Operand);
                    break;
                default:
                    throw new NotSupportedException($"The unary operator '{u.NodeType}' is not supported");
            }
            return u;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            _sb.Append("(");
            Visit(b.Left);

            switch (b.NodeType)
            {
                case ExpressionType.And:
                    _sb.Append(" AND ");
                    break;
                case ExpressionType.Or:
                    _sb.Append(" OR");
                    break;
                case ExpressionType.Equal:
                    _sb.Append(" = ");
                    break;
                case ExpressionType.NotEqual: 
                    _sb.Append(" <> ");
                    break;
                case ExpressionType.LessThan:
                    _sb.Append(" < ");
                    break;
                case ExpressionType.LessThanOrEqual:
                    _sb.Append(" <= ");
                    break;
                case ExpressionType.GreaterThan:
                    _sb.Append(" > ");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    _sb.Append(" >= ");
                    break;
                case ExpressionType.AndAlso:
                    _sb.Append(" AND ");
                    break;
                case ExpressionType.OrElse:
                    _sb.Append(" OR ");
                    break;
                default:
                    throw new NotSupportedException($"The binary operator '{b.NodeType}' is not supported");
            }

            Visit(b.Right);
            _sb.Append(")");
            return b;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (c.Value == null)
            {
                _sb.Append("NULL");
            }
            else
            {
                switch (Type.GetTypeCode(c.Value.GetType()))
                {
                    case TypeCode.Boolean:
                        _sb.Append(c.Value.ToString().ToLower());
                        break;
                    case TypeCode.String:
                        if ((string)c.Value == "_type")
                        { // _type will always be on the root object, so alias will be t0
                            _sb.Append("t0." + c.Value);
                            break;
                        }
                        _sb.Append("\"");
                        _sb.Append(c.Value);
                        _sb.Append("\"");
                        break;
                    case TypeCode.DateTime:
                        if (c.Type == typeof(DateTime))
                        {
                            var dt = (DateTime)c.Value;
                            _sb.Append("\"");
                            _sb.Append(dt.ToString("O"));
                            _sb.Append("\"");
                            break;
                        }
                        if (c.Type == typeof(DateTime?))
                        {
                            var dt = (DateTime?)c.Value;
                            if (dt.HasValue)
                            {
                                _sb.Append("\"");
                                _sb.Append(dt.Value.ToString("O"));
                                _sb.Append("\"");
                                break;
                            }
                        }
                        throw new NotSupportedException($"The constant DateTime for '{c.Value}' is not supported");
                    case TypeCode.Object:
                        if (c.Type == typeof(Guid))
                        {
                            _sb.Append("\"");
                            _sb.Append(c.Value);
                            _sb.Append("\"");
                            break;
                        }
                        if (c.Type == typeof(DateTimeOffset))
                        {
                            var dt = (DateTimeOffset)c.Value;
                            _sb.Append("\"");
                            _sb.Append(dt.ToString("O"));
                            _sb.Append("\"");
                            break;
                        }
                        if (c.Type == typeof(DateTimeOffset?))
                        {
                            var dt = (DateTimeOffset?)c.Value;
                            if (dt.HasValue)
                            {
                                _sb.Append("\"");
                                _sb.Append(dt.Value.ToString("O"));
                                _sb.Append("\"");
                            }
                            break;
                        }
                        throw new NotSupportedException($"The constant for '{c.Value}' is not supported, Type: {c.Type}");
                    default:
                        _sb.Append(c.Value);
                        break;
                }
            }
            return c;
        }

        protected override Expression VisitColumn(ColumnExpression column)
        {
            if (!string.IsNullOrEmpty(column.Alias))
            {
                _sb.Append(column.Alias);
                _sb.Append(".");
            }
            _sb.Append(column.Name);
            return column;
        }

        protected override Expression VisitSelect(SelectExpression select)
        {
            _sb.Append("SELECT ");

            if (select.Columns.Count == 0 && select.From != null)
            {
                _sb.Append("* ");
            }
            else
            {
                for (int i = 0, n = select.Columns.Count; i < n; i++)
                {
                    ColumnDeclaration column = select.Columns[i];
                    if (i > 0)
                    {
                        _sb.Append(", ");
                    }

                    ColumnExpression c = Visit(column.Expression) as ColumnExpression;

                    if (c == null || c.Name != select.Columns[i].Name)
                    {
                        _sb.Append(" AS ");
                        _sb.Append(column.Name);
                    }
                }
            }

            if (select.From != null)
            {
                AppendNewLine(Indentation.Same);
                _sb.Append("FROM ");
                VisitSource(select.From);
            }

            if (select.Where != null)
            {
                AppendNewLine(Indentation.Same);
                _sb.Append("WHERE ");
                Visit(select.Where);
            }

            if (select.OrderBy != null && select.OrderBy.Count > 0)
            {
                AppendNewLine(Indentation.Same);
                _sb.Append("ORDER BY ");
                for (int i = 0; i < select.OrderBy.Count; i++)
                {
                    var exp = select.OrderBy[i];
                    if (i > 0)
                        _sb.Append(",");

                    Visit(exp.Expression);
                    if(exp.OrderType != OrderType.Ascending)
                    {
                        _sb.Append(" DESC");
                    }
                }
            }

            return select;
        }

        protected override Expression VisitSource(Expression source)
        {
            switch ((DbExpressionType)source.NodeType)
            {
                case DbExpressionType.Table:
                    TableExpression table = (TableExpression)source;
                    _sb.Append(table.Name);
                    _sb.Append(" AS ");
                    _sb.Append(table.Alias);
                    break;
                case DbExpressionType.Select:
                    SelectExpression select = (SelectExpression)source;
                    _sb.Append("(");
                    AppendNewLine(Indentation.Inner);
                    Visit(select);
                    AppendNewLine(Indentation.Outer);
                    _sb.Append(")");
                    _sb.Append(" AS ");
                    _sb.Append(select.Alias);
                    break;
                default:
                    throw new InvalidOperationException("Select source is not valid type");
            }

            return source;
        }

        protected override Expression VisitMember(MemberExpression m)
        {
            if (m.Expression != null && m.Expression.NodeType == ExpressionType.MemberInit)
            {
                _sb.Append("t0.");
                var name =  char.ToLowerInvariant(m.Member.Name[0]) + m.Member.Name.Substring(1);
                _sb.Append(name); // TODO: fix this to the same serialize setting as we persists to cosmos
                return m;
            }
            else if (m.Expression != null && m.Expression.NodeType == ExpressionType.MemberAccess && m.Expression.Type.IsGenericType)
            {
                return VisitGenericMember(m);

            }
            return m;
        }

        protected Expression VisitGenericMember(MemberExpression m)
        {
            if (m.Expression != null && m.Expression.NodeType == ExpressionType.MemberAccess && m.Expression.Type.IsGenericType)
            {
                var exp = m.Expression as MemberExpression;
                if (exp.Expression.NodeType == ExpressionType.MemberInit)
                {
                    _sb.Append("t0.");
                    if (!(m.Member.DeclaringType.IsGenericType && m.Member.DeclaringType.Namespace == "System" && m.Member.DeclaringType.Name.StartsWith("Nullable")))
                    {
                        var parentProperty = char.ToLowerInvariant(exp.Member.Name[0]) + exp.Member.Name.Substring(1);
                        _sb.Append(parentProperty + ".");
                        var name = char.ToLowerInvariant(m.Member.Name[0]) + m.Member.Name.Substring(1);
                        _sb.Append(name); // TODO: fix this to the same serialize setting as we persists to cosmos
                    }
                    else
                    {
                        var name = char.ToLowerInvariant(exp.Member.Name[0]) + exp.Member.Name.Substring(1);
                        _sb.Append(name); // TODO: fix this to the same serialize setting as we persists to cosmos
                    }
                    
                    return m;
                }
                else if (exp.Expression.NodeType == ExpressionType.MemberAccess && m.Expression.Type.IsGenericType)
                {
                    return VisitGenericMember(exp);
                }
            }
            return m;
        }
    }
}
