import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ApiService } from '../../core/services/api.service';
import { ReservationDetail, ConfirmPaymentResult, CancelReservationResult } from '../../core/models/models';

@Component({
  selector: 'app-reservations',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './reservations.component.html',
  styleUrls: ['./reservations.component.css'],
})
export class ReservationsComponent {
  reservationId = '';
  reservation: ReservationDetail | null = null;
  confirmResult: ConfirmPaymentResult | null = null;
  cancelResult: CancelReservationResult | null = null;
  error = '';
  loading = false;

  constructor(private api: ApiService, private translate: TranslateService) {}

  search() {
    if (!this.reservationId.trim()) return;
    this.loading = true;
    this.error = '';
    this.reservation = null;
    this.confirmResult = null;
    this.cancelResult = null;

    this.api.getReservation(this.reservationId.trim()).subscribe({
      next: r => { this.reservation = r; this.loading = false; },
      error: e => {
        this.loading = false;
        this.error = e.status === 404
          ? this.translate.instant('reservations.errors.notFound')
          : e.error?.detail || e.error?.title || this.translate.instant('reservations.errors.searchFailed');
      }
    });
  }

  confirmPayment() {
    this.loading = true;
    this.error = '';
    this.confirmResult = null;

    this.api.confirmReservation(this.reservationId.trim()).subscribe({
      next: r => {
        this.confirmResult = r;
        if (this.reservation) this.reservation.status = r.status;
        this.loading = false;
      },
      error: e => {
        this.loading = false;
        this.error = e.error?.detail || e.error?.title || this.translate.instant('reservations.errors.confirmFailed');
      }
    });
  }

  cancelReservation() {
    this.loading = true;
    this.error = '';
    this.cancelResult = null;

    this.api.cancelReservation(this.reservationId.trim()).subscribe({
      next: r => {
        this.cancelResult = r;
        if (this.reservation) this.reservation.status = 'Cancelada';
        this.loading = false;
      },
      error: e => {
        this.loading = false;
        this.error = e.error?.detail || e.error?.title || this.translate.instant('reservations.errors.cancelFailed');
      }
    });
  }

  get canConfirm(): boolean { return this.reservation?.status === 'PendientePago'; }
  get canCancel():  boolean { return this.reservation?.status === 'Confirmada'; }
}
