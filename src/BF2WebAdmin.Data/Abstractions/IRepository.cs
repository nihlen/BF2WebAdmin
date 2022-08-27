using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BF2WebAdmin.Data.Abstractions;

public interface IRepository<T>
{
    Task<IEnumerable<T>> GetAsync();
    Task<T> GetAsync(Guid id);
    Task CreateAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}