namespace B1SLayerApp.Modelos
{
    public class SolicitudFacturaConPago
    {
        public int DocEntryEntrega { get; set; }
        public string CardCode { get; set; } = string.Empty;
        public DateTime DocDueDate { get; set; }
        public DateTime DocDate { get; set; }
        public string CashAccount { get; set; } = string.Empty;
    }

    public class EntregaSAPB1
    {
        public int DocEntry { get; set; }
        public string CardCode { get; set; } = string.Empty;
        public decimal DocTotal { get; set; }
        public List<LineaEntregaSAPB1> DocumentLines { get; set; } = [];
    }

    public class LineaEntregaSAPB1
    {
        public int LineNum { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal RemainingOpenQuantity { get; set; }
        public string TaxCode { get; set; } = string.Empty;
        public string LineStatus { get; set; } = string.Empty;
    }

    public class FacturaSAPB1
    {
        public string CardCode { get; set; } = string.Empty;
        public DateTime DocDueDate { get; set; }
        public List<LineaFacturaSAPB1> DocumentLines { get; set; } = [];
    }

    public class LineaFacturaSAPB1
    {
        public string ItemCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string TaxCode { get; set; } = string.Empty;
        public int BaseType { get; set; }
        public int BaseEntry { get; set; }
        public int BaseLine { get; set; }
    }
}
