using RSYInventory.Data.Services;

namespace RSYInventory.Web.Services;

/// <summary>
/// After 12:00 Eastern (NC), promotes picked-up schedules into acquired vehicles.
/// </summary>
public sealed class PickupPromotionHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<PickupPromotionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay so the host finishes warming up.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var schedules = scope.ServiceProvider.GetRequiredService<PickupScheduleService>();
                var promoted = await schedules.PromoteDuePickupsAsync(stoppingToken);
                if (promoted > 0)
                    logger.LogInformation("Promoted {Count} scheduled pickup(s) to acquired vehicles.", promoted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Pickup promotion cycle failed.");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
