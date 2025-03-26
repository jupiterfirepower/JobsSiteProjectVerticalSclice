using JobProject.Contracts;
using JobProject.Features.Commands;
using JobsEntities.DTO;
using MediatR;

namespace JobProject.Features.Handlers;

public class CreateVacancyAdaptedCommandHandler(IProcessingService service) : IRequestHandler<CreateVacancyAdaptedCommand, VacancyDto>
{
    public async Task<VacancyDto> Handle(CreateVacancyAdaptedCommand command, CancellationToken cancellationToken) =>
        await service.CreateVacancy(command.Vacancy);
}