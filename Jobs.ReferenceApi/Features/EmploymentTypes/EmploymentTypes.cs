using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Jobs.Common.Constants;
using Jobs.Common.Contracts;
using Jobs.Common.Extentions;
using Jobs.Core.Contracts;
using Jobs.DTO;
using Jobs.Entities.Models;
using Jobs.ReferenceApi.Contracts;
using Jobs.ReferenceApi.Features.Contracts;
using Jobs.ReferenceApi.Helpers;
using Jobs.ReferenceApi.Services;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Jobs.ReferenceApi.Features.EmploymentTypes;

public static class EmploymentTypes
{
    public record QueryList : IRequest<List<EmploymentTypeDto>>;

    public record Result(List<EmploymentTypeDto> Data);
    
    public class EmploymentTypeEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/emp-types", async Task<Results<Ok<List<EmploymentTypeDto>>, BadRequest>> (HttpContext context, 
                    [FromServices] ISender mediatr, 
                    [FromServices] IApiKeyService service, 
                    [FromServices] IEncryptionService cryptService,
                    [FromServices] ISignedNonceService signedNonceService,
                    [FromServices] IHttpContextAccessor httpContextAccessor,
                    [FromHeader(Name = HttpHeaderKeys.XApiHeaderKey), Required, 
                     StringLength(HttpHeaderKeys.XApiHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiHeaderKeyMinLength)] string apiKey,
                    [FromHeader(Name = HttpHeaderKeys.SNonceHeaderKey), Required, 
                     StringLength(HttpHeaderKeys.SNonceHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.SNonceHeaderKeyMinLength)] string signedNonce,
                    [FromHeader(Name = HttpHeaderKeys.XApiSecretHeaderKey), Required, 
                     StringLength(HttpHeaderKeys.XApiSecretHeaderKeyMaxLength, MinimumLength = HttpHeaderKeys.XApiSecretHeaderKeyMinLength)] string apiSecret) =>
                {
                    /*GuardsHelper.Guards(mediatr, service, cryptService, signedNonceService, httpContextAccessor);
            
                    if (ApiSecurityHelper.IsBadRequest(httpContextAccessor, 
                            cryptService, signedNonceService, service, 
                            apiKey, signedNonce, apiSecret))
                    {
                        return TypedResults.BadRequest();
                    }*/
            
                    var ipAddress = context.Request.GetIpAddress();
                    Log.Information($"ClientIPAddress - {ipAddress}.");
            
                    var items = await mediatr.Send(new QueryList());
                    return TypedResults.Ok(items);
                }).WithName("GetEmploymentTypes")
                .RequireRateLimiting("FixedWindow")
                .WithOpenApi();
        }
    }

    public class ListEmploymentTypeQueryHandler(IEmploymentTypeService service) : IRequestHandler<QueryList, List<EmploymentTypeDto>>
    {
        public async Task<List<EmploymentTypeDto>> Handle(QueryList request, CancellationToken cancellationToken) => 
            await service.GetEmploymentTypesAsync();
    }
    
}