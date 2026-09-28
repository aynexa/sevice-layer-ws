using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B1SLayerApp.Modelos
{
    public class OrdersSAPB1
    {
        public int DocEntry { get; set; }
        public int DocNum { get; set; }
        public string CardCode { get; set; }
        public string CardName { get; set; }
        public DateTime DocDate { get; set; }
        public DateTime DocDueDate { get; set; }
        public DateTime TaxDate { get; set; }
        public string NumAtCard { get; set; }
        public string Comments { get; set; }
        public string DocStatus { get; set; }
        public decimal DocTotal { get; set; }
        public decimal PaidToDate { get; set; }
    }
}
