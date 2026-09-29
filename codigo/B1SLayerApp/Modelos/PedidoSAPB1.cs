namespace B1SLayerApp.Modelos
{
    public class PedidoSAPB1
    {
        public string CardCode { get; set; }
        public DateTime DocDueDate { get; set; }
        public List<LineaPedidoSAPB1> DocumentLines { get; set; }
    }

    public class LineaPedidoSAPB1
    {
        public string ItemCode { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
