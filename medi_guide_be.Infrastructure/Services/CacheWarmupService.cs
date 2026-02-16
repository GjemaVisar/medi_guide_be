using medi_guide_be.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace medi_guide_be.Infrastructure.Services;

/// <summary>
/// Loads symptom index and disease vectors into the memory cache
/// as soon as the application starts, so the first user request is fast.
///
/// When the "vector_cache" collection exists (after a rebuild), this
/// completes in seconds. Otherwise it falls back to the slow raw-collection
/// path and logs a warning.
/// </summary>
public class CacheWarmupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CacheWarmupService> _logger;

    public CacheWarmupService(IServiceProvider serviceProvider, ILogger<CacheWarmupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Cache warm-up started...");

            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDiseaseSimilarityService>();
            await service.WarmUpAsync(stoppingToken);

            sw.Stop();
            _logger.LogInformation(
                "Cache warm-up completed in {Elapsed}ms.",
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Cache warm-up failed. First request will load data on demand.");
        }
    }
}
