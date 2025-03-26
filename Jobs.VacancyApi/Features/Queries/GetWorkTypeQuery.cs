using Jobs.DTO;
using MediatR;

namespace Jobs.VacancyApi.Features.Queries;

public record GetWorkTypeQuery(int Id): IRequest<WorkTypeDto>;
