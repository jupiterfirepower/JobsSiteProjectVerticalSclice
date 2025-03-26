using Jobs.DTO;

namespace Jobs.ReferenceApi.Features.Contracts;

public interface IWorkTypeService
{
    Task<List<WorkTypeDto>> GetWorkTypesAsync();
    Task<WorkTypeDto> GetWorkTypeByIdAsync(int id);
}