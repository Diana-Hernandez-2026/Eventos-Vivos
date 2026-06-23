export interface Venue {
  id: number;
  name: string;
  capacity: number;
  city: string;
}

export interface EventDto {
  id: string;
  title: string;
  description: string;
  venueId: number;
  venueName: string;
  maxCapacity: number;
  startDateTime: string;
  endDateTime: string;
  ticketPrice: number;
  type: string;
  status: string;
  createdAt: string;
}

export interface CreateEventRequest {
  title: string;
  description: string;
  venueId: number;
  maxCapacity: number;
  startDateTime: string;
  endDateTime: string;
  ticketPrice: number;
  type: string;
}

export interface CursorPage<T> {
  items: T[];
  nextCursor: string | null;
  hasNextPage: boolean;
  count: number;
}

export interface Reservation {
  reservationId: string;
  status: string;
}

export interface CreateReservationRequest {
  eventId: string;
  quantity: number;
  buyerName: string;
  buyerEmail: string;
}

export interface ConfirmPaymentResult {
  reservationId: string;
  reservationCode: string;
  status: string;
}

export interface CancelReservationResult {
  reservationId: string;
  status: string;
  isLost: boolean;
  cancelledAt: string;
}

export interface ReservationDetail {
  id: string;
  eventId: string;
  eventTitle: string;
  eventStartDateTime: string;
  status: string;
  quantity: number;
  buyerName: string;
  buyerEmail: string;
  reservationCode: string | null;
  isLost: boolean;
  createdAt: string;
  cancelledAt: string | null;
}

export interface OccupancyReport {
  eventId: string;
  title: string;
  maxCapacity: number;
  confirmedTickets: number;
  availableTickets: number;
  lostTickets: number;
  occupancyPercentage: number;
  totalRevenue: number;
  status: string;
}

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
}
