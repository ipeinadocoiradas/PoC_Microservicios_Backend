using System.Text.Json.Serialization;
using Almacen.Application;
using Almacen.Domain;
using Almacen.Infrastructure;
using DistributedTransactions.ServiceDefaults;
using DistributedTransactions.ServiceDefaults.Http;

var builder = WebApplication.CreateBuilder(args);

builder.AgregarServiceDefaults();

builder.Services
    .AgregarAplicacionAlmacen()
    .AgregarInfraestructuraAlmacen(builder.Configuration)
    .AgregarGestionExcepciones<ExcepcionDominio>();

builder.Services
    .AddControllers()
    .AddJsonOptions(opciones => opciones.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

var app = builder.Build();

app.UsarServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
