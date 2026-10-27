using Xunit;

// Integration tests redirect the global Console.Out and set process environment variables, so the
// whole assembly runs serially to avoid cross-test races.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
