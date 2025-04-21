
using Microsoft.EntityFrameworkCore;
using Serilog;
using SysLog.Domine.Interfaces;
using SysLog.Domine.Repositories;
using SysLog.Repository.BackgroundServices;
using SysLog.Repository.Data;
using SysLog.Repository.Protocols;
using SysLog.Repository.Repositories;
using SysLog.Repository.Services;
using SysLog.Repository.Utilities;
using SysLog.Service.Services;


string projectRoot = Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.FullName;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(Path.Combine(projectRoot, "SysLog.Infraestructure/Logs/log-.txt"), 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IUdpProtocol, UdpProtocol>();
builder.Services.AddScoped<ILogRepository, LogRepository>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IJsonParser, JsonParser>();
builder.Services.AddHostedService<CatchLogsService>();

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddConsole();
    loggingBuilder.AddSerilog(Log.Logger, dispose : true);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Logs}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();