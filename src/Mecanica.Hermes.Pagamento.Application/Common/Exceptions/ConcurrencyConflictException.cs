using System.Diagnostics.CodeAnalysis;

namespace Mecanica.Hermes.Pagamento.Application.Common.Exceptions;

/// <summary>
///     Lançada pela camada de persistência quando uma tentativa de atualização concorrente
///     falha (ex: optimistic locking via <c>Version</c> no documento Mongo). Encapsula a
///     exceção de infra para que Application não dependa do driver MongoDB.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Exception sem lógica — apenas transporte de mensagem.")]
public sealed class ConcurrencyConflictException(string message, Exception? inner = null)
    : Exception(message, inner);
