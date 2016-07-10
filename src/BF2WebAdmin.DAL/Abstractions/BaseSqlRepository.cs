using System.Data;

namespace BF2WebAdmin.DAL.Abstractions
{
    public abstract class BaseSqlRepository<T>
    {
        protected IDbConnection Connection;

        protected BaseSqlRepository(IDbConnection connection)
        {
            Connection = connection;
        }
    }
}