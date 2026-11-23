import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Subject } from 'rxjs';
import { DisputeStatus } from '../disputes/disputes-api';

export interface DisputeNotification {
  readonly id: string;
  readonly disputeId: string;
  readonly referenceNumber: string;
  readonly status: DisputeStatus;
  readonly changedAt: string;
  readonly readAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);
  private readonly refreshRequested = new Subject<void>();
  readonly refreshes = this.refreshRequested.asObservable();

  list() {
    return this.http.get<DisputeNotification[]>('/api/dispute-notifications');
  }

  markRead(id: string) {
    return this.http.put<void>(
      '/api/dispute-notifications/' + encodeURIComponent(id) + '/read',
      null,
    );
  }

  refresh() {
    this.refreshRequested.next();
  }
}
