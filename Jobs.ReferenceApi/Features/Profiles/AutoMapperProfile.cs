using AutoMapper;
using Jobs.DTO;
using Jobs.Entities.Models;

namespace Jobs.ReferenceApi.Features.Profiles;

public class AutoMapperProfile: Profile
{
    public AutoMapperProfile()
    {
        CreateMap<EmploymentTypeDto, EmploymentType>().ReverseMap();
        CreateMap<WorkTypeDto, WorkType>().ReverseMap();
    }
}