using Bellatrix.Cli;
using Bellatrix.Cli.Output;

// Model-free entry point: shape args → call the shared engine → print the JSON contract.
// BELLATRIX_TRACE=<file> opts into one JSON trace line per invocation (no effect when unset).
return CliTrace.Run(args, CliRunner.Run, Environment.GetEnvironmentVariable(CliTrace.EnvironmentVariable));
