import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../account/auth.service';
import { TechniqueSummary } from '../videos/fight-video.model';
import { TechniquePickerComponent } from '../videos/technique-picker.component';
import { VideoService } from '../videos/video.service';
import { FocusBannerComponent } from './focus-banner.component';
import { LoginInviteComponent } from './login-invite.component';
import { PositionChip, RollCardComponent, chipLabel } from './roll-card.component';
import { ATTIRES, FINISH_CATEGORIES, FORMATS, today } from './training-labels';
import { TrainingSyncService } from './training-sync.service';
import { FieldErrors, RecentPositions, RollInput, TrainingSession, TrainingSessionInput } from './training.model';
import { TrainingService } from './training.service';

/** Attire, format and length of a new session when there is no earlier one to copy them from. */
const DEFAULTS = { attire: 'gi', format: 'aula', durationMinutes: 90 } as const;

export function newSession(from: Partial<Pick<TrainingSessionInput, 'attire' | 'format' | 'durationMinutes'>> = {}): TrainingSessionInput {
  return {
    id: crypto.randomUUID(),
    date: today(),
    attire: from.attire ?? DEFAULTS.attire,
    format: from.format ?? DEFAULTS.format,
    durationMinutes: from.durationMinutes ?? DEFAULTS.durationMinutes,
    techniqueNotes: null,
    techniques: [],
    sleep: null,
    pain: null,
    energy: null,
    focusPoint: null,
    rolls: []
  };
}

export function newRoll(): RollInput {
  return { partnerName: null, partnerBelt: null, durationMinutes: null, notes: null, steps: [], finishes: [] };
}

/** A stored session back in the shape the form edits. */
export function toInput(s: TrainingSession): TrainingSessionInput {
  return {
    id: s.id, date: s.date, attire: s.attire, format: s.format, durationMinutes: s.durationMinutes,
    techniqueNotes: s.techniqueNotes, techniques: [...s.techniques], sleep: s.sleep, pain: s.pain, energy: s.energy,
    focusPoint: s.focusPoint,
    rolls: s.rolls.map(r => ({
      partnerName: r.partnerName, partnerBelt: r.partnerBelt, durationMinutes: r.durationMinutes, notes: r.notes,
      steps: r.steps.map(st => ({ techniqueId: st.techniqueId, side: st.side, outcome: st.outcome })),
      finishes: r.finishes.map(f => ({ byMe: f.byMe, techniqueId: f.techniqueId, position: f.position }))
    }))
  };
}

/**
 * Records a session on one screen (/treino/novo) or edits one (/treinos/:id/editar). A new session is kept as a
 * draft in this browser while typed and, once saved, waits in the outbox until the API confirms it.
 */
@Component({
  selector: 'app-training-form',
  imports: [RouterLink, TechniquePickerComponent, RollCardComponent, FocusBannerComponent, LoginInviteComponent],
  templateUrl: './training-form.component.html',
  styleUrl: './training-form.component.scss'
})
export class TrainingFormComponent {
  protected auth = inject(AuthService);
  protected sync = inject(TrainingSyncService);
  private training = inject(TrainingService);
  private videos = inject(VideoService);
  private router = inject(Router);

  readonly attires = ATTIRES;
  readonly formats = FORMATS;
  readonly scales = [1, 2, 3, 4, 5];
  readonly quickDurations = [60, 90, 120];
  readonly physical = [
    { key: 'sleep', label: 'Sono' },
    { key: 'pain', label: 'Dor' },
    { key: 'energy', label: 'Energia' }
  ] as const;

  /** Id of the session being edited; null on /treino/novo. */
  editId = toSignal(inject(ActivatedRoute).paramMap.pipe(map(p => p.get('id'))), { initialValue: null });

  session = signal<TrainingSessionInput>(newSession());
  /** False until the draft, the earlier session's choices or the edited session are in. */
  ready = signal(false);
  saving = signal(false);
  error = signal('');
  fieldErrors = signal<FieldErrors>({});

  private catalog = toSignal(this.videos.techniques().pipe(catchError(() => of([] as TechniqueSummary[]))), { initialValue: [] });
  tagTechniques = computed(() => this.catalog().filter(t => !!t.id));
  finishTechniques = computed(() => this.tagTechniques().filter(t => FINISH_CATEGORIES.includes(t.category)));

  positions = signal<RecentPositions>({ recent: [], catalog: [] });
  /** Positions added with "+ posição" in this visit, offered in every roll. */
  private addedChips = signal<PositionChip[]>([]);

