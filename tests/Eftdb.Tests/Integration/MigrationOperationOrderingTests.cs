using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.Hypertable;
using CmdScale.EntityFrameworkCore.TimescaleDB.Configuration.ReorderPolicy;
using CmdScale.EntityFrameworkCore.TimescaleDB.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Tests that verify migration operation ordering in TimescaleMigrationsModelDiffer.
/// These tests ensure that operations are generated in the correct dependency order,
/// which is critical for successful migration execution.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Issue #15 Background:</strong> The original bug was that TimescaleMigrationsModelDiffer used List.Sort()
/// which is an unstable sort and destroyed the correct dependency order from base.GetDifferences().
/// The fix was to use OrderBy() which is a stable sort that preserves the relative order
/// of elements with equal priority values.
/// </para>
/// <para>
/// <strong>Why Unstable Sorts Don't Reliably Fail Tests:</strong>
/// List.Sort() is an unstable sort, meaning it does not guarantee preservation of relative order
/// for elements with equal sort keys. However, it doesn't randomly shuffle elements - it uses an
/// efficient algorithm (IntroSort) that may or may not maintain relative order depending on:
/// 1. The specific input data
/// 2. The number of elements
/// 3. The distribution of priorities
/// </para>
/// <para>
/// In practice, for small lists (like in these tests), List.Sort() often appears stable even though
/// it's not guaranteed to be. The bug was intermittent in production because:
/// - EF Core orders CreateTable before CreateIndex (stable order from base differ)
/// - All standard EF Core operations have priority 0
/// - List.Sort() might preserve their order... or might not
/// - The issue manifested unpredictably, especially with larger/more complex models
/// </para>
/// <para>
/// <strong>These tests verify correct behavior but cannot reliably detect the unstable sort bug.</strong>
/// They serve as regression tests to ensure OrderBy() is used and the correct ordering is maintained.
/// Manual code review is needed to verify the stable sort is in place.
/// </para>
/// </remarks>
public class MigrationOperationOrderingTests : MigrationTestBase
{
    #region Should_Order_CreateTable_Before_CreateIndex

