import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../account/auth.service';

/** Shown on the diary pages to visitors: the diary needs an account. */
@Component({
  selector: 'app-login-invite',
  template: `
    <section class="card invite">
      <h1>Diário de treino</h1>
      <p>Registre cada treino, os rolas e o que corrigir no próximo. Seus dados ficam na sua conta.</p>
      <a class="invite__btn" [href]="loginHref()">Entre com Google para usar o diário</a>
    </section>
  `,
  styles: `
    .invite { display: grid; justify-items: start; gap: 12px; padding: 28px 22px; }
    h1 { font-size: 1.4rem; }
    p { color: var(--muted); }
    .invite__btn {
      padding: 10px 18px; border-radius: 999px; background: var(--blue); color: #fff;
      font-weight: 600; text-decoration: none;
      &:hover { background: var(--blue-hover); }
    }
  `
})
export class LoginInviteComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  /** Comes back to the diary page after signing in. */
  loginHref = computed(() => this.auth.loginUrl(this.router.url));
}
