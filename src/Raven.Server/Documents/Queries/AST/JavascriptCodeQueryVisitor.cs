using System;
using System.Collections.Generic;
using System.Text;
using Raven.Server.Documents.Queries.Parser;
using Sparrow;

namespace Raven.Server.Documents.Queries.AST
{
    public class JavascriptCodeQueryVisitor : QueryVisitor
    {
        private readonly StringBuilder _sb;
        private readonly string _alias;

        public JavascriptCodeQueryVisitor(StringBuilder sb, string alias)
        {
            _sb = sb;
            _alias = alias;
        }

        public override void VisitInclude(List<QueryExpression> includes)
        {
           throw new NotSupportedException();
        }

        public override void VisitUpdate(StringSegment update)
        {
            throw new NotSupportedException();
        }

        public override void VisitSelectFunctionBody(StringSegment func)
        {
            throw new NotSupportedException();
        }

        public override void VisitSelect(List<(QueryExpression Expression, StringSegment? Alias)> select, bool isDistinct)
        {
            throw new NotSupportedException();
        }

        public override void VisitLoad(List<(QueryExpression Expression, StringSegment? Alias)> load)
        {
            throw new NotSupportedException();
        }

        public override void VisitOrderBy(List<(QueryExpression Expression, OrderByFieldType FieldType, bool Ascending)> orderBy)
        {
            throw new NotSupportedException();
        }

        public override void VisitDeclaredFunctions(Dictionary<StringSegment, StringSegment> declaredFunctions)
        {
            throw new NotSupportedException();
        }

        public override void VisitCompoundWhereExpression(BinaryExpression @where)
        {
            _sb.Append("(");

            VisitExpression(where.Left);

            switch (where.Operator)
            {
                case OperatorType.And:
                case OperatorType.AndNot:
                    _sb.Append(" && ");
                    break;
                case OperatorType.OrNot:
                case OperatorType.Or:
                    _sb.Append(" || ");
                    break;
            }

            var not = where.Operator == OperatorType.OrNot || where.Operator == OperatorType.AndNot;

            if (not)
                _sb.Append("!(");
            
            VisitExpression(where.Right);

            if (not)
                _sb.Append(")");
            
            _sb.Append(")");
        }

        public override void VisitMethod(MethodExpression expr)
        {
            _sb.Append(expr.Name.Value);
            _sb.Append("(");
            for (var index = 0; index < expr.Arguments.Count; index++)
            {
                if (index != 0)
                    _sb.Append(", ");
                VisitExpression(expr.Arguments[index]);
            }
            _sb.Append(")");
        }

        public override void VisitValue(ValueExpression expr)
        {
            if (expr.Value == ValueTokenType.String)
                _sb.Append('"');
            
            _sb.Append(expr.Token.Value.Replace("\"", "\\\""));

            if (expr.Value == ValueTokenType.String)
                _sb.Append('"');
        }

        public override void VisitIn(InExpression expr)
        {
            _sb.Append(" in(");
            VisitExpression(expr.Source);

            for (var index = 0; index < expr.Values.Count; index++)
            {
                _sb.Append(", ");
                VisitExpression(expr.Values[index]);
            }

            _sb.Append(")");
        }

        public override void VisitBetween(BetweenExpression expr)
        {
            _sb.Append(" between( ");
            VisitExpression(expr.Source);
            _sb.Append(", ");
            VisitExpression(expr.Min);
            _sb.Append(", ");
            VisitExpression(expr.Max);
            _sb.Append(")");
        }

        public override void VisitField(FieldExpression field)
        {
            if (_alias != null)
                _sb.Append("this.");
            _sb.Append(field.Field);
        }

        public override void VisitTrue()
        {
            _sb.Append("true");
        }

        public override void VisitSimpleWhereExpression(BinaryExpression expr)
        {
            VisitExpression(expr.Left);

            switch (expr.Operator)
            {
                case OperatorType.Equal:
                    _sb.Append(" === ");
                    break;
                case OperatorType.NotEqual:
                    _sb.Append(" !== ");
                    break;
                case OperatorType.LessThan:
                    _sb.Append(" < ");
                    break;
                case OperatorType.GreaterThan:
                    _sb.Append(" > ");
                    break;
                case OperatorType.LessThanEqual:
                    _sb.Append(" <= ");
                    break;
                case OperatorType.GreaterThanEqual:
                    _sb.Append(" >= ");
                    break;
            }

            VisitExpression(expr.Right);
        }

        public override void VisitGroupByExpression(List<FieldExpression> expressions)
        {
            throw new NotSupportedException();
        }

        public override void VisitFromClause(FieldExpression @from, StringSegment? alias, QueryExpression filter, bool index)
        {
            throw new NotSupportedException();
        }

        public override void VisitDeclaredFunction(StringSegment name, StringSegment func)
        {
            throw new NotSupportedException();
        }
    }
}
