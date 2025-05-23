using Microsoft.EntityFrameworkCore;
using SysLog.Domine.Model;

namespace SysLog.Repository.Data;     //public class DataContext : DbContext

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    private DbSet<Log> Log { get; set; }
}