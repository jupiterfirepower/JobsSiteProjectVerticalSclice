using Jobs.Common.Contracts;
using Jobs.Entities.Models;
using Jobs.VacancyApi.Data;
using Microsoft.EntityFrameworkCore;

namespace Jobs.VacancyApi.Repository;

public class VacancyEmploymentTypesRepository(JobsDbContext context): IMiniGenericRepository<VacancyEmploymentTypes>, IDisposable
{
    public bool Remove(int vacId)
    {
        var items = context.VacancyEmploymentTypes.
            Where(x => x.VacancyId == vacId).ToList();
        
        items.ForEach(c=> context.VacancyEmploymentTypes.Remove(c));
        
        return true;
    }

    public void Add(in VacancyEmploymentTypes sender)
    {
        context.Add(sender).State = EntityState.Added;
    }

    public Task<int> SaveAsync()
    {
        return context.SaveChangesAsync();
    }

    private bool _disposed = false;

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                context.Dispose();
            }
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}