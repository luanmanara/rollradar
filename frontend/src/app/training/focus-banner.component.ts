import { Component, effect, inject, signal } from '@angular/core';
import { AuthService } from '../account/auth.service';
import { REVIEWS, dayLabel } from './training-labels';
import { FocusPoint, FocusReview } from './training.model';
import { TrainingService } from './training.service';

/**
 * "Ponto a corrigir": the point the user wrote at the end of a session, with "trabalhei / em parte / ainda não".
 * Shows nothing to visitors or when there is no open point.
 */
@Component({
  selector: 'app-focus-banner',
  template: `
    @if (focus(); as f) {
      <section class="focus" aria-label="Ponto a corrigir">
        <div class="focus__text">
          <span class="focus__label">Ponto a corrigir · {{ day(f.originDate) }}</span>
          <strong>{{ f.text }}</strong>
        </div>
        <div class="focus__answers" role="group" aria-label="Você trabalhou este ponto?">
          @for (r of reviews; track r.value) {
            <button type="button" class="focus__btn" [disabled]="busy()" (click)="answer(f, r.value)">{{ r.label }}</button>
          }
        </div>
        @if (error()) { <p class="focus__error" role="alert">{{ error() }}</p> }
      </section>
    }
  `,
  styles: `
    .focus {
      display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px 16px;
      margin-bottom: 16px; padding: 14px 16px; border: 1px solid var(--red-100); border-left: 4px solid var(--red);
      border-radius: var(--radius-sm); background: var(--red-50);
    }
    .focus__text { display: grid; gap: 2px; min-width: 0; }
    .focus__label { color: var(--red-ink); font-size: 0.78rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.03em; }
    .focus__answers { display: flex; flex-wrap: wrap; gap: 6px; }
    .focus__btn {
      padding: 6px 12px; border: 1px solid var(--line); border-radius: 999px; background: var(--surface);
      font-size: 0.86rem; font-weight: 600;
      &:hover { border-color: var(--blue); color: var(--blue); }
      &:disabled { opacity: 0.6; cursor: default; }
    }
    .focus__error { flex-basis: 100%; color: var(--red-ink); font-size: 0.85rem; }
  `
})
export class FocusBannerComponent {
  private auth = inject(AuthService);
  private training = inject(TrainingService);

  readonly reviews = REVIEWS;
  readonly day = dayLabel;
  focus = signal<FocusPoint | null>(null);
  busy = signal(false);
  error = signal('');

  constructor() {
    // Asked again when the user signs in or out.
    effect(() => {
      if (!this.auth.user()) {
        this.focus.set(null);
        return;
      }
      this.training.focus().subscribe({ next: f => this.focus.set(f), error: () => this.focus.set(null) });
    });
  }

  answer(focus: FocusPoint, review: FocusReview): void {
    this.busy.set(true);
    this.error.set('');
    this.training.reviewFocus(focus.id, review).subscribe({
      next: () => {
        this.busy.set(false);
        // The next open point (an older one), if any.
        this.training.focus().subscribe({ next: f => this.focus.set(f), error: () => this.focus.set(null) });
      },
      error: () => {
        this.busy.set(false);
        this.error.set('Não foi possível registrar a resposta. Tente de novo.');
      }
    });
  }
}
