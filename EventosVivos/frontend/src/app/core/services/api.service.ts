import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import {
  Venue, EventDto, CreateEventRequest, CursorPage,
  CreateReservationRequest, Reservation, ReservationDetail,
  ConfirmPaymentResult, CancelReservationResult, OccupancyReport
} from '../models/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private base = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // Venues
  getVenues() {
    return this.http.get<Venue[]>(`${this.base}/venues`);
  }

  // Events
  getEvents(filters?: {
    type?: string; startFrom?: string; startTo?: string;
    venueId?: number; status?: string; titleSearch?: string;
    cursor?: string; limit?: number;
  }) {
    let params = new HttpParams();
    if (filters) {
      Object.entries(filters).forEach(([k, v]) => {
        if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
      });
    }
    return this.http.get<CursorPage<EventDto>>(`${this.base}/events`, { params });
  }

  createEvent(data: CreateEventRequest) {
    return this.http.post<{ id: string; title: string; status: string }>(`${this.base}/events`, data);
  }

  getOccupancyReport(eventId: string) {
    return this.http.get<OccupancyReport>(`${this.base}/events/${eventId}/report`);
  }

  // Reservations
  getReservation(reservationId: string) {
    return this.http.get<ReservationDetail>(`${this.base}/reservations/${reservationId}`);
  }

  createReservation(data: CreateReservationRequest) {
    return this.http.post<Reservation>(`${this.base}/reservations`, data);
  }

  confirmReservation(reservationId: string) {
    return this.http.post<ConfirmPaymentResult>(`${this.base}/reservations/${reservationId}/confirm`, {});
  }

  cancelReservation(reservationId: string) {
    return this.http.post<CancelReservationResult>(`${this.base}/reservations/${reservationId}/cancel`, {});
  }
}
