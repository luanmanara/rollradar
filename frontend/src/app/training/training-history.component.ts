import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import { AuthService } from '../account/auth.service';
import { LoginInviteComponent } from './login-invite.component';
import { ATTIRES, FORMATS, dayLabel, durationLabel, label, weekLabel, weekStart } from './training-labels';
import { rollsLine, sessionLine } from './training-summary';
import { TrainingSyncService } from './training-sync.service';
import { Attire, Partner, TrainingFilter, TrainingFormat, TrainingSession, TrainingSessionInput } from './training.model';
import { TrainingService } from './training.service';

const PAGE_SIZE = 20;

/** The user's sessions by week, newest first, as cards (day, pills, numbers), with filters and a text search. */
@Component({
  selector: 'app-training-history',
  imports: [RouterLink, NgTemplateOutlet, LoginInviteComponent],
  templateUrl: './training-history.component.html',
  styleUrl: './training-history.component.scss'
})
export class TrainingHistoryComponent {
  protected auth = inject(AuthService);
  protected sync = inject(TrainingSyncService);
  private training = inject(TrainingService);

  readonly attires = ATTIRES;
  readonly formats = FORMATS;
  readonly day = dayLabel;
  readonly line = sessionLine;
  readonly rolls = rollsLine;

  attire = signal<Attire | ''>('');
  format = signal<TrainingFormat | ''>('');
  partnerId = signal<number | null>(null);
  query = signal('');
  private debouncedQuery = toSignal(toObservable(this.query).pipe(debounceTime(300)), { initialValue: '' });

  partners = signal<Partner[]>([]);
  sessions = signal<TrainingSession[]>([]);
  total = signal(0);
  page = signal(1);
  loading = signal(false);
  error = signal('');

  private filter = computed<TrainingFilter>(() => ({
    attire: this.attire(), format: this.format(), partnerId: this.partnerId(), q: this.debouncedQuery(), pageSize: PAGE_SIZE
  }));

  filtered = computed(() => !!(this.attire() || this.format() || this.partnerId() || this.query().trim()));

  /** Sessions grouped by the Monday of their week. */
  weeks = computed(() => {
    const weeks: { label: string; sessions: TrainingSession[] }[] = [];
    let current = '';
    for (const s of this.sessions()) {
      const monday = weekStart(s.date);
      if (monday !== current) weeks.push({ label: weekLabel(monday), sessions: [] });
      current = monday;
      weeks[weeks.length - 1].sessions.push(s);
    }
    return weeks;
  });

  constructor() {
    effect(() => {
      const filter = this.filter();
      if (!this.auth.user()) return;
      untracked(() => this.load(filter, 1));
    });
    effect(() => {
      if (!this.auth.user()) return;
      untracked(() => this.training.partners().subscribe({ next: p => this.partners.set(p), error: () => {} }));
    });
    // A session recorded offline shows up as soon as the API confirms it.
    effect(() => {
      if (this.sync.lastSaved() && this.auth.user()) untracked(() => this.load(this.filter(), 1));
    });
  }

  /** "Aula", "Com kimono", "1h30". */
  tags(s: TrainingSession | TrainingSessionInput): string[] {
    return [label(FORMATS, s.format), label(ATTIRES, s.attire), durationLabel(s.durationMinutes)];
  }

  counts(s: TrainingSession | TrainingSessionInput): { rolls: number; mine: number; theirs: number } {
    const finishes = s.rolls.flatMap(r => r.finishes);
    const mine = finishes.filter(f => f.byMe).length;
    return { rolls: s.rolls.length, mine, theirs: finishes.length - mine };
  }

  more(): void {
    this.load(this.filter(), this.page() + 1);
  }

  clear(): void {
    this.attire.set('');
    this.format.set('');
    this.partnerId.set(null);
    this.query.set('');
  }

  private load(filter: TrainingFilter, page: number): void {
    this.loading.set(true);
    this.error.set('');
    this.training.list({ ...filter, page }).subscribe({
      next: result => {
        this.sessions.set(page === 1 ? result.items : [...this.sessions(), ...result.items]);
        this.total.set(result.total);
        this.page.set(page);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Não foi possível carregar o histórico.');
      }
    });
  }
}
