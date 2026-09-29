using B1SLayer;
using B1SLayerApp.Modelos;
using Microsoft.Extensions.Options;

namespace B1SLayerApp.Servicios;

public sealed class ConexionSapOptions
{
    public string ServiceLayerUrl { get; set; } = string.Empty;
    public string CompanyDb { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed record ResultadoSesion(string SessionId, string Usuario, string CompanyDb, string ServiceLayerUrl);

public sealed record ConsultaOrden(string Metodo, string Recurso, OrdersSAPB1 Orden);

public sealed record ConsultaSocios(
    string Metodo,
    string Recurso,
    string Encabezado,
    string Filtro,
    string Seleccion,
    string Orden,
    int TamanoPagina,
    IReadOnlyList<BusinessPartnersSAPB1> Socios);

public sealed class ServicioSap
{
    public const string FiltroClientes = "CardType eq 'cCustomer'";
    public const string SeleccionClientes = "CardCode, CardName, CardType, FederalTaxID, EmailAddress";
    public const string OrdenClientes = "CardName";

    private readonly ConexionSapOptions _opciones;
    private readonly SLConnection _conexion;
    private readonly SemaphoreSlim _login = new(1, 1);

    public ServicioSap(IOptions<ConexionSapOptions> opciones)
    {
        _opciones = opciones.Value;
        if (string.IsNullOrWhiteSpace(_opciones.ServiceLayerUrl)
            || string.IsNullOrWhiteSpace(_opciones.CompanyDb)
            || string.IsNullOrWhiteSpace(_opciones.UserName))
        {
            throw new InvalidOperationException("Revisa la sección Sap en appsettings.json.");
        }

        _conexion = new SLConnection(
            _opciones.ServiceLayerUrl,
            _opciones.CompanyDb,
            _opciones.UserName,
            _opciones.Password);
    }

    public async Task<ResultadoSesion> AsegurarSesionAsync()
    {
        if (string.IsNullOrWhiteSpace(_conexion.LoginResponse?.SessionId))
        {
            await _login.WaitAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(_conexion.LoginResponse?.SessionId))
                    await _conexion.LoginAsync();
            }
            finally
            {
                _login.Release();
            }
        }

        var sessionId = _conexion.LoginResponse?.SessionId;
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new InvalidOperationException("Service Layer no devolvió un SessionId.");

        return new ResultadoSesion(sessionId, _opciones.UserName, _opciones.CompanyDb, _opciones.ServiceLayerUrl);
    }

    public async Task<ConsultaOrden> ObtenerOrdenAsync(int docEntry)
    {
        if (docEntry <= 0)
            throw new ArgumentException("El DocEntry debe ser mayor que cero.");

        await AsegurarSesionAsync();

        var orden = await _conexion.Request("Orders", docEntry).GetAsync<OrdersSAPB1>();
        if (orden is null)
            throw new InvalidOperationException($"No se encontró la orden {docEntry}.");

        return new ConsultaOrden("GET", $"Orders({docEntry})", orden);
    }

    public async Task<ConsultaSocios> ObtenerClientesAsync(int tamanoPagina)
    {
        if (tamanoPagina is < 1 or > 100)
            throw new ArgumentException("El tamaño de página debe estar entre 1 y 100.");

        await AsegurarSesionAsync();

        var socios = await _conexion.Request("BusinessPartners")
            .Filter(FiltroClientes)
            .Select(SeleccionClientes)
            .OrderBy(OrdenClientes)
            .WithPageSize(tamanoPagina)
            .WithCaseInsensitive()
            .GetAsync<List<BusinessPartnersSAPB1>>() ?? [];

        var recurso = "BusinessPartners"
            + $"?$filter={FiltroClientes}"
            + $"&$select={SeleccionClientes}"
            + $"&$orderby={OrdenClientes}";

        return new ConsultaSocios(
            "GET",
            recurso,
            $"B1S-PageSize: {tamanoPagina}",
            FiltroClientes,
            SeleccionClientes,
            OrdenClientes,
            tamanoPagina,
            socios);
    }
}
