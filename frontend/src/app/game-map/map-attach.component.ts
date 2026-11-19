import { Component, computed, inject, input, signal } from '@angular/core';
import { AuthService } from '../account/auth.service';
import { FormatTimePipe } from '../videos/format-time.pipe';
import { VideoService } from '../videos/video.service';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { TechniqueSummary } from '../videos/fight-video.model';
import { GameMapView } from './game-map.model';
import { GameMapService } from './game-map.service';
import { edgeName, nameLookup, nodeName } from './map-rules';

/**
 * "Anexar ao meu mapa" in the video modal: pins the fight, at the second being watched, to one position or arrow of
 * the user's map. Shown only to signed-in users.
 */
@Component({
  selector: 'app-map-attach',
  imports: [FormatTimePipe],
  template: `
    @if (auth.user()) {
      @if (!open()) {
        <button type="button" class="attach" (click)="start()">📌 Anexar ao meu mapa</button>
        @if (saved()) { <span class="saved" role="status">Anexado.</span> }
      } @else {
        <div class="form" role="group" aria-label="Anexar ao meu mapa">
          <span class="form__title">Anexar em {{ second() | formatTime }}</span>
          @if (map(); as m) {
            @if (options().length === 0) {
              <p class="muted">Seu mapa ainda não tem posições. Registre rolas com posições para anexar vídeos.</p>
            } @else {
              <label class="sr-only" for="attach-target">Onde anexar</label>
              <select id="attach-target" class="text" (change)="target.set($any($event.target).value)">
                <option value="">Escolha a posição ou seta</option>
                <optgroup label="Posições">
                  @for (o of options(); track o.value) { @if (o.group === 'node') { <option [value]="o.value">{{ o.label }}</option> } }
                </optgroup>
                <optgroup label="Setas">
                  @for (o of options(); track o.value) { @if (o.group === 'edge') { <option [value]="o.value">{{ o.label }}</option> } }
                </optgroup>
              </select>
              <label class="sr-only" for="attach-note">Anotação</label>
              <input #note id="attach-note" class="text" type="text" maxlength="500" placeholder="Anotação (opcional)">
              <div class="row">
                <button type="button" class="btn btn--primary" [disabled]="!target() || busy()" (click)="save(note.value)">Anexar</button>
                <button type="button" class="btn" (click)="open.set(false)">Cancelar</button>
              </div>
            }
          } @else {
            <p class="muted" aria-busy="true">Carregando seu mapa…</p>
          }
          @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        </div>
      }
    }
  `,
  styles: `
    :host { display: block; }
    .attach {
      padding: 6px 12px; border: 1px solid var(--line); border-radius: 999px; background: var(--surface);
      font-size: 0.85rem; font-weight: 600;
      &:hover { border-color: var(--blue); color: var(--blue); }
    }
    .saved { margin-left: 8px; color: var(--green-ink); font-size: 0.85rem; font-weight: 600; }
    .form { display: grid; gap: 8px; padding: 12px; border: 1px solid var(--line); border-radius: var(--radius-sm); background: var(--page); }
    .form__title { font-weight: 700; }
    .text { width: 100%; min-height: 40px; padding: 8px 10px; border: 1.5px solid var(--line); border-radius: var(--radius-sm); background: var(--surface); font: inherit; }
    .row { display: flex; gap: 8px; }
    .btn { min-height: 36px; padding: 0 14px; border: 1.5px solid var(--line); border-radius: 999px; background: var(--surface); font-weight: 700; }
    .btn--primary { border-color: var(--blue); background: var(--blue); color: #fff; }
    .btn:disabled { opacity: 0.6; cursor: default; }
    .muted { color: var(--muted); font-size: 0.85rem; }
    .error { color: var(--red-ink); font-size: 0.85rem; }
  `
})
export class MapAttachComponent {
  protected auth = inject(AuthService);
  private mapService = inject(GameMapService);
  private catalog = toSignal(inject(VideoService).techniques().pipe(catchError(() => of([] as TechniqueSummary[]))), { initialValue: [] });

  videoId = input.required<string>();
  /** Reads the second being watched when the user saves. */
  currentSecond = input.required<() => number>();

  open = signal(false);
  map = signal<GameMapView | null>(null);
  target = signal('');
  second = signal(0);
  busy = signal(false);
  saved = signal(false);
  error = signal('');

  /** Positions and arrows to choose from, as "node:12" / "edge:5". */
  options = computed(() => {
    const m = this.map();
    if (!m) return [];
    const nameOf = nameLookup(this.catalog());
    const nodes = new Map(m.nodes.map(n => [n.id, n]));
    return [
      ...m.nodes.filter(n => !n.hidden).map(n => ({ group: 'node', value: `node:${n.id}`, label: nodeName(n, nameOf) })),
      ...m.edges.filter(e => nodes.get(e.fromNodeId) && !nodes.get(e.fromNodeId)!.hidden).map(e => ({
        group: 'edge', value: `edge:${e.id}`, label: `${edgeName(e, nameOf)} (de ${nodeName(nodes.get(e.fromNodeId)!, nameOf)})`
      }))
    ];
  });

  start(): void {
    this.second.set(Math.max(0, Math.floor(this.currentSecond()())));
    this.open.set(true);
    this.saved.set(false);
    this.error.set('');
    this.mapService.map().subscribe({ next: m => this.map.set(m), error: () => this.error.set('Não foi possível carregar o seu mapa.') });
  }

  save(note: string): void {
    const [group, id] = this.target().split(':');
    if (!id) return;
    this.busy.set(true);
    this.mapService.attachVideo(group === 'node' ? { nodeId: +id } : { edgeId: +id }, this.videoId(), this.second(), note.trim() || null).subscribe({
      next: () => {
        this.busy.set(false);
        this.open.set(false);
        this.saved.set(true);
      },
      error: () => {
        this.busy.set(false);
        this.error.set('Não foi possível anexar.');
      }
    });
  }
}
