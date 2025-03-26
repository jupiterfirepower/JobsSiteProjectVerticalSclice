using AutoMapper;
using Jobs.Common.Contracts;
using Jobs.DTO;
using Jobs.Entities.Models;
using Jobs.ReferenceApi.Contracts;
using Jobs.ReferenceApi.Features.Contracts;
using Jobs.ReferenceApi.Services;

namespace Jobs.ReferenceApi.Features.Services;

public class EmploymentTypeService(IGenericRepository<EmploymentType> empRepository, 
    ICacheService cacheService, 
    IMapper mapper) : BaseProcessingService(mapper), IEmploymentTypeService
{
    public async Task<List<EmploymentTypeDto>> GetEmploymentTypesAsync()
    {
        await CheckOrLoadEmploymentTypesData();
        
        return cacheService.GetData<List<EmploymentTypeDto>>("empTypes");
    }
        
    public async Task<EmploymentTypeDto> GetEmploymentTypeByIdAsync(int id)
    {
        await CheckOrLoadEmploymentTypesData();
        
        var currentListData = cacheService.GetData<List<EmploymentTypeDto>>("empTypes");
        var current = currentListData.FirstOrDefault(x=>x.EmploymentTypeId == id);

        return mapper.Map<EmploymentTypeDto>(current);
    }
        
    private async Task LoadEmploymentTypesDataToLocalCacheService()
    {
        var empTypes = await Task.FromResult(GetDataAsync<EmploymentType, EmploymentTypeDto>("./StorageData/EmpTypes.json",async () => await empRepository.GetAllAsync()));
        cacheService.SetData("empTypes", empTypes, DateTimeOffset.UtcNow.AddYears(100));
    }
    
    # region CheckAndLoad
    private async Task CheckOrLoadEmploymentTypesData()
    {
        if (!cacheService.HasData("empTypes"))
            await LoadEmploymentTypesDataToLocalCacheService();
    }
    # endregion

}