using medi_guide_be.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace medi_guide_be.Infrastructure.Services;

/// <summary>
/// Loads symptom index and disease vectors into the memory cache
/// as soon as the application starts, so the first user request is fast.
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
        const int maxRetries = 3;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Cache warm-up started (attempt {Attempt}/{Max})...", attempt, maxRetries);

                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDiseaseSimilarityService>();
                await service.WarmUpAsync(stoppingToken);

                _logger.LogInformation("Cache warm-up completed.");
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Cache warm-up attempt {Attempt}/{Max} failed.", attempt, maxRetries);

                if (attempt < maxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), stoppingToken);
                else
                    _logger.LogError(ex, "Cache warm-up failed after {Max} attempts. First request will load data on demand.", maxRetries);
            }
        }
    }
}
