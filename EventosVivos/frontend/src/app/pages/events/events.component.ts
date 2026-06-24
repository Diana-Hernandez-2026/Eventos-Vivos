import { Component, OnInit } from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { EventDto, Venue, CursorPage } from '../../core/models/models';

@Component({
  selector: 'app-events',
  standalone: true,
  imports: [CommonModule, FormsModule, CurrencyPipe, DatePipe, TranslatePipe],
  templateUrl: './events.component.html',
  styleUrl: './events.component.css'
})
export class EventsComponent implements OnInit {
  events: EventDto[] = [];
  venues: Venue[] = [];
  page: CursorPage<EventDto> | null = null;
  loading = false;
  error = '';
  showCreate = false;
  creating = false;
  createError = '';
  createSuccess = '';
  selectedReport: any = null;
  selectedEvent: EventDto | null = null;
  lastReservationId = '';
  reserving = false;
  confirming = false;
  reservationError = '';
  reservationSuccess = '';
  paymentResult: any = null;

  filters = { titleSearch: '', type: '', status: '', venueId: '', startFrom: '', startTo: '' };
  newEvent = { title: '', description: '', venueId: 1, maxCapacity: 50, ticketPrice: 0, startDateTime: '', endDateTime: '', type: 'Conferencia' };
  reservation = { buyerName: '', buyerEmail: '', quantity: 1 };

  constructor(
    public api: ApiService,
    public auth: AuthService,
    public router: Router,
    private translate: TranslateService
  ) {}

  ngOnInit() {
    this.api.getVenues().subscribe({ next: v => this.venues = v, error: () => {} });
    this.loadEvents();
  }

  loadEvents(append = false) {
    this.loading = true;
    this.error = '';
    const f: any = {};
    if (this.filters.titleSearch) f.titleSearch = this.filters.titleSearch;
    if (this.filters.type)        f.type        = this.filters.type;
    if (this.filters.status)      f.status      = this.filters.status;
    if (this.filters.venueId)     f.venueId     = this.filters.venueId;
    if (this.filters.startFrom)   f.startFrom   = this.filters.startFrom;
    if (this.filters.startTo)     f.startTo     = this.filters.startTo;
    if (append && this.page?.nextCursor) f.cursor = this.page.nextCursor;

    this.api.getEvents(f).subscribe({
      next: (p) => {
        this.page   = p;
        this.events = append ? [...this.events, ...p.items] : p.items;
        this.loading = false;
      },
      error: (e) => {
        this.error   = e.message || this.translate.instant('events.loadError');
        this.loading = false;
      }
    });
  }

  loadMore()    { this.loadEvents(true); }
  clearFilters() {
    this.filters = { titleSearch: '', type: '', status: '', venueId: '', startFrom: '', startTo: '' };
    this.loadEvents();
  }

  createEvent() {
    this.creating = true; this.createError = ''; this.createSuccess = '';
    const payload = {
      ...this.newEvent,
      startDateTime: new Date(this.newEvent.startDateTime).toISOString(),
      endDateTime:   new Date(this.newEvent.endDateTime).toISOString()
    };
    this.api.createEvent(payload).subscribe({
      next: (r) => {
        this.createSuccess = this.translate.instant('events.form.success', { title: r.title });
        this.creating   = false;
        this.showCreate = false;
        this.loadEvents();
      },
      error: (e) => {
        this.createError = e.error?.detail || JSON.stringify(e.error?.extensions?.errors || e.message);
        this.creating = false;
      }
    });
  }

  viewReport(event: EventDto) {
    this.api.getOccupancyReport(event.id).subscribe({
      next: r => this.selectedReport = r,
      error: e => alert(e.error?.detail || 'Error')
    });
  }

  openReservation(event: EventDto) {
    this.selectedEvent     = event;
    this.reservation       = { buyerName: '', buyerEmail: '', quantity: 1 };
    this.reservationError  = '';
    this.reservationSuccess = '';
    this.paymentResult     = null;
  }

  submitReservation() {
    if (!this.selectedEvent) return;
    this.reserving = true; this.reservationError = '';
    this.api.createReservation({ eventId: this.selectedEvent.id, ...this.reservation }).subscribe({
      next: (r) => {
        this.lastReservationId  = r.reservationId;
        this.reservationSuccess = this.translate.instant('events.reservation.success');
        this.reserving = false;
      },
      error: (e) => {
        this.reservationError = e.error?.detail || JSON.stringify(e.error?.extensions?.errors || e.message);
        this.reserving = false;
      }
    });
  }

  confirmPayment() {
    this.confirming = true;
    this.api.confirmReservation(this.lastReservationId).subscribe({
      next: (r) => { this.paymentResult = r; this.confirming = false; },
      error: (e) => {
        this.reservationError = e.error?.detail || this.translate.instant('reservations.errors.confirmFailed');
        this.confirming = false;
      }
    });
  }
}
