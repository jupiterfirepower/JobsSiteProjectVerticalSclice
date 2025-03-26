using Jobs.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Jobs.CompanyApi.DBContext;

public class CompanyDbContext(DbContextOptions<CompanyDbContext>
    options) : DbContext(options)
{
    /*public JobsDbContext(DbContextOptions<JobsDbContext> options) : base(options)
    {
        Database.EnsureCreated();
    }*/
    
    public DbSet<SecretApiKey> ApiKeys => Set<SecretApiKey>();
    public DbSet<Company> Companies => Set<Company>();
    
   
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.Seed();
    }
}