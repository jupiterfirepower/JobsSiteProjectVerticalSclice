using AutoMapper;
using Jobs.Common.Contracts;
using Jobs.CompanyApi.Services.Contracts;
using Jobs.DTO;
using Jobs.DTO.In;
using Jobs.Entities.Models;

namespace Jobs.CompanyApi.Services;

public class ProcessingService(IGenericRepository<Company> repository, IMapper mapper) : IProcessingService
{
    public async Task<List<CompanyDto>> GetCompanies()
    {
        var vacancies = await repository.GetAllAsync();
        return mapper.Map<List<CompanyDto>>(vacancies);
    }
    
    public async Task<CompanyDto> GetCompanyById(int id)
    {
        var company = await repository.GetByIdAsync(id);
        return mapper.Map<CompanyDto>(company);
    }

    public async Task<CompanyDto> CreateCompany(CompanyInDto vacancy)
    {
        var newCompany = mapper.Map<Company>(vacancy);
        repository.Add(newCompany);
        await repository.SaveAsync();
        return mapper.Map<CompanyDto>(newCompany);
    }

    public async Task<int> DeleteCompany(int id)
    {
        bool result = repository.Remove(id);
        await repository.SaveAsync();
        return result ? 0 : -1;
    }

    public async Task<int> UpdateCompany(CompanyInDto company)
    {
        var currentCompany = await repository.GetByIdAsync(company.CompanyId);
        
        if (currentCompany == null)
        {
            return -1;
        }

        var current = mapper.Map<Company>(company);
        repository.Change(currentCompany, current);
        await repository.SaveAsync();
        return currentCompany.CompanyId;
    }
}