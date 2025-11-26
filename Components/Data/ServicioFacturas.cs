using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace facturas.Components.Data
{
    public class ServicioFacturas
    {
        private string RutaDb => Path.Combine(AppContext.BaseDirectory, "facturas.db");

        public async Task<List<Facturas>> ObtenerFacturas()
        {
            var lista = new List<Facturas>();

            using var cx = new SqliteConnection($"Data Source={RutaDb}");
            await cx.OpenAsync();

            var cmd = cx.CreateCommand();
            cmd.CommandText = "SELECT id, fecha, cliente FROM facturas ORDER BY id DESC";

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var f = new Facturas
                {
                    Id = rd.GetInt32(0),
                    Fecha = DateTime.Parse(rd.GetString(1)),
                    Cliente = rd.GetString(2)
                };

                f.Articulos = await ObtenerArticulos(f.Id);
                lista.Add(f);
            }

            return lista;
        }

        public async Task<Facturas> ObtenerFacturaPorId(int id)
        {
            Facturas f = null;
            using var cx = new SqliteConnection($"Data Source={RutaDb}");
            await cx.OpenAsync();

            var cmd = cx.CreateCommand();
            cmd.CommandText = "SELECT id, fecha, cliente FROM facturas WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);

            using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                f = new Facturas
                {
                    Id = rd.GetInt32(0),
                    Fecha = DateTime.Parse(rd.GetString(1)),
                    Cliente = rd.GetString(2)
                };
                f.Articulos = await ObtenerArticulos(f.Id);
            }

            return f;
        }

        private async Task<List<Articulos>> ObtenerArticulos(int facturaId)
        {
            var lista = new List<Articulos>();

            using var cx = new SqliteConnection($"Data Source={RutaDb}");
            await cx.OpenAsync();

            var cmd = cx.CreateCommand();
            cmd.CommandText = "SELECT id, nombre, cantidad, precio FROM articulos WHERE facturaId = $id";
            cmd.Parameters.AddWithValue("$id", facturaId);

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                lista.Add(new Articulos
                {
                    Id = rd.GetInt32(0),
                    FacturaId = facturaId,
                    Nombre = rd.GetString(1),
                    Cantidad = rd.GetInt32(2),
                    Precio = (decimal)rd.GetDouble(3)
                });
            }

            return lista;
        }

        public async Task AgregarFactura(Facturas f)
        {
            using var cx = new SqliteConnection($"Data Source={RutaDb}");
            await cx.OpenAsync();
            using var transaction = cx.BeginTransaction();

            try
            {
                var cmd = cx.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO facturas(fecha, cliente) VALUES($fecha, $cliente); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("$fecha", f.Fecha.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("$cliente", f.Cliente);

                object result = await cmd.ExecuteScalarAsync();

                if (result != null && result != DBNull.Value)
                {
                    var id = (long)result;
                    f.Id = (int)id;
                }
                else
                {
                    throw new InvalidOperationException("Error al guardar la factura. La base de datos no devolvió un ID.");
                }

                foreach (var a in f.Articulos)
                {
                    var cmdArticulo = cx.CreateCommand();
                    cmdArticulo.Transaction = transaction;
                    cmdArticulo.CommandText = "INSERT INTO articulos(facturaId, nombre, cantidad, precio) VALUES($facturaId, $nombre, $cantidad, $precio)";
                    cmdArticulo.Parameters.AddWithValue("$facturaId", f.Id);
                    cmdArticulo.Parameters.AddWithValue("$nombre", a.Nombre);
                    cmdArticulo.Parameters.AddWithValue("$cantidad", a.Cantidad);
                    cmdArticulo.Parameters.AddWithValue("$precio", a.Precio);
                    await cmdArticulo.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private async Task AgregarArticulo(int facturaId, Articulos a)
        {
            using var cx = new SqliteConnection($"Data Source={RutaDb}");
            await cx.OpenAsync();

            var cmd = cx.CreateCommand();
            cmd.CommandText = "INSERT INTO articulos(facturaId, nombre, cantidad, precio) VALUES($facturaId, $nombre, $cantidad, $precio)";
            cmd.Parameters.AddWithValue("$facturaId", facturaId);
            cmd.Parameters.AddWithValue("$nombre", a.Nombre);
            cmd.Parameters.AddWithValue("$cantidad", a.Cantidad);
            cmd.Parameters.AddWithValue("$precio", a.Precio);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task EliminarFactura(Facturas f)
        {
            using var cx = new SqliteConnection($"Data Source={RutaDb}");
            await cx.OpenAsync();
            using var transaction = cx.BeginTransaction();

            try
            {
                var cmd1 = cx.CreateCommand();
                cmd1.Transaction = transaction;
                cmd1.CommandText = "DELETE FROM articulos WHERE facturaId = $id";
                cmd1.Parameters.AddWithValue("$id", f.Id);
                await cmd1.ExecuteNonQueryAsync();

                var cmd2 = cx.CreateCommand();
                cmd2.Transaction = transaction;
                cmd2.CommandText = "DELETE FROM facturas WHERE id = $id";
                cmd2.Parameters.AddWithValue("$id", f.Id);
                await cmd2.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task ActualizarFactura(Facturas f)
        {
            using var cx = new SqliteConnection($"Data Source={RutaDb}");
            await cx.OpenAsync();
            using var transaction = cx.BeginTransaction();

            try
            {
                var cmdFactura = cx.CreateCommand();
                cmdFactura.Transaction = transaction;
                cmdFactura.CommandText = "UPDATE facturas SET fecha = $fecha, cliente = $cliente WHERE id = $id";
                cmdFactura.Parameters.AddWithValue("$fecha", f.Fecha.ToString("yyyy-MM-dd"));
                cmdFactura.Parameters.AddWithValue("$cliente", f.Cliente);
                cmdFactura.Parameters.AddWithValue("$id", f.Id);
                await cmdFactura.ExecuteNonQueryAsync();

                var cmdDelete = cx.CreateCommand();
                cmdDelete.Transaction = transaction;
                cmdDelete.CommandText = "DELETE FROM articulos WHERE facturaId = $id";
                cmdDelete.Parameters.AddWithValue("$id", f.Id);
                await cmdDelete.ExecuteNonQueryAsync();

                var cmdInsert = cx.CreateCommand();
                cmdInsert.Transaction = transaction;
                cmdInsert.CommandText = "INSERT INTO articulos(facturaId, nombre, cantidad, precio) VALUES($facturaId, $nombre, $cantidad, $precio)";

                var pFacturaId = cmdInsert.Parameters.AddWithValue("$facturaId", f.Id);
                var pNombre = cmdInsert.Parameters.AddWithValue("$nombre", string.Empty);
                var pCantidad = cmdInsert.Parameters.AddWithValue("$cantidad", 0);
                var pPrecio = cmdInsert.Parameters.AddWithValue("$precio", 0.0m);

                foreach (var a in f.Articulos)
                {
                    pNombre.Value = a.Nombre;
                    pCantidad.Value = a.Cantidad;
                    pPrecio.Value = a.Precio;
                    await cmdInsert.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<List<Facturas>> ObtenerFacturasAnuales(int anio)
        {
            var lista = new List<Facturas>();

            using var cx = new SqliteConnection($"Data Source={RutaDb}");
            await cx.OpenAsync();

            var cmd = cx.CreateCommand();
            cmd.CommandText = @"
                SELECT id, fecha, cliente 
                FROM facturas 
                WHERE strftime('%Y', fecha) = $anio
                ORDER BY fecha, id DESC;
            ";
            cmd.Parameters.AddWithValue("$anio", anio.ToString());

            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var f = new Facturas
                {
                    Id = rd.GetInt32(0),
                    Fecha = DateTime.Parse(rd.GetString(1)),
                    Cliente = rd.GetString(2)
                };

                f.Articulos = await ObtenerArticulos(f.Id);
                lista.Add(f);
            }

            return lista;
        }

        public async Task<Estadisticas> ObtenerDashboard()
        {
            var stats = new Estadisticas();

            using var cx = new SqliteConnection($"Data Source={RutaDb}");
            await cx.OpenAsync();

            var cmdHoy = cx.CreateCommand();
            cmdHoy.CommandText = @"
                SELECT SUM(a.cantidad * a.precio) 
                FROM facturas f
                JOIN articulos a ON f.id = a.facturaId
                WHERE date(f.fecha) = date('now', 'localtime')";
            var resultHoy = await cmdHoy.ExecuteScalarAsync();
            stats.VentasHoy = (resultHoy != null && resultHoy != DBNull.Value) ? Convert.ToDecimal(resultHoy) : 0;

            var cmdMes = cx.CreateCommand();
            cmdMes.CommandText = @"
                SELECT SUM(a.cantidad * a.precio) 
                FROM facturas f
                JOIN articulos a ON f.id = a.facturaId
                WHERE strftime('%Y-%m', f.fecha) = strftime('%Y-%m', 'now', 'localtime')";
            var resultMes = await cmdMes.ExecuteScalarAsync();
            stats.VentasMes = (resultMes != null && resultMes != DBNull.Value) ? Convert.ToDecimal(resultMes) : 0;

            var cmdTotal = cx.CreateCommand();
            cmdTotal.CommandText = "SELECT COUNT(*) FROM facturas";
            var resultTotal = await cmdTotal.ExecuteScalarAsync();
            stats.TotalFacturasHistorico = (resultTotal != null && resultTotal != DBNull.Value) ? Convert.ToInt32(resultTotal) : 0;

            // --- NUEVO: TOTAL ARTICULOS HISTÓRICO (TODOS) ---
            var cmdArtsHist = cx.CreateCommand();
            cmdArtsHist.CommandText = "SELECT SUM(cantidad) FROM articulos";
            var resArtsHist = await cmdArtsHist.ExecuteScalarAsync();
            stats.TotalArticulosHistorico = (resArtsHist != null && resArtsHist != DBNull.Value) ? Convert.ToInt32(resArtsHist) : 0;
            // ------------------------------------------------

            var cmdMejorMes = cx.CreateCommand();
            cmdMejorMes.CommandText = @"
                SELECT strftime('%Y-%m', fecha) as mes, COUNT(*) as total 
                FROM facturas 
                GROUP BY mes 
                ORDER BY total DESC 
                LIMIT 1";
            using (var rd = await cmdMejorMes.ExecuteReaderAsync())
            {
                if (await rd.ReadAsync())
                {
                    string mesStr = rd.GetString(0);
                    DateTime dt = DateTime.ParseExact(mesStr, "yyyy-MM", CultureInfo.InvariantCulture);
                    stats.MejorMesNombre = dt.ToString("MMMM yyyy", new CultureInfo("es-ES"));
                    if (stats.MejorMesNombre.Length > 0)
                        stats.MejorMesNombre = char.ToUpper(stats.MejorMesNombre[0]) + stats.MejorMesNombre.Substring(1);

                    stats.MejorMesCantidad = rd.GetInt32(1);
                }
            }

            var cmdTop = cx.CreateCommand();
            cmdTop.CommandText = @"
                SELECT nombre, SUM(cantidad) as total_vendido
                FROM articulos
                GROUP BY nombre
                ORDER BY total_vendido DESC
                LIMIT 1";
            using (var reader = await cmdTop.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    stats.ProductoMasVendido = reader.IsDBNull(0) ? "---" : reader.GetString(0);
                    stats.CantidadProductoMasVendido = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                }
            }

            var cmdMayor = cx.CreateCommand();
            cmdMayor.CommandText = @"
                SELECT f.cliente, SUM(a.cantidad * a.precio) as total
                FROM facturas f
                JOIN articulos a ON f.id = a.facturaId
                GROUP BY f.cliente
                ORDER BY total DESC
                LIMIT 1";
            using (var readerMF = await cmdMayor.ExecuteReaderAsync())
            {
                if (await readerMF.ReadAsync())
                {
                    stats.ClienteMayorFacturador = readerMF.IsDBNull(0) ? "---" : readerMF.GetString(0);
                    stats.MontoMayorFacturador = readerMF.IsDBNull(1) ? 0 : readerMF.GetDecimal(1);
                }
            }

            var cmdActivo = cx.CreateCommand();
            cmdActivo.CommandText = @"
                SELECT cliente, COUNT(id) as total_facts
                FROM facturas
                GROUP BY cliente
                ORDER BY total_facts DESC
                LIMIT 1";
            using (var readerMA = await cmdActivo.ExecuteReaderAsync())
            {
                if (await readerMA.ReadAsync())
                {
                    stats.ClienteMasActivo = readerMA.IsDBNull(0) ? "---" : readerMA.GetString(0);
                    stats.CantidadFacturasMasActivo = readerMA.IsDBNull(1) ? 0 : readerMA.GetInt32(1);
                }
            }

            var cmdTopList = cx.CreateCommand();
            cmdTopList.CommandText = @"
                SELECT nombre, SUM(cantidad) as total
                FROM articulos
                GROUP BY nombre
                ORDER BY total DESC
                LIMIT 5";
            using (var readerTop = await cmdTopList.ExecuteReaderAsync())
            {
                while (await readerTop.ReadAsync())
                {
                    stats.TopProductos.Add(new ProductoTop
                    {
                        Nombre = readerTop.GetString(0),
                        Cantidad = readerTop.GetInt32(1)
                    });
                }
            }

            var cmdList = cx.CreateCommand();
            cmdList.CommandText = "SELECT id, fecha, cliente FROM facturas ORDER BY id DESC LIMIT 5";

            var tempLista = new List<Facturas>();
            using (var readerList = await cmdList.ExecuteReaderAsync())
            {
                while (await readerList.ReadAsync())
                {
                    tempLista.Add(new Facturas
                    {
                        Id = readerList.GetInt32(0),
                        Fecha = DateTime.Parse(readerList.GetString(1)),
                        Cliente = readerList.GetString(2)
                    });
                }
            }

            foreach (var f in tempLista)
            {
                var cmdArts = cx.CreateCommand();
                cmdArts.CommandText = "SELECT nombre, cantidad, precio FROM articulos WHERE facturaId = $id";
                cmdArts.Parameters.AddWithValue("$id", f.Id);

                using (var readerArts = await cmdArts.ExecuteReaderAsync())
                {
                    while (await readerArts.ReadAsync())
                    {
                        f.Articulos.Add(new Articulos
                        {
                            Nombre = readerArts.GetString(0),
                            Cantidad = readerArts.GetInt32(1),
                            Precio = (decimal)readerArts.GetDouble(2)
                        });
                    }
                }
                stats.UltimasFacturas.Add(f);
            }

            var cmdHist = cx.CreateCommand();
            cmdHist.CommandText = @"
                SELECT strftime('%Y-%m', f.fecha) as mes, SUM(a.cantidad * a.precio) 
                FROM facturas f 
                JOIN articulos a ON f.id = a.facturaId 
                WHERE f.fecha >= date('now', '-5 months', 'start of month')
                GROUP BY mes 
                ORDER BY mes";

            using (var rd = await cmdHist.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    string mesStr = rd.GetString(0);
                    DateTime dt = DateTime.ParseExact(mesStr, "yyyy-MM", CultureInfo.InvariantCulture);
                    string nombreMes = dt.ToString("MMM", new CultureInfo("es-ES")).ToUpper();

                    stats.HistoricoVentas.Add(new DatoGrafico
                    {
                        Etiqueta = nombreMes,
                        Valor = rd.GetDecimal(1)
                    });
                }
            }

            stats.DatosCargados = true;
            return stats;
        }
    }
}