using CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Utils;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Tests.Integration;

/// <summary>
/// Runs the license-neutral hypertable facts from <see cref="HypertableIntegrationTestsBase"/>
/// against the Apache-edition (OSS) TimescaleDB image to prove they do not depend on any
/// Community-only feature.
/// </summary>
public class HypertableApacheIntegrationTests : HypertableIntegrationTestsBase
{
    protected override string Image => TimescaleImages.Apache;
}
