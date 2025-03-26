using Jobs.DTO;
using Jobs.VacancyApi.Contracts;
using Jobs.VacancyApi.Features.Commands;
using MediatR;

namespace Jobs.VacancyApi.Features.Handlers;

public class CreateVacancyCommandHandler(IProcessingService service) : IRequestHandler<CreateVacancyCommand, VacancyDto>
{
    public async Task<VacancyDto> Handle(CreateVacancyCommand command, CancellationToken cancellationToken) =>
        await service.CreateVacancy(command.Vacancy);
}