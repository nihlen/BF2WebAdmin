using System;
using System.Collections.Generic;

namespace BF2WebAdmin.DAL.Abstractions
{
    public interface IRepository<T>
    {
        IEnumerable<T> Get();
        T Get(Guid id);
        void Create(T entity);
        void Update(T entity);
        void Delete(Guid id);
    }
}