using Jobs.DTO;
using Jobs.DTO.In;

namespace Jobs.VacancyApi.Contracts;

public interface IProcessingService
{
    Task<List<EmploymentTypeDto>> GetEmploymentTypesAsync();
    Task<EmploymentTypeDto> GetEmploymentTypeByIdAsync(int id);
    Task<List<CategoryDto>> GetCategoriesAsync();
    Task<List<WorkTypeDto>> GetWorkTypesAsync();
    Task<WorkTypeDto> GetWorkTypeByIdAsync(int id);
    Task<List<VacancyDto>> GetVacancies();
    Task<VacancyDto> GetVacancyById(int id);
    Task<VacancyDto> CreateVacancy(VacancyInDto vacancy);
    Task<int> DeleteVacancy(int id);
    Task<int> UpdateVacancy(VacancyInDto vacancy);
}