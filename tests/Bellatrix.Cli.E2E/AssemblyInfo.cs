using Xunit;

// Live sessions share real drivers (one WinAppDriver/Appium on 4723, one Chrome): run serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
