namespace JobProject.Contracts;

public interface IMiniGenericRepository<T> where T : class
{
    bool Remove(int vacId);
    void Add(in T sender);
    Task<int> SaveAsync();
}