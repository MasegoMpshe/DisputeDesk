import { DatePipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { EMPTY, catchError, finalize, startWith, switchMap, tap } from 'rxjs';
import { DisputeNotification, NotificationsApi } from './notifications-api';

@Component({
  selector: 'app-notification-bell',
  imports: [DatePipe],
  templateUrl: './notification-bell.html',
  styleUrl: './notification-bell.css',
  host: {
    '(document:click)': 'closeOnOutsideClick($event)',
    '(keydown.escape)': 'closeWithKeyboard($event)',
  },
})
export class NotificationBell {
  private readonly api = inject(NotificationsApi);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  protected readonly notifications = signal<DisputeNotification[]>([]);
  protected readonly unread = computed(
    () => this.notifications().filter((item) => !item.readAt).length,
  );
  protected readonly expanded = signal(false);
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly opening = signal(false);
  protected readonly openFailed = signal(false);

  constructor() {
    this.api.refreshes
      .pipe(
        startWith(undefined),
        switchMap(() => {
          this.loading.set(true);
          this.loadFailed.set(false);
          return this.api.list().pipe(
            catchError(() => {
              this.loadFailed.set(true);
              return EMPTY;
            }),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((notifications) => this.notifications.set(notifications));
  }

  protected toggle() {
    this.expanded.update((open) => !open);
    if (this.expanded()) {
      this.openFailed.set(false);
      this.reload();
    }
  }

  protected reload() {
    this.api.refresh();
  }

  protected open(item: DisputeNotification) {
    if (this.opening()) return;
    if (item.readAt) {
      this.navigate(item);
      return;
    }

    this.openFailed.set(false);
    this.opening.set(true);
    this.api
      .markRead(item.id)
      .pipe(
        tap(() => {
          const readAt = new Date().toISOString();
          this.notifications.update((items) =>
            items.map((notification) =>
              notification.id === item.id ? { ...notification, readAt } : notification,
            ),
          );
          this.navigate(item);
          this.reload();
        }),
        finalize(() => this.opening.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        error: () => {
          this.openFailed.set(true);
        },
      });
  }

  protected closeWithKeyboard(event: Event) {
    if (!this.expanded()) return;
    event.stopPropagation();
    this.expanded.set(false);
    this.trigger().nativeElement.focus();
  }

  protected closeOnOutsideClick(event: Event) {
    if (this.expanded() && !this.host.nativeElement.contains(event.target as Node)) {
      this.expanded.set(false);
    }
  }

  private navigate(item: DisputeNotification) {
    this.expanded.set(false);
    this.trigger().nativeElement.focus();
    void this.router.navigate(['/disputes', item.disputeId], {
      queryParams: { notification: item.id },
    });
  }
}
