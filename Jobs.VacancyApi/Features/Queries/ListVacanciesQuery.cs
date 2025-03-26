using Jobs.DTO;
using MediatR;

namespace Jobs.VacancyApi.Features.Queries;

public record  ListVacanciesQuery : IRequest<List<VacancyDto>>;
