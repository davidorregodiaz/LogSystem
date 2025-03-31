using SysLog.Domine.Model;
using SysLog.Domine.Repositories;
using SysLog.Repository.Data;

namespace SysLog.Repository.Repositories;

public class LogRepository(ApplicationDbContext dbContext)  : Repository<Log>(dbContext),ILogRepository
{
    
}