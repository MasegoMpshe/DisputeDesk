import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export const disputeStatuses = {
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Withdrawn: 'Withdrawn',
} as const;

export type DisputeStatusFilter = keyof typeof disputeStatuses;
export type DisputeStatus = (typeof disputeStatuses)[DisputeStatusFilter];

export interface DisputeFilters {
  readonly status: DisputeStatusFilter | '';
  readonly from: string;
  readonly to: string;
}

export interface Dispute {
  readonly id: string;
  readonly transactionId: string;
  readonly transactionDescription: string;
  readonly referenceNumber: string;
  readonly status: DisputeStatus;
  readonly submittedAt: string;
  readonly amount: number;
  readonly currency: string;
}

export interface DisputePage {
  readonly items: Dispute[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface DisputeReasonOption {
  readonly code: string;
  readonly label: string;
  readonly requiresDetails: boolean;
}

export interface DisputeTimelineEntry {
  readonly status: DisputeStatus;
  readonly actor: 'Customer' | 'Reviewer';
  readonly changedAt: string;
  readonly note: string | null;
}

export interface DisputeDetails {
  readonly summary: Dispute;
  readonly reason: string;
  readonly details: string | null;
  readonly timeline: DisputeTimelineEntry[];
}

@Injectable({ providedIn: 'root' })
export class DisputesApi {
  private readonly http = inject(HttpClient);

  list(filters: DisputeFilters, page: number) {
    const params: Record<string, string> = { page: String(page) };
    if (filters.status) params['status'] = filters.status;
    if (filters.from) params['from'] = new Date(filters.from + 'T00:00:00').toISOString();
    if (filters.to) {
      // The API's upper bound is inclusive, so include the full final local day.
      params['to'] = new Date(filters.to + 'T23:59:59').toISOString().replace('.000Z', '.9999999Z');
    }
    return this.http.get<DisputePage>('/api/disputes/page', { params });
  }

  listReasons() {
    return this.http.get<DisputeReasonOption[]>('/api/disputes/reasons');
  }

  get(id: string) {
    return this.http.get<DisputeDetails>('/api/disputes/' + encodeURIComponent(id));
  }

  submit(transactionId: string, reasonCode: string, details: string | null) {
    return this.http.post<DisputeDetails>('/api/disputes', { transactionId, reasonCode, details });
  }

  withdraw(id: string) {
    return this.http.post<void>('/api/disputes/' + encodeURIComponent(id) + '/withdraw', {});
  }
}
