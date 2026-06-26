using Slotify.Domain.Services;

namespace Slotify.API;

/// <summary>
/// Servicio en segundo plano que, periódicamente, despacha los recordatorios de cita
/// que ya entran en la ventana de antelación de cada negocio (best-effort). El envío
/// real lo decide el <c>INotificationSender</c> configurado (en el TFM, simulado).
/// </summary>
public class ReminderBackgroundService(IServiceProvider services, ILogger<ReminderBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
                var count = await notifications.DispatchDueRemindersAsync(DateTime.UtcNow, stoppingToken);
                if (count > 0)
                    logger.LogInformation("Recordatorios despachados: {Count}", count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo despachando recordatorios de cita");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
