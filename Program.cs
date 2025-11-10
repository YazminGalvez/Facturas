using facturas.Components;
using facturas.Components.Data;
using Microsoft.Data.Sqlite;
using System.IO;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<ServicioFacturas>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


string rutaDb = Path.Combine(AppContext.BaseDirectory, "facturas.db");

using (var cx = new SqliteConnection($"Data Source={rutaDb}"))
{
    cx.Open();

    var cmd = cx.CreateCommand();
    cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS facturas(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            fecha TEXT,
            cliente TEXT
        );

        CREATE TABLE IF NOT EXISTS articulos(
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            facturaId INTEGER,
            nombre TEXT,
            cantidad INTEGER DEFAULT 1,
            precio REAL
        );
    ";
    cmd.ExecuteNonQuery();

    var checkCmd = cx.CreateCommand();
    checkCmd.CommandText = "PRAGMA table_info(articulos)";
    using var reader = checkCmd.ExecuteReader();
    bool tieneCantidad = false;
    while (reader.Read())
    {
        if (reader.GetString(1).Equals("cantidad", StringComparison.OrdinalIgnoreCase))
        {
            tieneCantidad = true;
            break;
        }
    }
    reader.Close();

    if (!tieneCantidad)
    {
        var alter = cx.CreateCommand();
        alter.CommandText = "ALTER TABLE articulos ADD COLUMN cantidad INTEGER DEFAULT 1";
        alter.ExecuteNonQuery();
    }
}

app.Run();