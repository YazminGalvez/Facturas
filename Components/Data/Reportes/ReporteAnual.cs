using System.Collections.Generic;
using System.Linq;

namespace facturas.Components.Data.Reportes
{
    public class ReporteAnual
    {
        public int Anio { get; set; }
        public List<ReporteMensual> Meses { get; set; } = new List<ReporteMensual>();
        public decimal TotalAnual => Meses.Sum(m => m.TotalFacturado);
    }
}