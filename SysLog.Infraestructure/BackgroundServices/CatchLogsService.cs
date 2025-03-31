
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SysLog.Domine.Interfaces;
using SysLog.Service.Services;

namespace SysLog.Repository.BackgroundServices;

public class CatchLogsService : BackgroundService
{
    
    private readonly IServiceProvider _serviceProvider;

    public CatchLogsService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scope = _serviceProvider.CreateScope();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
        var protocol = scope.ServiceProvider.GetRequiredService<IUdpProtocol>();
        var parser = scope.ServiceProvider.GetRequiredService<IJsonParser>();
        
        while (true)
        {
            try
            {
                protocol.Start();
                var logMessage = await protocol.CatchLog();
                
                // Intenta Parsear JSON a Log
                var log = parser.Parse(logMessage);

                // Guardar en la base de datos si hay logs válidos
                await logService.AddAsync(log);
                await logService.SaveAsync();
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"Error al procesar JSON: {jsonEx.Message}");
            }
            catch (FormatException fmtEx)
            {
                Console.WriteLine($"Error en formato de fecha u otro valor: {fmtEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general: {ex.Message}");
            }
        }
    }

    
}