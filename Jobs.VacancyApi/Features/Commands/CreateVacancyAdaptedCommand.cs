using JobProject.DTOModels;
using JobsEntities.DTO;
using MediatR;

namespace JobProject.Features.Commands;

public record CreateVacancyAdaptedCommand(VacancyInDto Vacancy) : IRequest<VacancyDto>;