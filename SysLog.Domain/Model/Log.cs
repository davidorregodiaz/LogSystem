using System.ComponentModel.DataAnnotations;

namespace SysLog.Domine.Model;

public class Log
{
    [Key]
    public int LogId { get; set; }
    public string Type { get; set; }
    public string Action { get; set; }
    public string Interface { get; set; }
    public string Protocol { get; set; }
    public string IpOut { get; set; }
    public string IpDestiny { get; set; }
    public string? Signature { get; set; }
    public DateTime DateTime { get; set; }

    public override string ToString()
    {
        return $@"Type : {Type},
                Action : {Action}, 
                IpOut : {IpOut}, 
                IpDestiny : {IpDestiny}, 
                Signature : {Signature}, 
                DateTime : {DateTime}, 
                Protocol : {Protocol}";
    }
}