
using Microsoft.EntityFrameworkCore;
using SysLog.Domine.Interfaces;
using SysLog.Domine.Repositories;
using SysLog.Repository.BackgroundServices;
using SysLog.Repository.Data;
using SysLog.Repository.Protocols;
using SysLog.Repository.Repositories;
using SysLog.Repository.Services;
using SysLog.Repository.Utilities;
using SysLog.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IUdpProtocol, UdpProtocol>();
builder.Services.AddScoped<ILogRepository, LogRepository>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IJsonParser, JsonParser>();
builder.Services.AddHostedService<CatchLogsService>();

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