using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Internals
{

    // TODO: This is not in use, yet and will need more work to be functional. Therefore, this class will be ignored by code coverage tools. Don't forget to remove the exclusion when you start using it.
    /// <summary>
    /// A simplified visitor to translate a WHERE clause LambdaExpression into a SQL string.
    /// This must be used by your IMigrationsModelDiffer, not the SQL generator.
    /// </summary>
    public class WhereClauseExpressionVisitor(IEntityType sourceEntityType) : ExpressionVisitor
    {
        private readonly StringBuilder sqlBuilder = new();

        /// <summary>
        /// Translates the provided expression into a SQL WHERE clause.
        /// </summary>
        public string Translate(Expression expression)
        {
            sqlBuilder.Clear();
            Visit(expression);
            return sqlBuilder.ToString();
        }

        // Handles expressions like x.Property == "value"
        protected override Expression VisitBinary(BinaryExpression node)
        {
            sqlBuilder.Append('(');

            // Visit the left side (e.g., x.Property)
            Visit(node.Left);

            // Add the SQL operator
            sqlBuilder.Append(GetSqlOperator(node.NodeType));

            // Visit the right side (e.g., "value")
            Visit(node.Right);

            sqlBuilder.Append(')');
            return node;
        }

        // Handles member access, like x.Ticker
        protected override Expression VisitMember(MemberExpression node)
        {
            // We only care about properties of the entity
            if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
            {
                if (node.Member is PropertyInfo propertyInfo)
                {
                    // Find the property on the EF Core model
                    IProperty? property = sourceEntityType.FindProperty(propertyInfo.Name);
                    if (property != null)
                    {
                        StoreObjectIdentifier storeIdentifier = StoreObjectIdentifier.Table(sourceEntityType.GetTableName()!, sourceEntityType.GetSchema());
                        // Get the *database column name*
                        string? columnName = property.GetColumnName(storeIdentifier);
                        // Append the quoted column name
                        sqlBuilder.Append($"\"{columnName}\"");
                        return node;
                    }
                }
            }

            // Fallback for other member access (e.g., accessing a variable)
            // This will try to evaluate it and treat it as a constant.
            if (TryEvaluate(node, out object? value))
            {
                AppendSqlLiteral(value);
                return node;
            }

            throw new NotSupportedException($"Member access '{node.Member.Name}' is not supported.");
        }

        // Handles constants, like "MCRS" or 100
        protected override Expression VisitConstant(ConstantExpression node)
        {
            AppendSqlLiteral(node.Value);
            return node;
        }

        // Tries to "compile" part of the expression to get its value
        private static bool TryEvaluate(Expression expression, out object? value)
        {
            try
            {
                value = Expression.Lambda(expression).Compile().DynamicInvoke();
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        // Helper to format a .NET value as a SQL literal
        private void AppendSqlLiteral(object? value)
        {
            if (value == null)
            {
                sqlBuilder.Append("NULL");
            }
            else if (value is string str)
            {
                // Simple string quoting; for production, you might need more robust escaping
                sqlBuilder.Append($"'{str.Replace("'", "''")}'");
            }
            else if (value is bool b)
            {
                sqlBuilder.Append(b ? "TRUE" : "FALSE");
            }
            else if (value is int || value is long || value is double || value is float || value is decimal)
            {
                sqlBuilder.Append(value.ToString());
            }
            else
            {
                throw new NotSupportedException($"Value of type '{value.GetType().Name}' is not supported.");
            }
        }

        // Helper to map ExpressionType to a SQL operator
        private static string GetSqlOperator(ExpressionType type)
        {
            switch (type)
            {
                case ExpressionType.Equal:
                    return " = ";
                case ExpressionType.NotEqual:
                    return " <> ";
                case ExpressionType.GreaterThan:
                    return " > ";
                case ExpressionType.GreaterThanOrEqual:
                    return " >= ";
                case ExpressionType.LessThan:
                    return " < ";
                case ExpressionType.LessThanOrEqual:
                    return " <= ";
                case ExpressionType.AndAlso:
                    return " AND ";
                case ExpressionType.OrElse:
                    return " OR ";
                default:
                    throw new NotSupportedException($"Operator '{type}' is not supported.");
            }
        }
    }
}