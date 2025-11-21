namespace facturas.Components.Data
{
    public class Estadisticas
    {
        public bool DatosCargados { get; set; } = false;
        public decimal VentasHoy { get; set; }
        public decimal VentasMes { get; set; }
    }
}