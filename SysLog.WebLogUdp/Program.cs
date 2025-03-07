
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SysLog.Data.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using SysLog.Domine.Model;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("PostgresConnection")));


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

//Inicia la Escucha
var udpListener = new UdpClient(514);
IPEndPoint anyIp = new IPEndPoint(IPAddress.Any, 0);

Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    while (true)
    {
        try
        {
            var received = await udpListener.ReceiveAsync();
            string logMessage = Encoding.ASCII.GetString(received.Buffer);

            var logsToSave = new List<Log>();

            // Intentar detectar JSON
            int jsonStartIndex = logMessage.IndexOf("{");
            if (jsonStartIndex != -1)
            {
                string jsonPart = logMessage.Substring(jsonStartIndex);
                using JsonDocument doc = JsonDocument.Parse(jsonPart);
                JsonElement root = doc.RootElement;

                string timestamp = root.GetProperty("timestamp").GetString();
                string inIface = root.GetProperty("in_iface").GetString();
                string srcIp = root.GetProperty("src_ip").GetString();
                int srcPort = root.GetProperty("src_port").GetInt32();
                string destIp = root.GetProperty("dest_ip").GetString();
                int destPort = root.GetProperty("dest_port").GetInt32();
                string protocol = root.GetProperty("proto").GetString();
                string eventType = root.GetProperty("event_type").GetString();

                string acction = EsIpPrivada(srcIp) ? "out" : "in";

                // Obtener la firma en caso de alert
                string? signature = null;
                if (eventType == "alert" && root.TryGetProperty("alert", out JsonElement alertElement))
                {
                    signature = alertElement.GetProperty("signature").GetString();
                }

                DateTime dateTime = DateTime.ParseExact(timestamp, "yyyy-MM-ddTHH:mm:ss.ffffffzzz",
                    CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

                logsToSave.Add(new Log()
                {
                    Type = eventType,
                    Interface = inIface,
                    Protocol = protocol,
                    IpOut = srcIp,
                    IpDestiny = destIp,
                    Acction = acction,
                    DateTime = dateTime,
                    Signature = signature
                });
            }
            else
            {
                // Caso: Mensaje tipo filterlog
                string pattern =
                    @"<\d+>([A-Za-z]+)\s+(\d+)\s+([\d:]+)\s+filterlog\[\d+\]:.*?,(em\d+\.\d+),(match|pass|block),.*?,(tcp|udp|icmp),.*?,([\d\.]+),([\d\.]+),(\d+),(\d+),";
                Match match = Regex.Match(logMessage, pattern);

                if (match.Success)
                {
                    string month = match.Groups[1].Value;
                    int day = int.Parse(match.Groups[2].Value);
                    string time = match.Groups[3].Value;
                    string inIface = match.Groups[4].Value;
                    string acction = match.Groups[5].Value;
                    string protocol = match.Groups[6].Value;
                    string srcIp = match.Groups[7].Value;
                    string destIp = match.Groups[8].Value;
                    int srcPort = int.Parse(match.Groups[9].Value);
                    int destPort = int.Parse(match.Groups[10].Value);

                    var fullDate = $"{DateTime.UtcNow.Year} {month} {day} {time}";
                    DateTime dateTime = DateTime.ParseExact(fullDate, "yyyy MMM d HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                    dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

                    logsToSave.Add(new Log()
                    {
                        Type = "filterlog",
                        Interface = inIface,
                        Protocol = protocol,
                        IpOut = srcIp,
                        IpDestiny = destIp,
                        Acction = acction,
                        DateTime = dateTime
                    });
                }
                else
                {
                    // Caso: Mensaje tipo cron
                    string cronPattern =
                        @"<\d+>([A-Za-z]+)\s+(\d+)\s+([\d:]+)\s+([^\[]+)\[(\d+)\]:\s+\((.*?)\)\s+CMD\s+\((.*)\)";
                    Match cronMatch = Regex.Match(logMessage, cronPattern);

                    if (cronMatch.Success)
                    {
                        string month = cronMatch.Groups[1].Value;
                        int day = int.Parse(cronMatch.Groups[2].Value);
                        string time = cronMatch.Groups[3].Value;
                        string process = cronMatch.Groups[4].Value;
                        string processId = cronMatch.Groups[5].Value;
                        string user = cronMatch.Groups[6].Value;
                        string command = cronMatch.Groups[7].Value;

                        var fullDate = $"{DateTime.UtcNow.Year} {month} {day} {time}";
                        DateTime dateTime = DateTime.ParseExact(fullDate, "yyyy MMM d HH:mm:ss",
                            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                        dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

                        logsToSave.Add(new Log()
                        {
                            Type = "cron",
                            Interface = process,
                            Protocol = "N/A",
                            IpOut = user,
                            IpDestiny = command,
                            Acction = "N/A",
                            DateTime = dateTime
                        });
                    }
                    else
                    {
                        // Caso: Log desconocido
                        logsToSave.Add(new Log()
                        {
                            Type = "unknown",
                            Interface = "N/A",
                            Protocol = "N/A",
                            IpOut = "N/A",
                            IpDestiny = logMessage,
                            Acction = "N/A",
                            DateTime = DateTime.UtcNow
                        });
                    }
                }
            }

            // Guardar en la base de datos si hay logs válidos
            if (logsToSave.Any())
            {
                context.AddRange(logsToSave);
                await context.SaveChangesAsync();
            }
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

    // Método para verificar si una IP es privada
    static bool EsIpPrivada(string ip)
    {
        if (IPAddress.TryParse(ip, out IPAddress ipAddress))
        {
            byte[] bytes = ipAddress.GetAddressBytes();
            return (bytes[0] == 10) ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        return false;
    }
});
app.Run();