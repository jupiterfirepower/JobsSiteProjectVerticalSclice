using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Jobs.Common.Constants;
using Jobs.Common.Extentions;
using Jobs.Core.Contracts;
using Jobs.DTO;
using Jobs.Entities.Models;
using Jobs.ReferenceApi.Contracts;
using Jobs.ReferenceApi.Features.Contracts;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Jobs.ReferenceApi.Features.WorkTypes;

public static class GetWorkTypeById
{
    public record GetQuery(int Id): IRequest<WorkTypeDto>;
    
   /* public class MapperProfile : Profile
    {
        public MapperProfile()
        {
            CreateMap<WorkTypeDto, WorkType>().ReverseMap();
        }
    }*/
    
    private static readonly ConcurrentDictionary<int, WorkTypeDto> Cache = new ();

    private static Func<int, WorkTypeDto> Memoize(this Func<int, WorkTypeDto> f)
    {
        return a => Cache.GetOrAdd(a, f);
    }
    
    public class GetWorkTypeEndpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("/work-types/{id:int}", async Task<Results<Ok<WorkTypeDto>, BadRequest, NotFound>> (int id, HttpContext context, 
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
                
                Func<int, WorkTypeDto> getWorkType = xid =>
                {
                    Console.WriteLine($"GetWorkTypeById - {xid}.");
                    var task = Task.Run(async () => await mediatr.Send(new GetQuery(xid)));
                    return task.ConfigureAwait(false).GetAwaiter().GetResult();
                };

                var memoizedFunc = getWorkType.Memoize();
                var result = memoizedFunc(id);
                //var result = await mediatr.Send(new GetQuery(id));
                return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
            })
            .AddEndpointFilter(async (context, next) =>
            {
                var id = context.GetArgument<int>(0);
       
                if (id <= 0)
                {
                    return TypedResults.BadRequest();
                }

                return await next(context);
            })
            .WithName("GetWorkType")
            .RequireRateLimiting("FixedWindow")
            .WithOpenApi();
        }
    }
    
    public class GetWorkTypeQueryHandler(IWorkTypeService service) : IRequestHandler<GetQuery, WorkTypeDto>
    {
        public async Task<WorkTypeDto> Handle(GetQuery request, CancellationToken cancellationToken) => await service.GetWorkTypeByIdAsync(request.Id);
    }
}