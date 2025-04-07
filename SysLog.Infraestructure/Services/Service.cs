using SysLog.Domine.Repositories;
using SysLog.Service.Services;

namespace SysLog.Repository.Services;

public class Service<T>(IRepository<T> repository) :IService<T>   where T : class
{
    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await repository.GetAllAsync();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
       return await repository.GetByIdAsync(id);
    }

    public async Task AddAsync(T entity)
    {
       await repository.AddAsync(entity);
    }

    public void Update(T obj)
    {
        repository.Update(obj);
    }

    public async Task SaveAsync()
    {
       await repository.SaveAsync();
    }

    public void Remove(T obj)
    {
        repository.Remove(obj);
    }
}
