import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-reservations',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './reservations.component.html',
  styleUrls: ['./reservations.component.css'],
})
export class ReservationsComponent {
  reservationId = '';
  result: any = null;
  error = '';
  loading = false;

  constructor(private api: ApiService) { }

  confirmPayment() {
    this.loading = true; this.error = ''; this.result = null;
    this.api.confirmReservation(this.reservationId).subscribe({
      next: r => { this.result = r; this.loading = false; },
      error: e => { this.error = e.error?.detail || e.error?.title || 'Error'; this.loading = false; }
    });
  }

  cancelReservation() {
    this.loading = true; this.error = ''; this.result = null;
    this.api.cancelReservation(this.reservationId).subscribe({
      next: r => { this.result = r; this.loading = false; },
      error: e => { this.error = e.error?.detail || e.error?.title || 'Error'; this.loading = false; }
    });
  }
}
