using Jobs.DTO;
using MediatR;

namespace Jobs.VacancyApi.Features.Queries;

public record GetVacancyQuery(int Id): IRequest<VacancyDto>;
