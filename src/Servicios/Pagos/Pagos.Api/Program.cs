using System.Text.Json.Serialization;
using DistributedTransactions.ServiceDefaults;
using DistributedTransactions.ServiceDefaults.Http;
using Pagos.Application;
using Pagos.Domain;
using Pagos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AgregarServiceDefaults();

builder.Services
    .AgregarAplicacionPagos()
    .AgregarInfraestructuraPagos(builder.Configuration)
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
