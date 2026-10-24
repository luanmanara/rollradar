import { Component, computed, inject, input, output, signal } from '@angular/core';
import { TechniqueSummary } from '../videos/fight-video.model';
import { normalize } from '../videos/technique-match';
import { TechniquePickerComponent } from '../videos/technique-picker.component';
import { BELTS, OUTCOMES, SIDES, finishesLabel } from './training-labels';
import { Belt, CatalogPosition, NodeSide, Partner, PositionRef, RollFinishInput, RollInput, RollStepInput, StepOutcome } from './training.model';
import { TrainingService } from './training.service';

/** A position offered as a chip in the rolls, e.g. "Guarda fechada · por baixo". */
export interface PositionChip {
  techniqueId: string;
  side: NodeSide;
  label: string;
}

/** Taps on a position chip: unmarked → estive → fui bem → sofri → unmarked. Steps keep the order they were marked in. */
export function cycleStep(steps: RollStepInput[], chip: PositionRef): RollStepInput[] {
  const i = steps.findIndex(s => s.techniqueId === chip.techniqueId && s.side === chip.side);
  if (i < 0) return [...steps, { techniqueId: chip.techniqueId, side: chip.side, outcome: 'neutro' }];
  const next: Record<StepOutcome, StepOutcome | null> = { neutro: 'bem', bem: 'mal', mal: null };
  const outcome = next[steps[i].outcome];
  return outcome ? steps.map((s, j) => (j === i ? { ...s, outcome } : s)) : steps.filter((_, j) => j !== i);
}

/** "closed-guard|por_baixo", to use a position as an option value. */
export function positionKey(p: PositionRef | null): string {
  return p ? `${p.techniqueId}|${p.side}` : '';
}

/** One roll in the session form: partner and belt, length, positions, submissions either way, notes. */
@Component({
  selector: 'app-roll-card',
  imports: [TechniquePickerComponent],
  templateUrl: './roll-card.component.html',
  styleUrl: './roll-card.component.scss'
})
export class RollCardComponent {
  private training = inject(TrainingService);

  roll = input.required<RollInput>();
  index = input.required<number>();
  chips = input<PositionChip[]>([]);
  catalog = input<CatalogPosition[]>([]);
  finishTechniques = input<TechniqueSummary[]>([]);

  rollChange = output<RollInput>();
  removed = output<void>();
  positionAdded = output<PositionChip>();

  readonly belts = BELTS;
  readonly sides = SIDES;
  readonly outcomes = OUTCOMES;
  readonly quickMinutes = [5, 6, 10];
  readonly key = positionKey;

  partners = signal<Partner[]>([]);
  adding = signal(false);
  newPosition = signal('');
  newSide = signal<NodeSide>('por_baixo');

  /** Catalog positions grouped by category, for the "+ posição" list. */
  catalogGroups = computed(() => {
    const groups = new Map<string, CatalogPosition[]>();
    for (const p of this.catalog()) groups.set(p.category, [...(groups.get(p.category) ?? []), p]);
    return [...groups].map(([category, positions]) => ({ category, positions }));
  });

  /** Positions marked in this roll: where a submission can have come from. */
  markedChips = computed(() => this.chips().filter(c => this.roll().steps.some(s => s.techniqueId === c.techniqueId && s.side === c.side)));

  summary = computed(() => finishesLabel(this.roll().finishes));

  private partnerTimer: ReturnType<typeof setTimeout> | null = null;

  outcomeOf(chip: PositionRef): StepOutcome | null {
    return this.roll().steps.find(s => s.techniqueId === chip.techniqueId && s.side === chip.side)?.outcome ?? null;
  }

  tap(chip: PositionChip): void {
    this.change({ steps: cycleStep(this.roll().steps, chip) });
  }

  setBelt(belt: Belt): void {
    this.change({ partnerBelt: this.roll().partnerBelt === belt ? null : belt });
  }

  /** Empty or zero clears it: the length of a roll is optional. */
  setMinutes(value: number | null): void {
    this.change({ durationMinutes: value && value > 0 ? value : null });
  }

  addFinish(byMe: boolean): void {
    this.change({ finishes: [...this.roll().finishes, { byMe, techniqueId: null, position: null }] });
  }

  removeFinish(index: number): void {
    this.change({ finishes: this.roll().finishes.filter((_, i) => i !== index) });
  }

  updateFinish(index: number, patch: Partial<RollFinishInput>): void {
    this.change({ finishes: this.roll().finishes.map((f, i) => (i === index ? { ...f, ...patch } : f)) });
  }

  /** The roll's marked positions, plus the one the submission already points to if it was unmarked since. */
  finishOptions(finish: RollFinishInput): PositionChip[] {
    const marked = this.markedChips();
    const current = this.chips().find(c => positionKey(c) === positionKey(finish.position));
    return current && !marked.includes(current) ? [...marked, current] : marked;
  }

  setFinishPosition(index: number, key: string): void {
    const chip = this.chips().find(c => positionKey(c) === key);
    this.updateFinish(index, { position: chip ? { techniqueId: chip.techniqueId, side: chip.side } : null });
  }

  techniqueName(id: string | null): string {
    const t = this.finishTechniques().find(f => f.id === id);
    return t ? t.portuguese || t.english : (id ?? '');
  }

  /** Typing the partner: suggestions from the user's partners; a known name brings back the belt. */
  onPartnerInput(name: string): void {
    const known = this.partners().find(p => normalize(p.name) === normalize(name));
    this.change({ partnerName: name || null, partnerBelt: known?.belt && !this.roll().partnerBelt ? known.belt : this.roll().partnerBelt });
    if (this.partnerTimer) clearTimeout(this.partnerTimer);
    this.partnerTimer = setTimeout(() => {
      this.training.partners(name).subscribe({ next: list => this.partners.set(list), error: () => this.partners.set([]) });
    }, 250);
  }

  addPosition(): void {
    const position = this.catalog().find(p => p.techniqueId === this.newPosition());
    if (!position) return;
    const chip: PositionChip = { techniqueId: position.techniqueId, side: this.newSide(), label: chipLabel(position.portuguese || position.english, this.newSide()) };
    this.positionAdded.emit(chip);
    if (!this.outcomeOf(chip)) this.tap(chip);
    this.adding.set(false);
    this.newPosition.set('');
  }

  change(patch: Partial<RollInput>): void {
    this.rollChange.emit({ ...this.roll(), ...patch });
  }
}

export function chipLabel(name: string, side: NodeSide): string {
  return `${name} · ${SIDES.find(s => s.value === side)?.label ?? side}`;
}
