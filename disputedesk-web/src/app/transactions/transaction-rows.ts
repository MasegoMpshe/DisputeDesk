import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { Transaction } from './transactions-api';

@Component({
  selector: 'app-transaction-rows',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './transaction-rows.html',
  styleUrl: './transaction-rows.css',
})
export class TransactionRows {
  readonly transactions = input.required<readonly Transaction[]>();
}