    private class OrderingEntity1
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Name { get; set; }
    }

    private class OrderingContext1 : DbContext
    {
        public DbSet<OrderingEntity1> Entities => Set<OrderingEntity1>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderingEntity1>(entity =>
            {
                entity.ToTable("OrderingTable1");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("idx_ordering1_timestamp");
                entity.HasIndex(x => x.Name).HasDatabaseName("idx_ordering1_name");
            });
        }
    }

    /// <summary>
    /// Verifies that CreateTableOperation appears before CreateIndexOperation.
    /// This is essential because indexes cannot be created on tables that don't exist yet.
    /// </summary>
    [Fact]
    public void Should_Order_CreateTable_Before_CreateIndex()
    {
        // Arrange
        using OrderingContext1 context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        int createTableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingTable1");
        int firstIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering1_timestamp");
        int secondIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering1_name");

        Assert.NotEqual(-1, createTableIndex);
        Assert.NotEqual(-1, firstIndexIndex);
        Assert.NotEqual(-1, secondIndexIndex);
        Assert.True(createTableIndex < firstIndexIndex,
            $"CreateTable (index {createTableIndex}) should appear before first CreateIndex (index {firstIndexIndex})");
        Assert.True(createTableIndex < secondIndexIndex,
            $"CreateTable (index {createTableIndex}) should appear before second CreateIndex (index {secondIndexIndex})");
    }

    #endregion

    #region Should_Order_CreateTable_Before_Foreign_Key_Index

    private class OrderingParent2
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private class OrderingChild2
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public OrderingParent2? Parent { get; set; }
    }

    private class OrderingContext2 : DbContext
    {
        public DbSet<OrderingParent2> Parents => Set<OrderingParent2>();
        public DbSet<OrderingChild2> Children => Set<OrderingChild2>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderingParent2>(entity =>
            {
                entity.ToTable("OrderingParents2");
                entity.HasKey(x => x.Id);
            });

            modelBuilder.Entity<OrderingChild2>(entity =>
            {
                entity.ToTable("OrderingChildren2");
                entity.HasKey(x => x.Id);
                entity.HasOne(x => x.Parent)
                      .WithMany()
                      .HasForeignKey(x => x.ParentId);
            });
        }
    }

    /// <summary>
    /// Verifies that CreateTableOperation appears before CreateIndexOperation for foreign key index.
    /// EF Core includes foreign keys in the CreateTableOperation, but generates a separate
    /// CreateIndexOperation for the foreign key index.
    /// </summary>
    [Fact]
    public void Should_Order_CreateTable_Before_Foreign_Key_Index()
    {
        // Arrange
        using OrderingContext2 context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        int parentTableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingParents2");
        int childTableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingChildren2");
        int foreignKeyIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex &&
            createIndex.Table == "OrderingChildren2" &&
            createIndex.Columns.Contains("ParentId"));

        Assert.NotEqual(-1, parentTableIndex);
        Assert.NotEqual(-1, childTableIndex);
        Assert.NotEqual(-1, foreignKeyIndexIndex);
        Assert.True(parentTableIndex < foreignKeyIndexIndex,
            $"CreateTable for parent (index {parentTableIndex}) should appear before foreign key index (index {foreignKeyIndexIndex})");
        Assert.True(childTableIndex < foreignKeyIndexIndex,
            $"CreateTable for child (index {childTableIndex}) should appear before foreign key index (index {foreignKeyIndexIndex})");
    }

    #endregion

    #region Should_Order_CreateTable_Before_CreateHypertable

    private class OrderingMetric3
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderingContext3 : DbContext
    {
        public DbSet<OrderingMetric3> Metrics => Set<OrderingMetric3>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderingMetric3>(entity =>
            {
                entity.ToTable("OrderingMetrics3");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 day");
            });
        }
    }

    /// <summary>
    /// Verifies that CreateTableOperation appears before CreateHypertableOperation.
    /// TimescaleDB requires the table to exist before it can be converted to a hypertable.
    /// </summary>
    [Fact]
    public void Should_Order_CreateTable_Before_CreateHypertable()
    {
        // Arrange
        using OrderingContext3 context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        int createTableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingMetrics3");
        int createHypertableIndex = operations.ToList().FindIndex(op =>
            op is CreateHypertableOperation hypertable && hypertable.TableName == "OrderingMetrics3");

        Assert.NotEqual(-1, createTableIndex);
        Assert.NotEqual(-1, createHypertableIndex);
        Assert.True(createTableIndex < createHypertableIndex,
            $"CreateTable (index {createTableIndex}) should appear before CreateHypertable (index {createHypertableIndex})");
    }

    #endregion

    #region Should_Order_CreateIndex_Before_AddReorderPolicy

    private class OrderingMetric4
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    private class OrderingContext4 : DbContext
    {
        public DbSet<OrderingMetric4> Metrics => Set<OrderingMetric4>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderingMetric4>(entity =>
            {
                entity.ToTable("OrderingMetrics4");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp);
                entity.WithReorderPolicy("idx_ordering4_timestamp");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("idx_ordering4_timestamp");
            });
        }
    }

    /// <summary>
    /// Verifies that CreateIndexOperation appears before AddReorderPolicyOperation.
    /// Reorder policies reference an index, so the index must exist first.
    /// </summary>
    [Fact]
    public void Should_Order_CreateIndex_Before_AddReorderPolicy()
    {
        // Arrange
        using OrderingContext4 context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        int createIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering4_timestamp");
        int addReorderPolicyIndex = operations.ToList().FindIndex(op =>
            op is AddReorderPolicyOperation policy && policy.IndexName == "idx_ordering4_timestamp");

        Assert.NotEqual(-1, createIndexIndex);
        Assert.NotEqual(-1, addReorderPolicyIndex);
        Assert.True(createIndexIndex < addReorderPolicyIndex,
            $"CreateIndex (index {createIndexIndex}) should appear before AddReorderPolicy (index {addReorderPolicyIndex})");
    }

    #endregion

    #region Should_Preserve_Order_For_Multiple_Tables_With_Indexes

    private class OrderingEntity5A
    {
        public int Id { get; set; }
        public DateTime Created { get; set; }
        public string? Name { get; set; }
    }

    private class OrderingEntity5B
    {
        public int Id { get; set; }
        public DateTime Updated { get; set; }
        public string? Description { get; set; }
    }

    private class OrderingEntity5C
    {
        public int Id { get; set; }
        public DateTime Deleted { get; set; }
        public string? Reason { get; set; }
    }

    private class OrderingContext5 : DbContext
    {
        public DbSet<OrderingEntity5A> EntitiesA => Set<OrderingEntity5A>();
        public DbSet<OrderingEntity5B> EntitiesB => Set<OrderingEntity5B>();
        public DbSet<OrderingEntity5C> EntitiesC => Set<OrderingEntity5C>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderingEntity5A>(entity =>
            {
                entity.ToTable("OrderingTableA5");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Created).HasDatabaseName("idx_ordering5a_created");
                entity.HasIndex(x => x.Name).HasDatabaseName("idx_ordering5a_name");
            });

            modelBuilder.Entity<OrderingEntity5B>(entity =>
            {
                entity.ToTable("OrderingTableB5");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Updated).HasDatabaseName("idx_ordering5b_updated");
                entity.HasIndex(x => x.Description).HasDatabaseName("idx_ordering5b_description");
            });

            modelBuilder.Entity<OrderingEntity5C>(entity =>
            {
                entity.ToTable("OrderingTableC5");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Deleted).HasDatabaseName("idx_ordering5c_deleted");
                entity.HasIndex(x => x.Reason).HasDatabaseName("idx_ordering5c_reason");
            });
        }
    }

    /// <summary>
    /// Verifies that when multiple tables have indexes, each table's CreateTableOperation
    /// appears before its associated CreateIndexOperations. This test is particularly sensitive
    /// to unstable sorts because all standard EF Core operations have priority 0.
    /// </summary>
    [Fact]
    public void Should_Preserve_Order_For_Multiple_Tables_With_Indexes()
    {
        // Arrange
        using OrderingContext5 context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        // Table A and its indexes
        int tableAIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingTableA5");
        int indexA1Index = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering5a_created");
        int indexA2Index = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering5a_name");

        // Table B and its indexes
        int tableBIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingTableB5");
        int indexB1Index = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering5b_updated");
        int indexB2Index = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering5b_description");

        // Table C and its indexes
        int tableCIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingTableC5");
        int indexC1Index = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering5c_deleted");
        int indexC2Index = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering5c_reason");

        // Verify all operations were found
        Assert.NotEqual(-1, tableAIndex);
        Assert.NotEqual(-1, indexA1Index);
        Assert.NotEqual(-1, indexA2Index);
        Assert.NotEqual(-1, tableBIndex);
        Assert.NotEqual(-1, indexB1Index);
        Assert.NotEqual(-1, indexB2Index);
        Assert.NotEqual(-1, tableCIndex);
        Assert.NotEqual(-1, indexC1Index);
        Assert.NotEqual(-1, indexC2Index);

        // Verify Table A comes before its indexes
        Assert.True(tableAIndex < indexA1Index,
            $"TableA (index {tableAIndex}) should appear before its first index (index {indexA1Index})");
        Assert.True(tableAIndex < indexA2Index,
            $"TableA (index {tableAIndex}) should appear before its second index (index {indexA2Index})");

        // Verify Table B comes before its indexes
        Assert.True(tableBIndex < indexB1Index,
            $"TableB (index {tableBIndex}) should appear before its first index (index {indexB1Index})");
        Assert.True(tableBIndex < indexB2Index,
            $"TableB (index {tableBIndex}) should appear before its second index (index {indexB2Index})");

        // Verify Table C comes before its indexes
        Assert.True(tableCIndex < indexC1Index,
            $"TableC (index {tableCIndex}) should appear before its first index (index {indexC1Index})");
        Assert.True(tableCIndex < indexC2Index,
            $"TableC (index {tableCIndex}) should appear before its second index (index {indexC2Index})");
    }

    #endregion

    #region Should_Order_Complex_Migration_With_All_Operation_Types

    private class OrderingParent6
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private class OrderingChild6
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public OrderingParent6? Parent { get; set; }
    }

    private class OrderingMetric6
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public int DeviceId { get; set; }
    }

    private class OrderingContext6 : DbContext
    {
        public DbSet<OrderingParent6> Parents => Set<OrderingParent6>();
        public DbSet<OrderingChild6> Children => Set<OrderingChild6>();
        public DbSet<OrderingMetric6> Metrics => Set<OrderingMetric6>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderingParent6>(entity =>
            {
                entity.ToTable("OrderingParents6");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Name).HasDatabaseName("idx_ordering6_parent_name");
            });

            modelBuilder.Entity<OrderingChild6>(entity =>
            {
                entity.ToTable("OrderingChildren6");
                entity.HasKey(x => x.Id);
                entity.HasOne(x => x.Parent)
                      .WithMany()
                      .HasForeignKey(x => x.ParentId);
            });

            modelBuilder.Entity<OrderingMetric6>(entity =>
            {
                entity.ToTable("OrderingMetrics6");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 day");
                entity.WithReorderPolicy("idx_ordering6_metric_timestamp");
                entity.HasIndex(x => x.Timestamp).HasDatabaseName("idx_ordering6_metric_timestamp");
                entity.HasIndex(x => x.DeviceId).HasDatabaseName("idx_ordering6_metric_device");
            });
        }
    }

    /// <summary>
    /// Verifies correct ordering in a complex migration with multiple operation types:
    /// CreateTable, CreateIndex, AddForeignKey, CreateHypertable, and AddReorderPolicy.
    /// This comprehensive test ensures the stable sort preserves all necessary dependencies.
    /// </summary>
    [Fact]
    public void Should_Order_Complex_Migration_With_All_Operation_Types()
    {
        // Arrange
        using OrderingContext6 context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        // Find all operation indices
        int parentTableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingParents6");
        int childTableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingChildren6");
        int metricsTableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingMetrics6");

        int parentIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering6_parent_name");
        int metricsTimestampIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering6_metric_timestamp");
        int metricsDeviceIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering6_metric_device");

        int foreignKeyIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex &&
            createIndex.Table == "OrderingChildren6" &&
            createIndex.Columns.Contains("ParentId"));
        int hypertableIndex = operations.ToList().FindIndex(op =>
            op is CreateHypertableOperation hypertable && hypertable.TableName == "OrderingMetrics6");
        int reorderPolicyIndex = operations.ToList().FindIndex(op =>
            op is AddReorderPolicyOperation);

        // Verify all operations were found
        Assert.NotEqual(-1, parentTableIndex);
        Assert.NotEqual(-1, childTableIndex);
        Assert.NotEqual(-1, metricsTableIndex);
        Assert.NotEqual(-1, parentIndexIndex);
        Assert.NotEqual(-1, metricsTimestampIndexIndex);
        Assert.NotEqual(-1, metricsDeviceIndexIndex);
        Assert.NotEqual(-1, foreignKeyIndexIndex);
        Assert.NotEqual(-1, hypertableIndex);
        Assert.NotEqual(-1, reorderPolicyIndex);

        // Verify CreateTable operations come before their dependent operations
        Assert.True(parentTableIndex < parentIndexIndex,
            "Parent table should be created before its index");
        Assert.True(parentTableIndex < foreignKeyIndexIndex,
            "Parent table should be created before foreign key index");
        Assert.True(childTableIndex < foreignKeyIndexIndex,
            "Child table should be created before foreign key index");

        // Verify metrics table dependencies
        Assert.True(metricsTableIndex < metricsTimestampIndexIndex,
            "Metrics table should be created before its timestamp index");
        Assert.True(metricsTableIndex < metricsDeviceIndexIndex,
            "Metrics table should be created before its device index");
        Assert.True(metricsTableIndex < hypertableIndex,
            "Metrics table should be created before hypertable operation");

        // Verify hypertable comes before reorder policy
        Assert.True(hypertableIndex < reorderPolicyIndex,
            "Hypertable should be created before reorder policy");

        // Verify index comes before reorder policy
        Assert.True(metricsTimestampIndexIndex < reorderPolicyIndex,
            "Index should be created before reorder policy that references it");
    }

    #endregion

    #region Should_Preserve_Relative_Order_Of_Standard_Operations

    private class OrderingMaster7
    {
        public int Id { get; set; }
        public string? Code { get; set; }
    }

    private class OrderingDetail7
    {
        public int Id { get; set; }
        public int MasterId { get; set; }
        public OrderingMaster7? Master { get; set; }
    }

    private class OrderingContext7 : DbContext
    {
        public DbSet<OrderingMaster7> Masters => Set<OrderingMaster7>();
        public DbSet<OrderingDetail7> Details => Set<OrderingDetail7>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderingMaster7>(entity =>
            {
                entity.ToTable("OrderingMasters7");
                entity.HasKey(x => x.Id);
            });

            modelBuilder.Entity<OrderingDetail7>(entity =>
            {
                entity.ToTable("OrderingDetails7");
                entity.HasKey(x => x.Id);
                entity.HasOne(x => x.Master)
                      .WithMany()
                      .HasForeignKey(x => x.MasterId);
            });
        }
    }

    /// <summary>
    /// Verifies that the stable sort preserves the relative order of standard EF Core operations.
    /// All standard operations have priority 0, so using an unstable sort (List.Sort) would
    /// scramble their order. This test ensures tables are created before their dependent indexes.
    /// </summary>
    [Fact]
    public void Should_Preserve_Relative_Order_Of_Standard_Operations()
    {
        // Arrange
        using OrderingContext7 context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        int masterTableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingMasters7");
        int detailTableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingDetails7");
        int foreignKeyIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex &&
            createIndex.Table == "OrderingDetails7" &&
            createIndex.Columns.Contains("MasterId"));

        Assert.NotEqual(-1, masterTableIndex);
        Assert.NotEqual(-1, detailTableIndex);
        Assert.NotEqual(-1, foreignKeyIndexIndex);

        // Both tables must be created before the foreign key index
        Assert.True(masterTableIndex < foreignKeyIndexIndex,
            $"Master table (index {masterTableIndex}) should be created before foreign key index (index {foreignKeyIndexIndex})");
        Assert.True(detailTableIndex < foreignKeyIndexIndex,
            $"Detail table (index {detailTableIndex}) should be created before foreign key index (index {foreignKeyIndexIndex})");
    }

    #endregion

    #region Should_Order_Hypertable_And_Indexes_Correctly

    private class OrderingMetric8
    {
        public DateTime Timestamp { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public int SensorId { get; set; }
    }

    private class OrderingContext8 : DbContext
    {
        public DbSet<OrderingMetric8> Metrics => Set<OrderingMetric8>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderingMetric8>(entity =>
            {
                entity.ToTable("OrderingMetrics8");
                entity.HasNoKey();
                entity.IsHypertable(x => x.Timestamp)
                      .WithChunkTimeInterval("1 hour");
                entity.HasIndex(x => x.Temperature).HasDatabaseName("idx_ordering8_temperature");
                entity.HasIndex(x => x.Humidity).HasDatabaseName("idx_ordering8_humidity");
                entity.HasIndex(x => x.SensorId).HasDatabaseName("idx_ordering8_sensor");
            });
        }
    }

    /// <summary>
    /// Verifies that in a hypertable with multiple indexes:
    /// 1. CreateTable comes first
    /// 2. CreateHypertable comes after table creation
    /// 3. All CreateIndex operations come after table creation
    /// </summary>
    [Fact]
    public void Should_Order_Hypertable_And_Indexes_Correctly()
    {
        // Arrange
        using OrderingContext8 context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert
        int tableIndex = operations.ToList().FindIndex(op =>
            op is CreateTableOperation createTable && createTable.Name == "OrderingMetrics8");
        int hypertableIndex = operations.ToList().FindIndex(op =>
            op is CreateHypertableOperation hypertable && hypertable.TableName == "OrderingMetrics8");
        int tempIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering8_temperature");
        int humidityIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering8_humidity");
        int sensorIndexIndex = operations.ToList().FindIndex(op =>
            op is CreateIndexOperation createIndex && createIndex.Name == "idx_ordering8_sensor");

        Assert.NotEqual(-1, tableIndex);
        Assert.NotEqual(-1, hypertableIndex);
        Assert.NotEqual(-1, tempIndexIndex);
        Assert.NotEqual(-1, humidityIndexIndex);
        Assert.NotEqual(-1, sensorIndexIndex);

        // Table must come before hypertable
        Assert.True(tableIndex < hypertableIndex,
            $"Table (index {tableIndex}) should be created before hypertable (index {hypertableIndex})");

        // Table must come before all indexes
        Assert.True(tableIndex < tempIndexIndex,
            $"Table (index {tableIndex}) should be created before temperature index (index {tempIndexIndex})");
        Assert.True(tableIndex < humidityIndexIndex,
            $"Table (index {tableIndex}) should be created before humidity index (index {humidityIndexIndex})");
        Assert.True(tableIndex < sensorIndexIndex,
            $"Table (index {tableIndex}) should be created before sensor index (index {sensorIndexIndex})");
    }

    #endregion

    #region Should_Fail_With_Unstable_Sort_Many_Tables

    // Many entity classes to generate enough operations to trigger unstable sort behavior
    private class LargeEntity01 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }
    private class LargeEntity02 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }
    private class LargeEntity03 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }
    private class LargeEntity04 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }
    private class LargeEntity05 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }
    private class LargeEntity06 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }
    private class LargeEntity07 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }
    private class LargeEntity08 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }
    private class LargeEntity09 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }
    private class LargeEntity10 { public int Id { get; set; } public string? Field1 { get; set; } public string? Field2 { get; set; } }

    private class LargeModelContext : DbContext
    {
        public DbSet<LargeEntity01> Entities01 => Set<LargeEntity01>();
        public DbSet<LargeEntity02> Entities02 => Set<LargeEntity02>();
        public DbSet<LargeEntity03> Entities03 => Set<LargeEntity03>();
        public DbSet<LargeEntity04> Entities04 => Set<LargeEntity04>();
        public DbSet<LargeEntity05> Entities05 => Set<LargeEntity05>();
        public DbSet<LargeEntity06> Entities06 => Set<LargeEntity06>();
        public DbSet<LargeEntity07> Entities07 => Set<LargeEntity07>();
        public DbSet<LargeEntity08> Entities08 => Set<LargeEntity08>();
        public DbSet<LargeEntity09> Entities09 => Set<LargeEntity09>();
        public DbSet<LargeEntity10> Entities10 => Set<LargeEntity10>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
                            .UseTimescaleDb();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure 10 tables, each with 2 indexes = 30 operations (well above 16 threshold)
            ConfigureEntity<LargeEntity01>(modelBuilder, "LargeTable01");
            ConfigureEntity<LargeEntity02>(modelBuilder, "LargeTable02");
            ConfigureEntity<LargeEntity03>(modelBuilder, "LargeTable03");
            ConfigureEntity<LargeEntity04>(modelBuilder, "LargeTable04");
            ConfigureEntity<LargeEntity05>(modelBuilder, "LargeTable05");
            ConfigureEntity<LargeEntity06>(modelBuilder, "LargeTable06");
            ConfigureEntity<LargeEntity07>(modelBuilder, "LargeTable07");
            ConfigureEntity<LargeEntity08>(modelBuilder, "LargeTable08");
            ConfigureEntity<LargeEntity09>(modelBuilder, "LargeTable09");
            ConfigureEntity<LargeEntity10>(modelBuilder, "LargeTable10");
        }

        private static void ConfigureEntity<T>(ModelBuilder modelBuilder, string tableName) where T : class
        {
            modelBuilder.Entity<T>(entity =>
            {
                entity.ToTable(tableName);
                entity.HasKey("Id");
                entity.HasIndex("Field1").HasDatabaseName($"idx_{tableName.ToLower()}_field1");
                entity.HasIndex("Field2").HasDatabaseName($"idx_{tableName.ToLower()}_field2");
            });
        }
    }

    /// <summary>
    /// This test uses a large model (10 tables with 2 indexes each = 30+ operations) to trigger
    /// the unstable sort behavior in List.Sort(). IntroSort uses InsertionSort for lists under 16 elements,
    /// which is stable. For larger lists, it uses QuickSort which is unstable.
    /// This test should FAIL when using Sort() and PASS when using OrderBy().
    /// </summary>
    [Fact]
    public void Should_Maintain_Order_With_Many_Tables_And_Indexes()
    {
        // Arrange
        using LargeModelContext context = new();

        // Act
        IReadOnlyList<MigrationOperation> operations = GenerateMigrationOperations(null, context);

        // Assert - verify each table's CreateTable comes before its indexes
        List<MigrationOperation> opsList = [.. operations];

        for (int i = 1; i <= 10; i++)
        {
            string tableName = $"LargeTable{i:D2}";
            string idx1Name = $"idx_largetable{i:D2}_field1";
            string idx2Name = $"idx_largetable{i:D2}_field2";

            int tableIndex = opsList.FindIndex(op =>
                op is CreateTableOperation ct && ct.Name == tableName);
            int index1Index = opsList.FindIndex(op =>
                op is CreateIndexOperation ci && ci.Name == idx1Name);
            int index2Index = opsList.FindIndex(op =>
                op is CreateIndexOperation ci && ci.Name == idx2Name);

            Assert.NotEqual(-1, tableIndex);
            Assert.NotEqual(-1, index1Index);
            Assert.NotEqual(-1, index2Index);

            Assert.True(tableIndex < index1Index,
                $"{tableName} (pos {tableIndex}) must come before {idx1Name} (pos {index1Index})");
            Assert.True(tableIndex < index2Index,
                $"{tableName} (pos {tableIndex}) must come before {idx2Name} (pos {index2Index})");
        }
    }

    #endregion
}
