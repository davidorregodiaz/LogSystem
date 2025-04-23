using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SysLog.Service.Interfaces;

namespace SysLog.Repository.BackgroundServices;

public class BackupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public BackupService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var scope = _serviceProvider.CreateScope();
            var backup = scope.ServiceProvider.GetRequiredService<IBackup>();
            await backup.BackupAsync();
            
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
        
    }
}