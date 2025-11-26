using System.Collections.Generic;

namespace facturas.Components.Data
{
    public class Estadisticas
    {
        public bool DatosCargados { get; set; } = false;
        public decimal VentasHoy { get; set; }
        public decimal VentasMes { get; set; }
        public int TotalFacturasHistorico { get; set; }
        public string MejorMesNombre { get; set; } = "---";
        public int MejorMesCantidad { get; set; } = 0;
        public string ProductoMasVendido { get; set; } = "---";
        public int CantidadProductoMasVendido { get; set; } = 0;
        public string ClienteMayorFacturador { get; set; } = "---";
        public decimal MontoMayorFacturador { get; set; } = 0;
        public string ClienteMasActivo { get; set; } = "---";
        public int CantidadFacturasMasActivo { get; set; } = 0;

        public List<Facturas> UltimasFacturas { get; set; } = new List<Facturas>();

        public List<DatoGrafico> HistoricoVentas { get; set; } = new List<DatoGrafico>();
    }

    public class DatoGrafico
    {
        public string Etiqueta { get; set; } 
        public decimal Valor { get; set; }   
    }
}