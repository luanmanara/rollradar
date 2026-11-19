import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { TechniqueSummary } from '../videos/fight-video.model';
import { TechniquePickerComponent } from '../videos/technique-picker.component';
import { EdgeKind, MapNode } from './game-map.model';
import { GameMapService } from './game-map.service';
import { EDGE_KINDS, NameOf, nodeName, techniquesForKind } from './map-rules';

/** New arrow from a position: kind, target (or who finishes, for a submission) and an optional catalog technique. */
@Component({
  selector: 'app-map-edge-form',
  imports: [TechniquePickerComponent],
  template: `
    <div class="form">
      <div class="choices" role="group" aria-label="Tipo da seta">
        @for (k of kinds; track k.value) {
          <button type="button" class="seg" [attr.aria-pressed]="kind() === k.value" (click)="setKind(k.value)">{{ k.label }}</button>
        }
      </div>
      @if (kind() === 'finalizacao') {
        <div class="choices" role="group" aria-label="Quem finaliza">
          <button type="button" class="seg" [attr.aria-pressed]="!againstMe()" (click)="againstMe.set(false)">Eu finalizo</button>
          <button type="button" class="seg" [attr.aria-pressed]="againstMe()" (click)="againstMe.set(true)">Sou finalizado</button>
        </div>
      } @else {
        <label class="sr-only" for="map-target">Leva para</label>
        <select class="text" id="map-target" (change)="target.set(+$any($event.target).value || null)">
          <option value="">Leva para qual posição?</option>
          @for (t of targets(); track t.id) { <option [value]="t.id">{{ name(t) }}</option> }
        </select>
      }
      @if (technique(); as t) {
        <p class="chosen"><span>{{ t.portuguese || t.english }}</span> <button type="button" class="seg" (click)="technique.set(null)">Trocar</button></p>
      } @else {
        <app-technique-picker [techniques]="techniques()" inputId="map-new-technique" submitLabel="Ok"
          placeholder="Técnica do catálogo (opcional)" (picked)="technique.set($event)" />
      }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      <button type="button" class="seg seg--primary" [disabled]="busy()" (click)="save()">Criar seta</button>
    </div>
  `,
  styles: `
    .form { display: grid; gap: 10px; padding: 12px; border: 1px dashed var(--line); border-radius: var(--radius-sm); }
    .choices { display: flex; flex-wrap: wrap; gap: 6px; }
    .seg {
      min-height: 38px; padding: 0 14px; border: 1.5px solid var(--line); border-radius: 999px; background: var(--surface); font-weight: 600;
      &[aria-pressed="true"] { border-color: var(--blue); background: var(--blue-50); color: var(--blue); }
      &--primary { justify-self: start; border-color: var(--blue); background: var(--blue); color: #fff; }
      &:disabled { opacity: 0.6; cursor: default; }
    }
    .text { width: 100%; min-height: 42px; padding: 8px 10px; border: 1.5px solid var(--line); border-radius: var(--radius-sm); background: var(--surface); font: inherit; }
    .chosen { display: flex; align-items: center; gap: 8px; font-weight: 600; }
    .error { color: var(--red-ink); font-size: 0.85rem; }
  `
})
export class MapEdgeFormComponent {
  private map = inject(GameMapService);

  from = input.required<MapNode>();
  /** Positions the arrow can go to. */
  targets = input<MapNode[]>([]);
  catalog = input<TechniqueSummary[]>([]);
  nameOf = input.required<NameOf>();
  created = output<void>();

  readonly kinds = EDGE_KINDS;
  kind = signal<EdgeKind>('raspagem');
  target = signal<number | null>(null);
  againstMe = signal(false);
  technique = signal<TechniqueSummary | null>(null);
  busy = signal(false);
  error = signal('');

  techniques = computed(() => techniquesForKind(this.kind(), this.catalog()));

  name(node: MapNode): string {
    return nodeName(node, this.nameOf());
  }

  setKind(kind: EdgeKind): void {
    this.kind.set(kind);
    this.technique.set(null);
  }

  save(): void {
    const finish = this.kind() === 'finalizacao';
    if (!finish && this.target() === null) {
      this.error.set('Escolha para qual posição a seta leva.');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    this.map.addEdge({
      fromNodeId: this.from().id,
      toNodeId: finish ? null : this.target(),
      kind: this.kind(),
      techniqueId: this.technique()?.id ?? null,
      againstMe: finish && this.againstMe()
    }).subscribe({
      next: () => {
        this.busy.set(false);
        this.created.emit();
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        const errors: Record<string, string[]> = e.error?.errors ?? {};
        this.error.set(Object.values(errors).flat()[0] ?? 'Não foi possível criar a seta.');
      }
    });
  }
}
