import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { AuthService } from '../account/auth.service';
import { LoginInviteComponent } from '../training/login-invite.component';
import { SIDES } from '../training/training-labels';
import { NodeSide } from '../training/training.model';
import { TechniqueSummary } from '../videos/fight-video.model';
import { VideoModalComponent } from '../videos/video-modal.component';
import { VideoService } from '../videos/video.service';
import { GameMapView, Highlight, MapSelection } from './game-map.model';
import { GameMapService } from './game-map.service';
import { MapGraphComponent } from './map-graph.component';
import { MapListComponent } from './map-list.component';
import { MapClip, MapPanelComponent } from './map-panel.component';
import { edgeName, nameLookup, nodeName, percent } from './map-rules';

export type MapView = 'lista' | 'grafo';

/** Below this width the map opens as a list (a graph is hard to use on a phone). */
export const LIST_BELOW_PX = 720;

/**
 * The user's game map (/mapa): positions and arrows as a list or a graph, the strong and weak points, and a panel for
 * each position or arrow. Numbers are counted from the rolls of the diary.
 */
@Component({
  selector: 'app-game-map-page',
  imports: [RouterLink, LoginInviteComponent, MapListComponent, MapGraphComponent, MapPanelComponent, VideoModalComponent],
  templateUrl: './game-map-page.component.html',
  styleUrl: './game-map-page.component.scss'
})
export class GameMapPageComponent {
  protected auth = inject(AuthService);
  private mapService = inject(GameMapService);

  readonly sides = SIDES;
  readonly percent = percent;

  catalog = toSignal(inject(VideoService).techniques().pipe(catchError(() => of([] as TechniqueSummary[]))), { initialValue: [] });
  nameOf = computed(() => nameLookup(this.catalog()));
  categoryOf = computed(() => {
    const categories = new Map(this.catalog().filter(t => t.id).map(t => [t.id!, t.category]));
    return (id: string) => categories.get(id) ?? '';
  });
  /** Catalog positions the user can add to the map. */
  positions = computed(() => this.catalog().filter(t => t.id && (t.category === 'Guardas' || t.category === 'Posições')));

  map = signal<GameMapView | null>(null);
  error = signal('');
  view = signal<MapView>(defaultView());
  selection = signal<MapSelection | null>(null);
  clip = signal<MapClip | null>(null);
  adding = signal(false);
  newPosition = signal('');
  newSide = signal<NodeSide>('por_baixo');
  showHidden = signal(false);

  visibleCount = computed(() => this.map()?.nodes.filter(n => !n.hidden).length ?? 0);
  hidden = computed(() => this.map()?.nodes.filter(n => n.hidden) ?? []);

  constructor() {
    effect(() => {
      if (this.auth.user()) untracked(() => this.load());
    });
  }

  load(): void {
    this.mapService.map().subscribe({
      next: m => {
        this.map.set(m);
        this.error.set('');
      },
      error: () => this.error.set('Não foi possível carregar o mapa.')
    });
  }

  /** "Guarda fechada · por baixo" or "Raspagem de tesoura", for a highlight. */
  highlightName(h: Highlight): string {
    const m = this.map();
    if (!m) return '';
    if (h.target === 'node') {
      const node = m.nodes.find(n => n.id === h.id);
      return node ? nodeName(node, this.nameOf()) : '';
    }
    const edge = m.edges.find(e => e.id === h.id);
    return edge ? edgeName(edge, this.nameOf()) : '';
  }

  hiddenName(id: number): string {
    const node = this.map()?.nodes.find(n => n.id === id);
    return node ? nodeName(node, this.nameOf()) : '';
  }

  select(selection: MapSelection): void {
    this.selection.set(selection);
  }

  savePositions(positions: { id: number; x: number; y: number }[]): void {
    this.mapService.moveNodes(positions).subscribe({ error: () => {} });
  }

  addNode(): void {
    if (!this.newPosition()) return;
    this.mapService.addNode(this.newPosition(), this.newSide()).subscribe({
      next: node => {
        this.adding.set(false);
        this.newPosition.set('');
        this.load();
        this.selection.set({ target: 'node', id: node.id });
      },
      error: () => this.error.set('Não foi possível adicionar a posição.')
    });
  }

  unhide(id: number): void {
    this.mapService.updateNode(id, { hidden: false }).subscribe({ next: () => this.load(), error: () => {} });
  }
}

function defaultView(): MapView {
  try {
    return innerWidth < LIST_BELOW_PX ? 'lista' : 'grafo';
  } catch {
    return 'lista';
  }
}
