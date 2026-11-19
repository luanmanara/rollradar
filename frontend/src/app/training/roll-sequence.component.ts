import { Component, computed, input, output, signal } from '@angular/core';
import { MapEdge, MapNode } from '../game-map/game-map.model';
import { TRANSITION_KINDS, edgeName, kindLabel, nameLookup, techniquesForKind } from '../game-map/map-rules';
import { TechniqueSummary } from '../videos/fight-video.model';
import { TechniquePickerComponent } from '../videos/technique-picker.component';
import type { PositionChip } from './roll-card.component';
import { OUTCOMES } from './training-labels';
import { RollStepInput, TransitionInput } from './training.model';

type TransitionKind = NonNullable<TransitionInput['kind']>;

/**
 * Detailed mode of a roll: the marked positions in order, and between two of them the transition tried (an arrow of
 * the game map already there, or a kind with a catalog technique) and whether it worked.
 */
@Component({
  selector: 'app-roll-sequence',
  imports: [TechniquePickerComponent],
  templateUrl: './roll-sequence.component.html',
  styleUrl: './roll-sequence.component.scss'
})
export class RollSequenceComponent {
  steps = input<RollStepInput[]>([]);
  chips = input<PositionChip[]>([]);
  mapNodes = input<MapNode[]>([]);
  mapEdges = input<MapEdge[]>([]);
  /** The whole catalog, for the technique of a new transition. */
  techniques = input<TechniqueSummary[]>([]);
  /** Roll number, to keep ids unique on the page. */
  index = input(0);
  stepsChange = output<RollStepInput[]>();

  readonly kinds = TRANSITION_KINDS;
  readonly outcomes = OUTCOMES;
  /** Step whose transition is being chosen. */
  editing = signal<number | null>(null);
  newKind = signal<TransitionKind>('raspagem');

  private nameOf = computed(() => nameLookup(this.techniques()));
  techniquesForNew = computed(() => techniquesForKind(this.newKind(), this.techniques()));

  chipName(step: RollStepInput): string {
    return this.chips().find(c => c.techniqueId === step.techniqueId && c.side === step.side)?.label ?? step.techniqueId;
  }

  /** Arrows of the map leaving the step's position (submissions left out). */
  edgesFrom(step: RollStepInput): MapEdge[] {
    const node = this.mapNodes().find(n => n.techniqueId === step.techniqueId && n.side === step.side);
    return node ? this.mapEdges().filter(e => e.fromNodeId === node.id && e.kind !== 'finalizacao') : [];
  }

  /** "Raspagem · Raspagem de tesoura", or the name of the map's arrow. */
  transitionText(t: TransitionInput): string {
    if (t.edgeId !== null) {
      const edge = this.mapEdges().find(e => e.id === t.edgeId);
      return edge ? `${kindLabel(edge.kind)} · ${edgeName(edge, this.nameOf())}` : 'Transição do mapa';
    }
    const kind = t.kind ? kindLabel(t.kind) : 'Transição';
    return t.techniqueId ? `${kind} · ${this.nameOf()(t.techniqueId)}` : kind;
  }

  startEditing(j: number): void {
    this.editing.set(j);
    this.newKind.set('raspagem');
  }

  /** Picks an arrow of the map by id ("" leaves it as it was). */
  pickEdge(j: number, value: string): void {
    if (value) this.setTransition(j, { edgeId: +value, kind: null, techniqueId: null, succeeded: true });
  }

  /** A new transition of the chosen kind, with the technique picked (or none). */
  addNew(j: number, technique: TechniqueSummary | null): void {
    this.setTransition(j, { edgeId: null, kind: this.newKind(), techniqueId: technique?.id ?? null, succeeded: true });
  }

  toggleSucceeded(j: number): void {
    const t = this.steps()[j].transition;
    if (t) this.setTransition(j, { ...t, succeeded: !t.succeeded });
  }

  setTransition(j: number, transition: TransitionInput | null): void {
    this.editing.set(null);
    this.stepsChange.emit(this.steps().map((s, i) => (i === j ? { ...s, transition } : s)));
  }
}
