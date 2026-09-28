using B1SLayer;
using B1SLayerApp.Modelos;

var serviceLayerUrl = "https://192.168.1.14:50000/b1s/v1";
var userName = "dev";
var password = "sbosap";
var companyDb = "B1H_LAZZOS_PROD2503";

try
{
    var connection = new SLConnection(serviceLayerUrl, companyDb, userName, password);
    await connection.LoginAsync();

    var sessionId = connection.LoginResponse?.SessionId;
    if (string.IsNullOrWhiteSpace(sessionId))
    {
        Console.WriteLine("Error: Service Layer no devolvió un SessionId.");
        return;
    }

    Console.WriteLine($"Session ID: {sessionId}");
    Console.WriteLine($"¡Bienvenido, {userName}!");

    // Obtener una orden de venta específica (por ejemplo, DocEntry = 50528)
    var order = await connection.Request("Orders", 50528).GetAsync<OrdersSAPB1>();

    Console.WriteLine($"DocEntry: {order.DocEntry}");
    Console.WriteLine($"DocNum: {order.DocNum}");
    Console.WriteLine($"CardCode: {order.CardCode}");
    Console.WriteLine($"CardName: {order.CardName}");

    // Obtener una lista de socios de negocio con filtro, selección y ordenamiento

    var bpList = await connection.Request("BusinessPartners")
    .Filter("CardType eq 'cCustomer'")
    .Select("CardCode, CardName, CardType, FederalTaxID, EmailAddress")
    .OrderBy("CardName")
    .WithPageSize(50)
    .WithCaseInsensitive()
    .GetAsync<List<BusinessPartnersSAPB1>>();

    Console.ReadLine();
}
catch (SLException ex)
{
    var message = ex.ErrorDetails?.Message?.Value;
    Console.WriteLine($"Error: {(string.IsNullOrWhiteSpace(message) ? ex.Message : message)}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}