using System.Collections.Generic;
using System.Linq;

namespace facturas.Components.Data.Reportes
{
    public class ReporteMensual
    {
        public int Mes { get; set; }
        public string NombreMes { get; set; }
        public List<Facturas> Facturas { get; set; } = new List<Facturas>();
        public decimal TotalFacturado => Facturas.Sum(f => f.Total);

        public ReporteMensual(int mes, string nombreMes)
        {
            Mes = mes;
            NombreMes = nombreMes;
        }
    }
}