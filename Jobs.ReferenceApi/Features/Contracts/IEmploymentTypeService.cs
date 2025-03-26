using Jobs.DTO;

namespace Jobs.ReferenceApi.Features.Contracts;

public interface IEmploymentTypeService
{
    Task<List<EmploymentTypeDto>> GetEmploymentTypesAsync();
    Task<EmploymentTypeDto> GetEmploymentTypeByIdAsync(int id);
}