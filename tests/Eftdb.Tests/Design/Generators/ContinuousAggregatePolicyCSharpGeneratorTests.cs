using CmdScale.EntityFrameworkCore.TimescaleDB.Design.Generators;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Design.Generators
{
    /// <summary>
    /// Tests the actual C# text emitted by <see cref="ContinuousAggregatePolicyCSharpGenerator"/>
    /// using a real <see cref="ICSharpHelper"/>.
    /// </summary>
    public class ContinuousAggregatePolicyCSharpGeneratorTests
    {
        private readonly ICSharpHelper code = DesignTimeHelper.CreateRealCSharpHelper();

        private string Generate(AddContinuousAggregatePolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new ContinuousAggregatePolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        private string Generate(RemoveContinuousAggregatePolicyOperation operation)
        {
            IndentedStringBuilder builder = new();
            new ContinuousAggregatePolicyCSharpGenerator(code).Generate(operation, builder);
            return builder.ToString();
        }

        #region AddPolicy_DefaultValues_OmitInvertedAndDefaultArgs

        [Fact]
        public void AddPolicy_DefaultValues_OmitInvertedAndDefaultArgs()
        {
            // Arrange
            AddContinuousAggregatePolicyOperation op = new()
            {
                MaterializedViewName = "hourly",
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("materializedViewName: \"hourly\"", result);
            Assert.DoesNotContain("bucketsPerBatch:", result);
            Assert.DoesNotContain("maxBatchesPerExecution:", result);
            Assert.DoesNotContain("refreshNewestFirst:", result);
            Assert.DoesNotContain("ifNotExists:", result);
        }

        #endregion

        #region AddPolicy_NonDefaultValues_AreEmitted

        [Fact]
        public void AddPolicy_NonDefaultValues_AreEmitted()
        {
            // Arrange
            AddContinuousAggregatePolicyOperation op = new()
            {
                MaterializedViewName = "hourly",
                BucketsPerBatch = 5,
                MaxBatchesPerExecution = 10,
                RefreshNewestFirst = false,
                IfNotExists = true,
                IncludeTieredData = true,
            };

            // Act
            string result = Generate(op);

            // Assert
            Assert.Contains("bucketsPerBatch: 5", result);
            Assert.Contains("maxBatchesPerExecution: 10", result);
            Assert.Contains("refreshNewestFirst: false", result);
            Assert.Contains("ifNotExists: true", result);
            Assert.Contains("includeTieredData: true", result);
        }

        #endregion

        #region RemovePolicy_IfExists_OnlyEmittedWhenTrue

        [Fact]
        public void RemovePolicy_IfExists_OnlyEmittedWhenTrue()
        {
            // Arrange
            RemoveContinuousAggregatePolicyOperation withFlag = new()
            {
                MaterializedViewName = "hourly",
                IfExists = true,
            };
            RemoveContinuousAggregatePolicyOperation withoutFlag = new()
            {
                MaterializedViewName = "hourly",
                IfExists = false,
            };

            // Act
            string withResult = Generate(withFlag);
            string withoutResult = Generate(withoutFlag);

            // Assert
            Assert.Contains("ifExists: true", withResult);
            Assert.DoesNotContain("ifExists:", withoutResult);
        }

        #endregion
    }
}
