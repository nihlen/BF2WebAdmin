namespace BF2WebAdmin.Data.Abstractions
{
    //public abstract class BaseRavenRepository<T> : IRepository<T>
    //{
    //    protected readonly IDocumentStore Store;

    //    protected BaseRavenRepository() : this(DocumentStoreHolder.Store) { }
    //    protected BaseRavenRepository(IDocumentStore store) => Store = store;

    //    public IEnumerable<T> Get()
    //    {
    //        using (var session = Store.OpenSession())
    //        {
    //            return session.Query<T>();
    //        }
    //    }

    //    public T Get(Guid id)
    //    {
    //        using (var session = Store.OpenSession())
    //        {
    //            return session.Load<T>(id);
    //        }
    //    }

    //    public void Create(T entity)
    //    {
    //        using (var session = Store.OpenSession())
    //        {
    //            session.Store(entity);
    //            session.SaveChanges();
    //        }
    //    }

    //    public void Update(T entity)
    //    {
    //        using (var session = Store.OpenSession())
    //        {
    //            session.Store(entity);
    //            session.SaveChanges();
    //        }
    //    }

    //    public void Delete(Guid id)
    //    {
    //        using (var session = Store.OpenSession())
    //        {
    //            session.Delete<T>(id);
    //            session.SaveChanges();
    //        }
    //    }
    //}
}