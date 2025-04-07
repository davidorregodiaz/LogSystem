using SysLog.Domine.Model;
using SysLog.Domine.Repositories;
using SysLog.Service.Services;

namespace SysLog.Repository.Services;

public class LogService(ILogRepository repository) : Service<Log>(repository),ILogService
{
}