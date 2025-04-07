using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SysLog.Domine.Interfaces;
using SysLog.Domine.Model;

namespace SysLog.Repository.Utilities;

public class JsonParser : IJsonParser
{
    public Log Parse(string logMessage)
    {
        int jsonStartIndex = logMessage.IndexOf("{");
        if (jsonStartIndex != -1)
        {
            string jsonPart = logMessage.Substring(jsonStartIndex);
            using JsonDocument doc = JsonDocument.Parse(jsonPart);
            JsonElement root = doc.RootElement;

            string timestamp = root.GetProperty("timestamp").GetString()!;
            string inIface = root.GetProperty("in_iface").GetString()!;
            string srcIp = root.GetProperty("src_ip").GetString()!;
            int srcPort = root.GetProperty("src_port").GetInt32();
            string destIp = root.GetProperty("dest_ip").GetString()!;
            int destPort = root.GetProperty("dest_port").GetInt32();
            string logProtocol = root.GetProperty("proto").GetString()!;
            string eventType = root.GetProperty("event_type").GetString()!;

            string action = EsIpPrivada(srcIp) ? "out" : "in";

            // Obtener la firma en caso de alert
            string? signature = null;

            if (eventType == "alert" && root.TryGetProperty("alert", out JsonElement alertElement))
            {
                signature = alertElement.GetProperty("signature").GetString();
            }

            DateTime dateTime = DateTime.ParseExact(timestamp, "yyyy-MM-ddTHH:mm:ss.ffffffzzz",
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

            return new Log
            {
                Type = eventType,
                Interface = inIface,
                Protocol = logProtocol,
                IpOut = srcIp,
                IpDestiny = destIp,
                Action = action,
                DateTime = dateTime,
                Signature = signature
            };
        }
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
                string action = match.Groups[5].Value;
                string logProtocol = match.Groups[6].Value;
                string srcIp = match.Groups[7].Value;
                string destIp = match.Groups[8].Value;
                int srcPort = int.Parse(match.Groups[9].Value);
                int destPort = int.Parse(match.Groups[10].Value);

                var fullDate = $"{DateTime.UtcNow.Year} {month} {day} {time}";
                DateTime dateTime = DateTime.ParseExact(fullDate, "yyyy MMM d HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

                return new Log
                {
                    Type = "filterlog",
                    Interface = inIface,
                    Protocol = logProtocol,
                    IpOut = srcIp,
                    IpDestiny = destIp,
                    Action = action,
                    DateTime = dateTime
                };
            }
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

                    return new Log
                    {
                        Type = "cron",
                        Interface = process,
                        Protocol = "N/A",
                        IpOut = user,
                        IpDestiny = command,
                        Action = "N/A",
                        DateTime = dateTime
                    };
                }
        
        //Log Desconocido
        return new Log
        {
            Type = "unknown",
            Interface = "N/A",
            Protocol = "N/A",
            IpOut = "N/A",
            IpDestiny = logMessage,
            Action = "N/A",
            DateTime = DateTime.UtcNow
        };
    }

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
}

