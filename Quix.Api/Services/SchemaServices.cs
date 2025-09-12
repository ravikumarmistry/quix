namespace Quix.Api.Services
{
    public static class SchemaEndpoints
    {
        public static RouteGroupBuilder MapSchema(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/schema");




            return group;
        }
    }
    public class SchemaServices
    {
        
    }
}
