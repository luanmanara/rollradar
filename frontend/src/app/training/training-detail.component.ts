import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../account/auth.service';
import { TechniqueSummary } from '../videos/fight-video.model';
import { VideoService } from '../videos/video.service';
import { LoginInviteComponent } from './login-invite.component';
import { RollViewComponent } from './roll-view.component';
import { ATTIRES, FORMATS, dayLabel, durationLabel, label } from './training-labels';
import { TrainingSession } from './training.model';
import { TrainingService } from './training.service';

/**
 * One session, laid out to be read at a glance: the day and its numbers on top, then the technique of the day, each
 * roll as a card (see RollViewComponent) and how the body felt.
 */
@Component({
  selector: 'app-training-detail',
  imports: [RouterLink, LoginInviteComponent, RollViewComponent],
  templateUrl: './training-detail.component.html',
  styleUrl: './training-detail.component.scss'
})
export class TrainingDetailComponent {
  protected auth = inject(AuthService);
  private training = inject(TrainingService);
  private router = inject(Router);

  readonly day = dayLabel;
  readonly dots = [1, 2, 3, 4, 5];

  private route = inject(ActivatedRoute);
  private id = toSignal(this.route.paramMap.pipe(map(p => p.get('id') ?? '')), { initialValue: '' });
  /** Arrived here right after recording the session (?salvo=1). */
  justSaved = toSignal(this.route.queryParamMap.pipe(map(p => p.has('salvo'))), { initialValue: false });
  private catalog = toSignal(inject(VideoService).techniques().pipe(catchError(() => of([] as TechniqueSummary[]))), { initialValue: [] });

  session = signal<TrainingSession | null>(null);
  error = signal('');
  confirming = signal(false);
  deleting = signal(false);

  /** "Aula", "Com kimono", "1h30": the pills under the day. */
  tags = computed(() => {
    const s = this.session();
    return s ? [label(FORMATS, s.format), label(ATTIRES, s.attire), durationLabel(s.durationMinutes)] : [];
  });

  /** The numbers of the session: rolls, submissions each way and, when told, the energy. */
  stats = computed(() => {
    const s = this.session();
    if (!s) return [];
    const finishes = s.rolls.flatMap(r => r.finishes);
    const mine = finishes.filter(f => f.byMe).length;
    const stats = [
      { label: s.rolls.length === 1 ? 'Rola' : 'Rolas', value: String(s.rolls.length), tone: '' },
      { label: 'Finalizei', value: String(mine), tone: mine ? 'win' : '' },
      { label: 'Fui finalizado', value: String(finishes.length - mine), tone: finishes.length - mine ? 'loss' : '' }
    ];
    if (s.energy !== null) stats.push({ label: 'Energia', value: `${s.energy}/5`, tone: '' });
    return stats;
  });

  scales = computed(() => {
    const s = this.session();
    if (!s) return [];
    return [
      { label: 'Sono', value: s.sleep, tone: 'good' },
      { label: 'Dor', value: s.pain, tone: 'bad' },
      { label: 'Energia', value: s.energy, tone: 'good' }
    ].filter(x => x.value !== null);
  });

  constructor() {
    effect(() => {
      const id = this.id();
      if (!this.auth.user() || !id) return;
      untracked(() => this.training.get(id).subscribe({
        next: s => this.session.set(s),
        error: () => this.error.set('Treino não encontrado.')
      }));
    });
  }

  techniqueName(id: string | null): string {
    const t = this.catalog().find(c => c.id === id);
    return t ? t.portuguese || t.english : (id ?? '');
  }

  remove(): void {
    const s = this.session();
    if (!s) return;
    this.deleting.set(true);
    this.training.remove(s.id).subscribe({
      next: () => this.router.navigate(['/treinos']),
      error: () => {
        this.deleting.set(false);
        this.error.set('Não foi possível apagar o treino. Tente de novo.');
      }
    });
  }
}
