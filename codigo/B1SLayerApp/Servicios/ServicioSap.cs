using System.Net.Http;
using System.Text.Json;
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

public sealed record DocumentoCreado(int DocEntry, int DocNum, string CardCode, decimal DocTotal);

public sealed record ResultadoFacturaConPago(
    string Metodo,
    string Recurso,
    bool ChangeSetUnico,
    int DocEntryEntrega,
    decimal TotalEntrega,
    string CashAccount,
    string FacturaEnviada,
    string PagoEnviado,
    DocumentoCreado Factura,
    DocumentoCreado Pago);

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

    public async Task<ConsultaOrden> CrearOrdenAsync(PedidoSAPB1 pedido)
    {
        if (pedido is null)
            throw new ArgumentException("El cuerpo del pedido es obligatorio.");

        if (string.IsNullOrWhiteSpace(pedido.CardCode))
            throw new ArgumentException("El CardCode es obligatorio.");

        if (pedido.DocDueDate == default)
            throw new ArgumentException("La fecha de entrega es obligatoria.");

        if (pedido.DocumentLines is not [var linea])
            throw new ArgumentException("El pedido debe incluir una sola línea.");

        if (string.IsNullOrWhiteSpace(linea.ItemCode))
            throw new ArgumentException("El ItemCode es obligatorio.");

        if (linea.Quantity <= 0)
            throw new ArgumentException("La cantidad debe ser mayor que cero.");

        if (linea.UnitPrice < 0)
            throw new ArgumentException("El precio no puede ser negativo.");

        pedido.CardCode = pedido.CardCode.Trim();
        linea.ItemCode = linea.ItemCode.Trim();

        await AsegurarSesionAsync();

        var orden = await _conexion.Request("Orders").PostAsync<OrdersSAPB1>(pedido); //POSTEO UTILIZANDO B1SLayer

        if (orden is null)
            throw new InvalidOperationException("Service Layer no devolvió el pedido creado.");

        return new ConsultaOrden("POST", "Orders", orden);
    }

    public async Task<ResultadoFacturaConPago> FacturarEntregaConPagoAsync(SolicitudFacturaConPago solicitud)
    {
        if (solicitud is null)
            throw new ArgumentException("El cuerpo de la factura es obligatorio.");

        if (solicitud.DocEntryEntrega <= 0)
            throw new ArgumentException("El DocEntry de la entrega debe ser mayor que cero.");

        if (string.IsNullOrWhiteSpace(solicitud.CardCode))
            throw new ArgumentException("El CardCode es obligatorio.");

        if (solicitud.DocDueDate == default)
            throw new ArgumentException("La fecha de vencimiento de la factura es obligatoria.");

        if (solicitud.DocDate == default)
            throw new ArgumentException("La fecha del pago es obligatoria.");

        if (string.IsNullOrWhiteSpace(solicitud.CashAccount))
            throw new ArgumentException("La cuenta de efectivo es obligatoria.");

        var cardCode = solicitud.CardCode.Trim();
        var cuenta = solicitud.CashAccount.Trim();

        await AsegurarSesionAsync();

        var entrega = await _conexion.Request("DeliveryNotes", solicitud.DocEntryEntrega).GetAsync<EntregaSAPB1>();
        if (entrega is null)
            throw new InvalidOperationException($"No se encontró la entrega {solicitud.DocEntryEntrega}.");

        if (!string.Equals(entrega.CardCode, cardCode, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"La entrega {entrega.DocEntry} pertenece a {entrega.CardCode}.");

        if (entrega.DocTotal <= 0)
            throw new InvalidOperationException("La entrega no tiene un total para facturar y cobrar.");

        var lineas = LineasPorElTotalDeLaEntrega(entrega);
        var factura = new FacturaSAPB1
        {
            CardCode = entrega.CardCode,
            DocDueDate = solicitud.DocDueDate.Date,
            DocumentLines = lineas
        };
        var pagoJson = JsonPago(entrega.CardCode, solicitud.DocDate.Date, entrega.DocTotal, cuenta, ContenidoFactura);
        var facturaJson = JsonSerializer.Serialize(factura);

        const bool changeSetUnico = true;
        var solicitudFactura = new SLBatchRequest(HttpMethod.Post, "Invoices", factura, ContenidoFactura);
        var solicitudPago = new SLBatchRequest(HttpMethod.Post, "IncomingPayments", pagoJson, ContenidoPago);

        HttpResponseMessage[] respuestas;
        try
        {
            respuestas = await _conexion.PostBatchAsync([solicitudFactura, solicitudPago], changeSetUnico);
        }
        catch (SLException ex)
        {
            throw new InvalidOperationException($"El changeset se revirtió y la factura no quedó creada. {ex.Message}");
        }

        if (respuestas.Length < 2 || respuestas.Any(respuesta => !respuesta.IsSuccessStatusCode))
        {
            var cuerpo = respuestas.Length == 0
                ? "Service Layer no devolvió el batch."
                : string.Join(" ", await Task.WhenAll(respuestas.Select(TextoRespuestaAsync)));
            throw new InvalidOperationException($"El changeset se revirtió y la factura no quedó creada. {MensajeSap(cuerpo)}");
        }

        var facturaCreada = LeerDocumento(await TextoRespuestaAsync(respuestas[0]), esPago: false);
        var pagoCreado = LeerDocumento(await TextoRespuestaAsync(respuestas[1]), esPago: true);

        return new ResultadoFacturaConPago(
            "POST",
            "$batch",
            changeSetUnico,
            entrega.DocEntry,
            entrega.DocTotal,
            cuenta,
            facturaJson,
            pagoJson,
            facturaCreada,
            pagoCreado);
    }

    private const int TipoDocumentoEntrega = 15;
    private const int ContenidoFactura = 1;
    private const int ContenidoPago = 2;
    private const string ImpuestoPorDefecto = "I18";

    private static List<LineaFacturaSAPB1> LineasPorElTotalDeLaEntrega(EntregaSAPB1 entrega)
    {
        var lineas = (entrega.DocumentLines ?? [])
            .Where(linea => !string.Equals(linea.LineStatus, "bost_Close", StringComparison.Ordinal))
            .Select(linea => new LineaFacturaSAPB1
            {
                ItemCode = linea.ItemCode,
                Quantity = linea.RemainingOpenQuantity > 0 ? linea.RemainingOpenQuantity : linea.Quantity,
                TaxCode = string.IsNullOrWhiteSpace(linea.TaxCode) ? ImpuestoPorDefecto : linea.TaxCode,
                BaseType = TipoDocumentoEntrega,
                BaseEntry = entrega.DocEntry,
                BaseLine = linea.LineNum
            })
            .Where(linea => !string.IsNullOrWhiteSpace(linea.ItemCode) && linea.Quantity > 0)
            .ToList();

        if (lineas.Count == 0)
            throw new InvalidOperationException($"La entrega {entrega.DocEntry} no tiene líneas abiertas para facturar.");

        return lineas;
    }

    private static string JsonPago(string cardCode, DateTime docDate, decimal total, string cashAccount, int contenidoFactura)
    {
        var monto = JsonSerializer.Serialize(total);
        return $$"""
        {
          "CardCode": {{JsonSerializer.Serialize(cardCode)}},
          "DocDate": "{{docDate:yyyy-MM-dd}}",
          "CashSum": {{monto}},
          "CashAccount": {{JsonSerializer.Serialize(cashAccount)}},
          "PaymentInvoices": [
            {
              "DocEntry": ${{contenidoFactura}},
              "SumApplied": {{monto}},
              "InvoiceType": "it_Invoice"
            }
          ]
        }
        """;
    }

    private static async Task<string> TextoRespuestaAsync(HttpResponseMessage respuesta)
        => respuesta.Content is null ? string.Empty : await respuesta.Content.ReadAsStringAsync();

    private static DocumentoCreado LeerDocumento(string cuerpo, bool esPago)
    {
        using var documento = JsonDocument.Parse(CuerpoJson(cuerpo));
        var raiz = documento.RootElement;
        var total = esPago
            ? LeerDecimal(raiz, "CashSum")
            : LeerDecimal(raiz, "DocTotal");

        return new DocumentoCreado(
            raiz.GetProperty("DocEntry").GetInt32(),
            raiz.TryGetProperty("DocNum", out var numero) ? numero.GetInt32() : 0,
            raiz.TryGetProperty("CardCode", out var cliente) ? cliente.GetString() ?? string.Empty : string.Empty,
            total);
    }

    private static decimal LeerDecimal(JsonElement raiz, string propiedad)
        => raiz.TryGetProperty(propiedad, out var valor) ? valor.GetDecimal() : 0;

    private static string CuerpoJson(string texto)
    {
        var inicio = texto.IndexOf('{');
        var fin = texto.LastIndexOf('}');
        if (inicio < 0 || fin < inicio)
            return texto.Trim();

        return texto[inicio..(fin + 1)];
    }

    private static string MensajeSap(string cuerpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(CuerpoJson(cuerpo));
            if (documento.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var mensaje)
                && mensaje.TryGetProperty("value", out var valor)
                && !string.IsNullOrWhiteSpace(valor.GetString()))
            {
                return valor.GetString()!;
            }
        }
        catch (JsonException)
        {
        }

        return string.IsNullOrWhiteSpace(cuerpo) ? "Service Layer rechazó el batch." : cuerpo.Trim();
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
