import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap, throwError } from 'rxjs';

/** The signed-in user, as GET /api/eu returns it. */
export interface CurrentUser {
  id: string;
  email: string | null;
  nome: string | null;
}

/** The site account (Google sign-in). The session is an HttpOnly cookie, so the API is the only one who knows it. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  /** Null when nobody is signed in (or before {@link load} answers). */
  readonly user = signal<CurrentUser | null>(null);
  /** False until the first answer of /api/eu, so the header doesn't flash "Entrar" for a signed-in user. */
  readonly loaded = signal(false);

  /** Asks the API who is signed in; 401 (or no API) means nobody, not an error. */
  load(): void {
    this.http.get<CurrentUser>('/api/eu').pipe(catchError(() => of(null))).subscribe(user => {
      this.user.set(user);
      this.loaded.set(true);
    });
  }

  /** Address that starts the Google sign-in and comes back to `returnUrl` (a path of this site). */
  loginUrl(returnUrl: string): string {
    return `/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  /** The JSON file with everything the user recorded (a plain link: the browser downloads it with the cookie). */
  readonly exportUrl = '/api/conta/exportar';

  /** Deletes the account and all its data for good; the site asks for confirmation first. */
  deleteAccount(): Observable<void> {
    return this.http.delete<void>('/api/conta').pipe(
      map(() => undefined),
      tap(() => this.user.set(null))
    );
  }

  /** Ends the session; a 401 means it had already ended, which is just as good. */
  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', null).pipe(
      catchError(error => (error.status === 401 ? of(undefined) : throwError(() => error))),
      map(() => undefined),
      tap(() => this.user.set(null))
    );
  }
}
