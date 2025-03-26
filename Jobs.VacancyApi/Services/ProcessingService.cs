using AutoMapper;
using Jobs.Common.Contracts;
using Jobs.DTO;
using Jobs.DTO.In;
using Jobs.Entities.Models;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Data;
using Microsoft.EntityFrameworkCore;

namespace Jobs.VacancyApi.Services;

public class ProcessingService(IGenericRepository<Vacancy> repository, 
    IGenericRepository<WorkType> workTypesRepository, 
    IGenericRepository<EmploymentType> empRepository, 
    IGenericRepository<Category> categoryRepository,
    //IMiniGenericRepository<VacancyWorkTypes> vacancyWorkTypesRepository,
    //IMiniGenericRepository<VacancyEmploymentTypes> vacancyEmploymentTypesRepository,
    JobsDbContext context,
    IMapper mapper) : IProcessingService
{
    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        var items = await categoryRepository.GetAllAsync();
        return mapper.Map<List<CategoryDto>>(items);
    }
    
    public async Task<List<EmploymentTypeDto>> GetEmploymentTypesAsync()
    {
        var items = await empRepository.GetAllAsync();
        return mapper.Map<List<EmploymentTypeDto>>(items);
    }
    
    public async Task<EmploymentTypeDto> GetEmploymentTypeByIdAsync(int id)
    {
        var current = await empRepository.GetByIdAsync(id);
        return mapper.Map<EmploymentTypeDto>(current);
    }
    
    public async Task<List<WorkTypeDto>> GetWorkTypesAsync()
    {
        var items = await workTypesRepository.GetAllAsync();
        return mapper.Map<List<WorkTypeDto>>(items);
    }
    
    public async Task<WorkTypeDto> GetWorkTypeByIdAsync(int id)
    {
        var current = await workTypesRepository.GetByIdAsync(id);
        return mapper.Map<WorkTypeDto>(current);
    }
    
    public async Task<List<VacancyDto>> GetVacancies()
    {
        var vacancies = await repository.GetAllAsync();
        return mapper.Map<List<VacancyDto>>(vacancies);
    }
    
    public async Task<VacancyDto> GetVacancyById(int id)
    {
        var vacancy = await repository.GetByIdAsync(id);
        return mapper.Map<VacancyDto>(vacancy);
    }

    public async Task<VacancyDto> CreateVacancy(VacancyInDto vacancy)
    {
        var newVacancy = mapper.Map<Vacancy>(vacancy);
        repository.Add(newVacancy);
        await repository.SaveAsync();
        
        var paramWorkTypes = string.Join(",", vacancy.WorkTypes);
        var count = await context.Database.ExecuteSqlRawAsync("call sp_save_vac_worktypes(@p0, @p1);", 
            parameters: new[] { (object)newVacancy.VacancyId,  paramWorkTypes});
        Console.WriteLine($"call sp_save_vac_worktypes - {count}");
        
        var paramEmploymentTypes = string.Join(",", vacancy.EmploymentTypes);
        count = await context.Database.ExecuteSqlRawAsync("call sp_save_vac_emptypes(@p0, @p1);", 
            parameters: [newVacancy.VacancyId,  paramEmploymentTypes]);
        Console.WriteLine($"sp_save_vac_emptypes - {count}");
        
        return mapper.Map<VacancyDto>(newVacancy);
    }

    public async Task<int> DeleteVacancy(int id)
    {
        bool result = repository.Remove(id);
        await repository.SaveAsync();
        return result ? 0 : -1;
    }

    public async Task<int> UpdateVacancy(VacancyInDto vacancy)
    {
        var currentVacancy = await repository.GetByIdAsync(vacancy.VacancyId);
        
        if (currentVacancy == null)
        {
            return -1;
        }

        var current = mapper.Map<Vacancy>(vacancy);
        repository.Change(currentVacancy, current);
        await repository.SaveAsync();
        return currentVacancy.VacancyId;
    }
}