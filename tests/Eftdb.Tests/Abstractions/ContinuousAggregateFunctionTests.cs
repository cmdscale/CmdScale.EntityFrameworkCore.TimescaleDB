using CmdScale.EntityFrameworkCore.TimescaleDB.Abstractions;
using CmdScale.EntityFrameworkCore.TimescaleDB.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Abstractions
{
    public class ContinuousAggregateFunctionTests
    {
        #region ToAnnotationValue_SerializesToAliasFunctionSourceColumn

        [Fact]
        public void ToAnnotationValue_SerializesToAliasFunctionSourceColumn()
        {
            // Arrange
            ContinuousAggregateFunction function = new("avg_t", EAggregateFunction.Avg, "temp");

            // Act
            string annotationValue = function.ToAnnotationValue();

            // Assert
            Assert.Equal("avg_t:Avg:temp", annotationValue);
        }

        #endregion

        #region Constructor_ExposesPropertiesUnchanged

        [Fact]
        public void Constructor_ExposesPropertiesUnchanged()
        {
            // Arrange & Act
            ContinuousAggregateFunction function = new("total", EAggregateFunction.Sum, "value");

            // Assert
            Assert.Equal("total", function.Alias);
            Assert.Equal(EAggregateFunction.Sum, function.Function);
            Assert.Equal("value", function.SourceColumn);
        }

        #endregion

        #region RoundTrip_AnnotationValue_FeedsContinuousAggregateGenerator

        [Fact]
        public void RoundTrip_AnnotationValue_FeedsContinuousAggregateGenerator()
        {
            // Arrange
            ContinuousAggregateFunction function = new("avg_t", EAggregateFunction.Avg, "temp");

            CreateContinuousAggregateOperation operation = new()
            {
                Schema = "public",
                MaterializedViewName = "daily_avg",
                ParentName = "measurements",
                TimeBucketWidth = "1 day",
                TimeBucketSourceColumn = "time",
                TimeBucketGroupBy = true,
                AggregateFunctions = [function.ToAnnotationValue()]
            };

            // Act
            List<string> statements = ContinuousAggregateSqlGenerator.Generate(operation);
            string sql = string.Join("\n", statements);

            // Assert
            Assert.Contains("AVG(\"temp\") AS \"avg_t\"", sql);
        }

        #endregion
    }
}
