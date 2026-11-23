import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import localeEnZa from '@angular/common/locales/en-ZA';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { sessionInterceptor } from './auth/session-interceptor';

registerLocaleData(localeEnZa);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([sessionInterceptor])),
    provideRouter(routes),
    { provide: LOCALE_ID, useValue: 'en-ZA' },
  ],
};
