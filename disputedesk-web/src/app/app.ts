import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthApi } from './auth/auth-api';
import { SignOut } from './auth/sign-out';

@Component({
  imports: [RouterOutlet, SignOut],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App {
  protected readonly role = inject(AuthApi).role;
}
