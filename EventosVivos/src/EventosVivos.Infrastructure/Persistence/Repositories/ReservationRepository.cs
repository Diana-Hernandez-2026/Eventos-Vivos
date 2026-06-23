using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence.Repositories;

public class ReservationRepository(AppDbContext db) : IReservationRepository
{
    public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Reservations.Include(r => r.Event).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(Reservation reservation, CancellationToken ct) =>
        await db.Reservations.AddAsync(reservation, ct);

    public Task<bool> ReservationCodeExistsAsync(string code, CancellationToken ct) =>
        db.Reservations.AnyAsync(r => r.ReservationCode == code, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
