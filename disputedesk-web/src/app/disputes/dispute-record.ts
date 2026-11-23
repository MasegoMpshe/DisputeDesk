import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DisputeDetails } from './disputes-api';

@Component({
  selector: 'app-dispute-record',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './dispute-record.html',
  styleUrl: './dispute-record.css',
})
export class DisputeRecord {
  readonly dispute = input.required<DisputeDetails>();
  readonly linkToTransaction = input(false);
}
