using B1SLayer;
using B1SLayerApp.Modelos;
using B1SLayerApp.Servicios;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ConexionSapOptions>(builder.Configuration.GetSection("Sap"));
builder.Services.AddSingleton<ServicioSap>();
builder.Services.AddCors(opciones =>
    opciones.AddDefaultPolicy(politica => politica.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.MapGet("/api/salud", () => Results.Ok(new { ok = true }));

app.MapGet("/api/sesion", async (ServicioSap sap) =>
{
    try
    {
        return Results.Ok(await sap.AsegurarSesionAsync());
    }
    catch (Exception ex)
    {
        return ErrorSap(ex);
    }
});

app.MapGet("/api/ordenes/{docEntry:int}", async (int docEntry, ServicioSap sap) =>
{
    try
    {
        return Results.Ok(await sap.ObtenerOrdenAsync(docEntry));
    }
    catch (Exception ex)
    {
        return ErrorSap(ex);
    }
});

app.MapPost("/api/ordenes", async (PedidoSAPB1 pedido, ServicioSap sap) =>
{
    try
    {
        return Results.Ok(await sap.CrearOrdenAsync(pedido));
    }
    catch (Exception ex)
    {
        return ErrorSap(ex);
    }
});

app.MapGet("/api/socios", async (int? tamano, ServicioSap sap) =>
{
    try
    {
        return Results.Ok(await sap.ObtenerClientesAsync(tamano ?? 50));
    }
    catch (Exception ex)
    {
        return ErrorSap(ex);
    }
});

app.Run();

static IResult ErrorSap(Exception ex)
{
    var message = ex switch
    {
        SLException sl => string.IsNullOrWhiteSpace(sl.ErrorDetails?.Message?.Value)
            ? sl.Message
            : sl.ErrorDetails.Message.Value,
        _ => ex.Message
    };

    var status = ex is ArgumentException
        ? StatusCodes.Status400BadRequest
        : StatusCodes.Status502BadGateway;

    return Results.Json(new { error = message }, statusCode: status);
}
