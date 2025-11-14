using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Benchmarks
{
    public class InProcessConfig : ManualConfig
    {
        public InProcessConfig()
        {
            // Use the InProcessNoEmitToolchain to run benchmarks in the same process
            // This avoids creating a separate project and bypasses the build error.
            AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Instance));
        }
    }
}
