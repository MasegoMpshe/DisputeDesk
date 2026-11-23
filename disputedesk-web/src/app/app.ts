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
  private readonly auth = inject(AuthApi);
  protected readonly role = this.auth.role;
  protected readonly displayName = this.auth.displayName;
}
