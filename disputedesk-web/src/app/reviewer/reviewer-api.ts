import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Dispute, DisputeDetails } from '../disputes/disputes-api';

export type ReviewOutcome = 'Approved' | 'Rejected';

@Injectable({ providedIn: 'root' })
export class ReviewerApi {
  private readonly http = inject(HttpClient);
  private readonly disputesPath = '/api/reviewer/disputes';

  list() {
    return this.http.get<Dispute[]>(this.disputesPath);
  }

  get(id: string) {
    return this.http.get<DisputeDetails>(this.path(id));
  }

  startReview(id: string) {
    return this.http.post<void>(this.path(id) + '/review', {});
  }

  decide(id: string, status: ReviewOutcome, note: string) {
    return this.http.post<void>(this.path(id) + '/decision', { status, note });
  }

  private path(id: string) {
    return this.disputesPath + '/' + encodeURIComponent(id);
  }
}
