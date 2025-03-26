using Jobs.DTO;
using Jobs.DTO.In;
using MediatR;

namespace Jobs.VacancyApi.Features.Commands;

public record CreateVacancyCommand(VacancyInDto Vacancy) : IRequest<VacancyDto>;

