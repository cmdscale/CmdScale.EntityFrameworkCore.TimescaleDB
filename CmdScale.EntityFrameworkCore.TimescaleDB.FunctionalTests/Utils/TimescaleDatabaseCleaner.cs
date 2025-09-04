using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.FunctionalTests.Utils;

public static class TimescaleDatabaseCleaner
{
    public static void EnsureClean(this DatabaseFacade database)
    {
        var dbCreator = database.GetService<IRelationalDatabaseCreator>();
        if (!dbCreator.Exists())
        {
            dbCreator.Create();
        }
        else
        {
            database.ExecuteSqlRaw(@"
                DO $$
                DECLARE
                    r RECORD;
                BEGIN
                    -- Drop all tables
                    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
                        EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.tablename) || ' CASCADE';
                    END LOOP;
                    -- Drop all sequences
                    FOR r IN (SELECT sequencename FROM pg_sequences WHERE schemaname = 'public') LOOP
                        EXECUTE 'DROP SEQUENCE IF EXISTS ' || quote_ident(r.sequencename) || ' CASCADE';
                    END LOOP;
                END $$;
            ");
        }
    }
}