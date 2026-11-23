import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Dispute } from './disputes-api';

@Component({
  selector: 'app-dispute-rows',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './dispute-rows.html',
  styleUrl: './dispute-rows.css',
})
export class DisputeRows {
  readonly items = input.required<Dispute[]>();
  readonly detailBase = input('/disputes');
}
