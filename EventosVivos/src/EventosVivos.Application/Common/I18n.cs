using System.Globalization;

namespace EventosVivos.Application.Common;

/// <summary>
/// Provides localized strings for application-layer messages.
/// Reads CultureInfo.CurrentCulture which is set per-request by the
/// RequestLocalizationMiddleware (via Accept-Language header).
/// Supports: en (default), es, pt.
/// </summary>
public static class I18n
{
    private static string Lang => CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

    // ── Exception titles (used by ExceptionHandlingMiddleware) ───────────────

    public static string TitleValidation => Lang switch {
        "es" => "Error de Validación",
        "pt" => "Erro de Validação",
        _    => "Validation Error"
    };
    public static string TitleNotFound => Lang switch {
        "es" => "No Encontrado",
        "pt" => "Não Encontrado",
        _    => "Not Found"
    };
    public static string TitleDomain => Lang switch {
        "es" => "Violación de Regla de Negocio",
        "pt" => "Violação de Regra de Negócio",
        _    => "Business Rule Violation"
    };
    public static string TitleConflict => Lang switch {
        "es" => "Conflicto",
        "pt" => "Conflito",
        _    => "Conflict"
    };
    public static string TitleUnauthorized => Lang switch {
        "es" => "No Autorizado",
        "pt" => "Não Autorizado",
        _    => "Unauthorized"
    };
    public static string TitleInternal => Lang switch {
        "es" => "Error Interno del Servidor",
        "pt" => "Erro Interno do Servidor",
        _    => "Internal Server Error"
    };

    // ── Domain messages (RN = business rule number) ──────────────────────────

    public static string Rn01(int maxCapacity, int venueCapacity) => Lang switch {
        "es" => $"La capacidad máxima ({maxCapacity}) supera la capacidad del venue ({venueCapacity}).",
        "pt" => $"A capacidade máxima ({maxCapacity}) excede a capacidade do venue ({venueCapacity}).",
        _    => $"MaxCapacity ({maxCapacity}) exceeds venue capacity ({venueCapacity})."
    };

    public static string Rn02 => Lang switch {
        "es" => "Ya existe un evento activo en este venue que se superpone con el horario solicitado.",
        "pt" => "Já existe um evento ativo neste venue que se sobrepõe ao horário solicitado.",
        _    => "Another active event at this venue overlaps the requested time slot."
    };

    public static string Rn03 => Lang switch {
        "es" => "Los eventos en fin de semana no pueden iniciar después de las 22:00.",
        "pt" => "Os eventos de fim de semana não podem começar depois das 22:00.",
        _    => "Weekend events cannot start after 22:00."
    };

    public static string Rn04Status(string status) => Lang switch {
        "es" => $"No se pueden reservar entradas para un evento con estado '{status}'.",
        "pt" => $"Não é possível reservar ingressos para um evento com o estado '{status}'.",
        _    => $"Cannot reserve tickets for an event with status '{status}'."
    };

    public static string Rn04Time => Lang switch {
        "es" => "No se permiten reservas para eventos que inician en menos de 1 hora.",
        "pt" => "Não são permitidas reservas para eventos que começam em menos de 1 hora.",
        _    => "Reservations are not allowed for events starting in less than 1 hour."
    };

    public static string Rn05Max(int maxAllowed) => Lang switch {
        "es" => $"Máximo {maxAllowed} entradas permitidas por transacción para este evento.",
        "pt" => $"Máximo de {maxAllowed} ingressos permitidos por transação para este evento.",
        _    => $"Maximum {maxAllowed} tickets allowed per transaction for this event."
    };

    public static string Rn05Available(int available) => Lang switch {
        "es" => $"Solo hay {available} entradas disponibles.",
        "pt" => $"Apenas {available} ingressos disponíveis.",
        _    => $"Only {available} tickets available."
    };

    public static string AlreadyConfirmed => Lang switch {
        "es" => "La reserva ya está confirmada.",
        "pt" => "A reserva já está confirmada.",
        _    => "Reservation is already confirmed."
    };

    public static string CannotConfirmCancelled => Lang switch {
        "es" => "No se puede confirmar una reserva cancelada.",
        "pt" => "Não é possível confirmar uma reserva cancelada.",
        _    => "Cannot confirm a cancelled reservation."
    };

    public static string AlreadyCancelled => Lang switch {
        "es" => "La reserva ya está cancelada.",
        "pt" => "A reserva já está cancelada.",
        _    => "Reservation is already cancelled."
    };

    public static string CannotCancelPending => Lang switch {
        "es" => "No se puede cancelar una reserva con pago pendiente. Solo las reservas confirmadas pueden cancelarse.",
        "pt" => "Não é possível cancelar uma reserva com pagamento pendente. Somente reservas confirmadas podem ser canceladas.",
        _    => "Cannot cancel a reservation with pending payment status. Only confirmed reservations can be cancelled."
    };

    // ── Validator custom messages ────────────────────────────────────────────

    public static string StartDateFuture => Lang switch {
        "es" => "La fecha de inicio debe ser en el futuro.",
        "pt" => "A data de início deve ser no futuro.",
        _    => "Start date must be in the future."
    };

    public static string EndDateAfterStart => Lang switch {
        "es" => "La fecha de fin debe ser posterior a la fecha de inicio.",
        "pt" => "A data de término deve ser posterior à data de início.",
        _    => "End date must be after start date."
    };

    // ── Auth messages ────────────────────────────────────────────────────────

    public static string AuthExchangeFailed => Lang switch {
        "es" => "No se pudo canjear el código de autorización de Microsoft.",
        "pt" => "Não foi possível trocar o código de autorização da Microsoft.",
        _    => "Could not exchange Microsoft authorization code."
    };

    public static string AuthInvalidRefreshToken => Lang switch {
        "es" => "Token de actualización inválido o expirado.",
        "pt" => "Token de atualização inválido ou expirado.",
        _    => "Invalid or expired refresh token."
    };

    // ── Not found messages ───────────────────────────────────────────────────

    public static string NotFoundEntity(string entity, object id) => Lang switch {
        "es" => $"{entity} '{id}' no fue encontrado.",
        "pt" => $"{entity} '{id}' não foi encontrado.",
        _    => $"{entity} with key '{id}' was not found."
    };
}
