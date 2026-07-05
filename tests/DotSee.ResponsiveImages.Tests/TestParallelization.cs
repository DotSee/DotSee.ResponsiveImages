using Xunit;

// The renderers rely on Umbraco's StaticServiceProvider.Instance (a global) for argless
// .Url() / GetCropUrl calls. Each harness sets it, so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
