# Apache 2 (OSS) Edition Support

TimescaleDB ships in two editions: the community edition (the default `timescale/timescaledb` images) and the Apache 2 edition (the `-oss` image tags). The Apache edition omits every community-only capability See the [TimescaleDB editions reference](https://www.tigerdata.com/docs/about/latest/timescaledb-editions) for the upstream feature split.

The target edition is a provider option. By default the provider targets the community edition. To target an Apache-edition server, opt in explicitly:

```csharp
optionsBuilder.UseNpgsql(connectionString)
    .UseTimescaleDb(o => o.UseApacheEdition());
```

With `UseApacheEdition()`, community-only statements are omitted from generated migration SQL. Each omitted feature leaves a `-- Skipping Community Edition feature (<feature>) - not available in Apache Edition` comment in the SQL (visible in `dotnet ef migrations script` output) and raises a warning through the EF Core logger while the SQL is generated.

Migration SQL is produced at apply/script time from the operations stored in migration files, not at `dotnet ef migrations add` time. Toggling `UseApacheEdition()` therefore changes the SQL of existing migrations without regenerating them.

> :warning: **Note:** Omitted features cause the model and the database to diverge on Apache servers. The configured model still carries the feature configuration; it simply is not applied to the database. The skip comment and the generation-time warning are the only signals.

## Feature support with `UseApacheEdition()`

| Feature | Behavior |
| --- | --- |
| Hypertables | Fully supported |
| Columnstore / compression settings | Omitted with skip comment and warning |
| Chunk skipping | Omitted with skip comment and warning |
| Compression (columnstore) policy | Omitted with skip comment and warning |
| Retention policy | Omitted with skip comment and warning |
| Reorder policy | Omitted with skip comment and warning |
| Continuous aggregates | Omitted with skip comment and warning |
| Continuous aggregate refresh policy | Omitted with skip comment and warning |
| Scaffolding (`dotnet ef dbcontext scaffold`) | Fully supported |

Scaffolding reads only catalog views that exist in both editions, so scaffolding an Apache-edition database produces a valid model.

## Edition mismatch

The option describes the target server; the provider does not probe the server's license at runtime.

- **Default (community) SQL against an Apache server:** the first community-only statement fails the migration with `functionality not supported under the current "apache" license`. Switch the context to `UseApacheEdition()`.
- **`UseApacheEdition()` SQL against a community server:** the migration succeeds, but every community-only feature in the model is silently absent from the database. Remove the option to apply the full model.
