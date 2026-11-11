// Modelo de aplicación distribuida para desarrollo local.
//
// Aspire y Docker Compose comparten el mismo contrato de configuración: ambos inyectan
// ConnectionStrings__PedidosDb / AlmacenDb / PagosDb y ConnectionStrings__rabbitmq. Los servicios no saben
// quién los arranca, por lo que no hay lógica duplicada: solo la declaración de la infraestructura.
//
// Las contraseñas de SQL Server y RabbitMQ las genera Aspire y las guarda en los user-secrets del AppHost
// (nunca en el repositorio). Los contenedores son persistentes: sobreviven a reinicios del AppHost, lo que
// permite comprobar que la Saga reanuda su trabajo tras detener y arrancar los servicios.

var builder = DistributedApplication.CreateBuilder(args);

var sqlServer = builder.AddSqlServer("sqlserver")
    .WithImageTag("2022-latest")
    .WithDataVolume("distributed-transactions-sql")
    .WithLifetime(ContainerLifetime.Persistent);

var pedidosDb = sqlServer.AddDatabase("PedidosDb");
var almacenDb = sqlServer.AddDatabase("AlmacenDb");
var pagosDb = sqlServer.AddDatabase("PagosDb");

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithDataVolume("distributed-transactions-rabbitmq")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.Almacen_Api>("almacen-api")
    .WithReference(almacenDb)
    .WithReference(rabbitMq)
    .WaitFor(almacenDb)
    .WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Pagos_Api>("pagos-api")
    .WithReference(pagosDb)
    .WithReference(rabbitMq)
    .WaitFor(pagosDb)
    .WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Pedidos_Api>("pedidos-api")
    .WithReference(pedidosDb)
    .WithReference(rabbitMq)
    .WaitFor(pedidosDb)
    .WaitFor(rabbitMq)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
