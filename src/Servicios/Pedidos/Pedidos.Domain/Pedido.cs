namespace Pedidos.Domain;

/// <summary>
/// Raíz de agregado del pedido.
/// </summary>
/// <remarks>
/// Encapsula sus invariantes: solo puede crearse mediante <see cref="Crear"/> y su estado solo cambia
/// a través de métodos de dominio que validan la transición. Un pedido finalizado es inmutable.
/// </remarks>
public sealed class Pedido
{
    private const int LongitudMaximaMotivo = 500;

    private readonly List<LineaPedido> _lineas = [];

    private Pedido()
    {
        // Requerido por EF Core.
    }

    private Pedido(PedidoId id, Guid clienteId, string moneda, IEnumerable<LineaPedido> lineas, DateTimeOffset fechaCreacionUtc)
    {
        Id = id;
        ClienteId = clienteId;
        Moneda = moneda;
        Estado = EstadoPedido.Pendiente;
        FechaCreacionUtc = fechaCreacionUtc;
        _lineas.AddRange(lineas);
    }

    public PedidoId Id { get; private set; }

    public Guid ClienteId { get; private set; }

    /// <summary>Código ISO 4217 en mayúsculas.</summary>
    public string Moneda { get; private set; } = string.Empty;

    public EstadoPedido Estado { get; private set; }

    /// <summary>Causa de la cancelación o del fallo; nulo si el pedido está pendiente o completado.</summary>
    public string? MotivoFinalizacion { get; private set; }

    public DateTimeOffset FechaCreacionUtc { get; private set; }

    public DateTimeOffset? FechaFinalizacionUtc { get; private set; }

    public IReadOnlyCollection<LineaPedido> Lineas => _lineas.AsReadOnly();

    public decimal Total => _lineas.Sum(linea => linea.Subtotal);

    public bool EstaFinalizado => Estado is not EstadoPedido.Pendiente;

    /// <summary>Registra un pedido nuevo en estado <see cref="EstadoPedido.Pendiente"/>.</summary>
    /// <exception cref="ExcepcionDominio">Si el cliente, la moneda o las líneas no son válidos.</exception>
    public static Pedido Crear(Guid clienteId, string moneda, IReadOnlyCollection<LineaPedido> lineas, DateTimeOffset fechaCreacionUtc)
    {
        ArgumentNullException.ThrowIfNull(lineas);

        if (clienteId == Guid.Empty)
        {
            throw new ExcepcionDominio("El pedido debe pertenecer a un cliente.");
        }

        if (lineas.Count == 0)
        {
            throw new ExcepcionDominio("El pedido debe contener al menos una línea.");
        }

        if (lineas.DistinctBy(linea => linea.ProductoId).Count() != lineas.Count)
        {
            throw new ExcepcionDominio("Un producto no puede aparecer en varias líneas del mismo pedido.");
        }

        return new Pedido(PedidoId.Nuevo(), clienteId, NormalizarMoneda(moneda), lineas, fechaCreacionUtc);
    }

    /// <summary>Marca el pedido como completado (stock reservado y pago cobrado).</summary>
    public void Completar(DateTimeOffset fechaUtc)
    {
        AsegurarPendiente(EstadoPedido.Completado);
        Finalizar(EstadoPedido.Completado, motivo: null, fechaUtc);
    }

    /// <summary>Cancela el pedido por una causa de negocio.</summary>
    public void Cancelar(string motivo, DateTimeOffset fechaUtc)
    {
        AsegurarPendiente(EstadoPedido.Cancelado);
        Finalizar(EstadoPedido.Cancelado, ValidarMotivo(motivo), fechaUtc);
    }

    /// <summary>Marca el pedido como fallido por un error técnico tras ejecutar las compensaciones.</summary>
    public void MarcarFallido(string motivo, DateTimeOffset fechaUtc)
    {
        AsegurarPendiente(EstadoPedido.Fallido);
        Finalizar(EstadoPedido.Fallido, ValidarMotivo(motivo), fechaUtc);
    }

    private void Finalizar(EstadoPedido estadoFinal, string? motivo, DateTimeOffset fechaUtc)
    {
        Estado = estadoFinal;
        MotivoFinalizacion = motivo;
        FechaFinalizacionUtc = fechaUtc;
    }

    private void AsegurarPendiente(EstadoPedido estadoDestino)
    {
        if (EstaFinalizado)
        {
            throw new ExcepcionDominio($"El pedido {Id} ya está {Estado} y no puede pasar a {estadoDestino}.");
        }
    }

    private static string ValidarMotivo(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ExcepcionDominio("Debe indicarse el motivo de la finalización.");
        }

        var motivoLimpio = motivo.Trim();
        return motivoLimpio.Length <= LongitudMaximaMotivo ? motivoLimpio : motivoLimpio[..LongitudMaximaMotivo];
    }

    private static string NormalizarMoneda(string moneda)
    {
        var normalizada = moneda?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalizada.Length != 3 || !normalizada.All(char.IsAsciiLetterUpper))
        {
            throw new ExcepcionDominio("La moneda debe ser un código ISO 4217 de tres letras.");
        }

        return normalizada;
    }
}
