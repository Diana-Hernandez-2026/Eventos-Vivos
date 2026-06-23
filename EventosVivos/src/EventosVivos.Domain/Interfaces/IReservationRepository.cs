using EventosVivos.Domain.Entities;

namespace EventosVivos.Domain.Interfaces;

public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Reservation reservation, CancellationToken ct = default);
    Task<bool> ReservationCodeExistsAsync(string code, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
