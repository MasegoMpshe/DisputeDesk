import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthApi } from './auth/auth-api';
import { NotificationBell } from './notifications/notification-bell';

@Component({
  imports: [RouterOutlet, NotificationBell],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App {
  protected readonly role = inject(AuthApi).role;
}
