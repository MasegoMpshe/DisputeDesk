import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export interface Dispute {
  readonly id: string;
  readonly transactionId: string;
  readonly transactionDescription: string;
  readonly referenceNumber: string;
  readonly status: string;
  readonly submittedAt: string;
}

export interface DisputeReasonOption {
  readonly code: string;
  readonly label: string;
  readonly requiresDetails: boolean;
}

export interface DisputeDetails {
  readonly summary: Dispute;
  readonly reason: string;
  readonly details: string | null;
}

@Injectable({ providedIn: 'root' })
export class DisputesApi {
  private readonly http = inject(HttpClient);

  list() {
    return this.http.get<Dispute[]>('/api/disputes');
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
}
