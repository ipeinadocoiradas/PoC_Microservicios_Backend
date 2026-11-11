using System.Text.Json.Serialization;
using DistributedTransactions.ServiceDefaults;
using DistributedTransactions.ServiceDefaults.Http;
using Pedidos.Application;
using Pedidos.Domain;
using Pedidos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AgregarServiceDefaults();

builder.Services
    .AgregarAplicacionPedidos()
    .AgregarInfraestructuraPedidos(builder.Configuration)
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
