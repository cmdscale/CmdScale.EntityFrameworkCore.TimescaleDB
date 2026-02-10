# Reorder Policies

A reorder policy reorganizes data in a hypertable's chunks to match the order of a specific index. This process can significantly improve query performance, especially for queries that read data in the index's order, as it reduces the number of disk pages that need to be read.
The policy intelligently reorders all chunks except for the two most recent ones, which are typically still receiving active writes. By default, this optimization job runs once every 24 hours, but the schedule is fully configurable.

You can have only one reorder policy on each hypertable.

[See also: add_reorder_policy](https://docs.tigerdata.com/api/latest/hypertable/add_reorder_policy/)

### Example
Here is a complete example of configuring a reorder policy on a `Trade` hypertable. The policy is set to run every two days, starting at a specific time.

```csharp
public void Configure(EntityTypeBuilder<Trade> builder)
{
    builder.ToTable("Trades");
    builder.HasNoKey().IsHypertable(x => x.Timestamp)

    // Configure the reorder policy to use the index
    builder.WithReorderPolicy(
        "Trades_Timestamp_idx", 
        DateTime.Parse("2025-09-23T09:15:19.3905112Z"), 
        "2 days", 
        "10 minutes", 
        3, 
        "1 minute"
    );
}
```


## Supported parameters

| Parameter          | Description                                                                                                                                                              | Type      | Database Type | DefaultValue   |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------- | ------------- | -------------- |
| `IndexName` *      | The name of the existing index that the reorder policy should use to sort the data.                                                                                      | `string`  | `regclass`    | `string.Empty` |
| `InitialStart`     | The first time the policy job is scheduled to run, specified as a UTC date-time string in ISO 8601 format. If `null`, the first run is based on the `schedule_interval`. | `string?` | `TIMESTAMPTZ` | `null`         |
| `ScheduleInterval` | The interval at which the reorder policy job runs. If `null`, it defaults to '1 day'.                                                                                    | `string?` | `INTERVAL`    | `'1 day'`      |
| `MaxRuntime`       | The maximum amount of time the job is allowed to run before being stopped. If `null`, there is no time limit.                                                            | `string?` | `INTERVAL`    | `null`         |
| `MaxRetries`       | The number of times the job is retried if it fails.                                                                                                                      | `int`     | `INTEGER`     | `-1`           |
| `RetryPeriod`      | The amount of time the scheduler waits between retries of a failed job. If `null`, it defaults to '00:05:00' (5 minutes).                                                | `string?` | `INTERVAL`    | `'00:05:00'`   |

\* required