  /** The user's positions, most recently used first, then the ones added now and the ones already marked. */
  chips = computed(() => {
    const names = new Map(this.positions().catalog.map(p => [p.techniqueId, p.portuguese || p.english]));
    const nameOf = (id: string) => names.get(id) ?? this.techniqueName(id);
    const chips: PositionChip[] = this.positions().recent.map(n => ({
      techniqueId: n.techniqueId, side: n.side, label: n.label ? chipLabel(n.label, n.side) : chipLabel(nameOf(n.techniqueId), n.side)
    }));
    const marked = this.session().rolls.flatMap(r => r.steps)
      .map(s => ({ techniqueId: s.techniqueId, side: s.side, label: chipLabel(nameOf(s.techniqueId), s.side) }));
    for (const chip of [...this.addedChips(), ...marked]) {
      if (!chips.some(c => c.techniqueId === chip.techniqueId && c.side === chip.side)) chips.push(chip);
    }
    return chips;
  });

  errorList = computed(() => Object.values(this.fieldErrors()).flat());

  private started = false;

  constructor() {
    // Starts once the user is known to be signed in.
    effect(() => {
      if (!this.auth.user() || this.started) return;
      this.started = true;
      untracked(() => this.start());
    });
    // Keeps the new session's draft in this browser on every change.
    effect(() => {
      const session = this.session();
      if (this.ready() && !this.editId()) untracked(() => this.sync.saveDraft(session));
    });
  }

  private start(): void {
    this.training.recentPositions().subscribe({ next: p => this.positions.set(p), error: () => {} });
    const id = this.editId();
    if (id) {
      this.training.get(id).subscribe({
        next: s => {
          this.session.set(toInput(s));
          this.ready.set(true);
        },
        error: () => this.error.set('Treino não encontrado.')
      });
      return;
    }

    const draft = this.sync.loadDraft();
    if (draft) {
      this.session.set(draft);
      this.ready.set(true);
      return;
    }
    // A new session starts with the attire, format and length of the last one.
    this.training.list({ pageSize: 1 }).subscribe({
      next: page => {
        const last = page.items[0];
        if (last) this.session.set(newSession(last));
        this.ready.set(true);
      },
      error: () => this.ready.set(true)
    });
  }

  techniqueName(id: string): string {
    const t = this.catalog().find(c => c.id === id);
    return t ? t.portuguese || t.english : id;
  }

  patch(changes: Partial<TrainingSessionInput>): void {
    this.session.update(s => ({ ...s, ...changes }));
  }

  addTechnique(t: TechniqueSummary): void {
    if (t.id && !this.session().techniques.includes(t.id)) this.patch({ techniques: [...this.session().techniques, t.id] });
  }

  removeTechnique(id: string): void {
    this.patch({ techniques: this.session().techniques.filter(t => t !== id) });
  }

  addRoll(): void {
    this.patch({ rolls: [...this.session().rolls, newRoll()] });
  }

  updateRoll(index: number, roll: RollInput): void {
    this.patch({ rolls: this.session().rolls.map((r, i) => (i === index ? roll : r)) });
  }

  removeRoll(index: number): void {
    this.patch({ rolls: this.session().rolls.filter((_, i) => i !== index) });
  }

  addChip(chip: PositionChip): void {
    if (!this.addedChips().some(c => c.techniqueId === chip.techniqueId && c.side === chip.side)) {
      this.addedChips.update(chips => [...chips, chip]);
    }
  }

  /** A 1–5 score; tapping the chosen one again clears it. */
  setScale(field: 'sleep' | 'pain' | 'energy', value: number): void {
    this.patch({ [field]: this.session()[field] === value ? null : value });
  }

  /** Brings the session the API refused back into the form, with the problems of each field. */
  reopenRefused(): void {
    const refused = this.sync.refused();
    if (!refused) return;
    this.session.set(refused);
    this.fieldErrors.set(this.sync.refusedErrors());
    this.error.set(this.sync.error());
    this.sync.refused.set(null);
    this.sync.state.set('idle');
  }

  save(): void {
    const session = this.session();
    this.error.set('');
    this.fieldErrors.set({});
    if (!(session.durationMinutes > 0)) {
      this.fieldErrors.set({ durationMinutes: ['Informe a duração do treino em minutos.'] });
      return;
    }

    if (!this.editId()) {
      this.saving.set(true);
      this.sync.submit(session).then(outcome => {
        this.saving.set(false);
        switch (outcome) {
          case 'saved':
            this.router.navigate(['/treinos', session.id], { queryParams: { salvo: 1 } });
            break;
          case 'pending':
            // Kept on this device; the history shows it on top as "enviando…" until the API confirms it.
            this.router.navigate(['/treinos']);
            break;
          case 'refused':
            this.reopenRefused();
            break;
          case 'signed_out':
            this.error.set(this.sync.error());
            break;
        }
      });
      return;
    }

    this.saving.set(true);
    this.training.update(session).subscribe({
      next: saved => {
        this.saving.set(false);
        this.router.navigate(['/treinos', saved.id]);
      },
      error: (e: HttpErrorResponse) => {
        this.saving.set(false);
        if (e.status === 400 && e.error?.errors) this.fieldErrors.set(e.error.errors);
        else this.error.set('Não foi possível salvar. Confira a conexão e tente de novo.');
      }
    });
  }
}
