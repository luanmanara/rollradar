import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { LoginInviteComponent } from '../training/login-invite.component';
import { AuthService } from './auth.service';

/** "Minha conta": who is signed in, download all the data, delete the account. */
@Component({
  selector: 'app-account-page',
  imports: [LoginInviteComponent],
  template: `
    @if (!auth.loaded()) {
      <p class="card loading" aria-busy="true">Carregando…</p>
    } @else if (auth.user(); as user) {
      <section class="card account">
        <h1>Minha conta</h1>
        <p>{{ user.nome }} <span class="muted">{{ user.email }}</span></p>

        <div class="block">
          <h2>Seus dados</h2>
          <p class="muted">Um arquivo JSON com todos os treinos, rolas, parceiros, posições e pontos a corrigir.</p>
          <a class="btn" [href]="auth.exportUrl" download>Exportar meus dados</a>
        </div>

        <div class="block block--danger">
          <h2>Apagar conta</h2>
          <p class="muted">Apaga a conta e todo o diário de treino. Não dá para desfazer; exporte os dados antes se quiser guardá-los.</p>
          <label class="check">
            <input type="checkbox" [checked]="understood()" (change)="understood.set($any($event.target).checked)">
            Entendo que tudo será apagado
          </label>
          <button type="button" class="btn btn--danger" [disabled]="!understood() || busy()" (click)="remove()">Apagar minha conta</button>
          @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        </div>
      </section>
    } @else {
      <app-login-invite />
    }
  `,
  styles: `
    :host { display: block; }
    .loading { padding: 20px; color: var(--muted); }
    .account { display: grid; gap: 20px; padding: 20px 16px; }
    h1 { font-size: 1.4rem; }
    .muted { color: var(--muted); font-size: 0.9rem; }
    .block { display: grid; justify-items: start; gap: 8px; padding-top: 16px; border-top: 1px solid var(--line); }
    h2 { font-size: 1.05rem; }
    .block--danger h2 { color: var(--red-ink); }
    .btn {
      display: inline-flex; align-items: center; min-height: 40px; padding: 0 16px;
      border: 1.5px solid var(--line); border-radius: 999px; background: var(--surface); color: var(--ink);
      font-weight: 600; text-decoration: none;
      &:hover { border-color: var(--blue); color: var(--blue); }
      &--danger { border-color: var(--red); color: var(--red-ink); }
      &--danger:hover { background: var(--red-50); color: var(--red-ink); }
      &:disabled { opacity: 0.55; cursor: default; }
    }
    .error { color: var(--red-ink); }
  `
})
export class AccountPageComponent {
  protected auth = inject(AuthService);
  private router = inject(Router);

  understood = signal(false);
  busy = signal(false);
  error = signal('');

  remove(): void {
    this.busy.set(true);
    this.error.set('');
    this.auth.deleteAccount().subscribe({
      next: () => this.router.navigate(['/']),
      error: () => {
        this.busy.set(false);
        this.error.set('Não foi possível apagar a conta. Tente de novo.');
      }
    });
  }
}
