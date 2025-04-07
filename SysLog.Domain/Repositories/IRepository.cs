namespace SysLog.Domine.Repositories;

public interface IRepository<T> where T:class 
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task AddAsync(T entity);
    void Update(T obj);
    Task SaveAsync();
    void Remove(T obj);
}