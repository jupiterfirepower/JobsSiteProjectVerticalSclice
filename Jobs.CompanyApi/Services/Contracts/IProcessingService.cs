using Jobs.DTO;
using Jobs.DTO.In;

namespace Jobs.CompanyApi.Services.Contracts;

public interface IProcessingService
{
    Task<List<CompanyDto>> GetCompanies();
    Task<CompanyDto> GetCompanyById(int id);

    Task<CompanyDto> CreateCompany(CompanyInDto vacancy);

    Task<int> DeleteCompany(int id);
    //Task<int> HideCompany(int id);
    Task<int> UpdateCompany(CompanyInDto vacancy);
}