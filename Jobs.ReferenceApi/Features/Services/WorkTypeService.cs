using AutoMapper;
using Jobs.Common.Contracts;
using Jobs.DTO;
using Jobs.Entities.Models;
using Jobs.ReferenceApi.Contracts;
using Jobs.ReferenceApi.Features.Contracts;
using Jobs.ReferenceApi.Services;

namespace Jobs.ReferenceApi.Features.Services;

public class WorkTypeService(IGenericRepository<WorkType> workTypesRepository, 
    ICacheService cacheService, 
    IMapper mapper) : BaseProcessingService(mapper), IWorkTypeService
{
    private readonly IMapper _mapper = mapper;

    public async Task<List<WorkTypeDto>> GetWorkTypesAsync()
    {
        await CheckOrLoadWorkTypesData();
        
        return cacheService.GetData<List<WorkTypeDto>>("workTypes");
    }
        
    public async Task<WorkTypeDto> GetWorkTypeByIdAsync(int id)
    {
        await CheckOrLoadWorkTypesData();
        
        var currentListData = cacheService.GetData<List<WorkTypeDto>>("workTypes");
        var current = currentListData.FirstOrDefault(x=>x.WorkTypeId == id);

        return _mapper.Map<WorkTypeDto>(current);
    }
        
    private async Task LoadWorkTypesDataToLocalCacheService()
    {
        var workTypes = await Task.FromResult(GetDataAsync<WorkType, WorkTypeDto>("./StorageData/WorkTypes.json",async () => await workTypesRepository.GetAllAsync()));
        cacheService.SetData("workTypes", workTypes, DateTimeOffset.UtcNow.AddYears(100));
    }
    
    # region CheckAndLoad
    private async Task CheckOrLoadWorkTypesData()
    {
        if (!cacheService.HasData("workTypes"))
            await LoadWorkTypesDataToLocalCacheService();
    }
    # endregion

}