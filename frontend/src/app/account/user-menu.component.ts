import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthService } from './auth.service';

/** Header corner: "Entrar com Google" for visitors, the user's name and "Sair" once signed in. */
@Component({
  selector: 'app-user-menu',
  template: `
    @if (auth.loaded()) {
      @if (auth.user(); as user) {
        <div class="user">
          <span class="user__name" [title]="user.email ?? ''">{{ user.nome || user.email || 'Minha conta' }}</span>
          <button type="button" class="user__btn" [disabled]="busy()" (click)="logout()">Sair</button>
        </div>
        @if (error()) { <p class="user__error" role="alert">{{ error() }}</p> }
      } @else {
        <a class="user__btn user__btn--login" [href]="loginHref()">Entrar com Google</a>
      }
    }
  `,
  styles: `
    :host { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; }
    .user { display: flex; align-items: center; gap: 10px; }
    .user__name { max-width: 220px; overflow: hidden; color: #fff; font-weight: 600; text-overflow: ellipsis; white-space: nowrap; }
    .user__btn {
      display: inline-flex; align-items: center; padding: 6px 14px;
      border: 1px solid rgba(255, 255, 255, 0.55); border-radius: 999px; background: rgba(255, 255, 255, 0.08);
      color: #fff; font-size: 0.88rem; font-weight: 600; text-decoration: none; transition: background 0.15s;
      &:hover { background: rgba(255, 255, 255, 0.18); }
      &:disabled { opacity: 0.6; cursor: default; }
      &--login { background: var(--surface); border-color: var(--surface); color: var(--navy); }
      &--login:hover { background: var(--blue-50); }
    }
    .user__error { color: var(--red-100); font-size: 0.8rem; }
  `
})
export class UserMenuComponent {
  protected auth = inject(AuthService);
  private router = inject(Router);

  busy = signal(false);
  error = signal('');

  private url = toSignal(
    this.router.events.pipe(filter(e => e instanceof NavigationEnd), map(e => e.urlAfterRedirects)),
    { initialValue: this.router.url }
  );

  /** Comes back to the page the visitor was on. */
  loginHref = computed(() => this.auth.loginUrl(this.url()));

  constructor() {
    this.auth.load();
  }

  logout(): void {
    this.busy.set(true);
    this.error.set('');
    this.auth.logout().subscribe({
      next: () => this.busy.set(false),
      error: () => {
        this.error.set('Não foi possível sair. Tente de novo.');
        this.busy.set(false);
      }
    });
  }
}
