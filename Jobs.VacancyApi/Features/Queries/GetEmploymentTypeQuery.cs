using Jobs.DTO;
using MediatR;

namespace Jobs.VacancyApi.Features.Queries;

public record GetEmploymentTypeQuery(int Id): IRequest<EmploymentTypeDto>;
