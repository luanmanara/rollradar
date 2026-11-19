import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { dayLabel } from '../training/training-labels';
import { FightVideo, TechniqueMoment, TechniqueSummary } from '../videos/fight-video.model';
import { FormatTimePipe } from '../videos/format-time.pipe';
import { EdgeDetail, MapEdge, MapNode, MapSelection, MapVideo, NodeDetail } from './game-map.model';
import { GameMapService } from './game-map.service';
import { MapEdgeFormComponent } from './map-edge-form.component';
import { NameOf, edgeName, kindLabel, nodeName, percent } from './map-rules';

/** A clip to open in the video modal. */
export interface MapClip {
  video: FightVideo;
  moment: TechniqueMoment;
}

/** A pinned video as the modal wants it: the fight with one "moment" at the saved second. */
export function clipOf(video: MapVideo): MapClip {
  const moment: TechniqueMoment = { id: `map-${video.id}`, technique: '', startSeconds: video.startSeconds ?? 0, snippet: video.note ?? '' };
  return {
    video: { id: video.videoId, title: video.title, url: video.url, thumbnailUrl: video.thumbnailUrl, channel: video.channel, uploadDate: video.uploadDate, moments: [moment] },
    moment
  };
}

/**
 * Panel over the map for a position or an arrow: its numbers, the latest notes (each opens its session), the videos
 * pinned to it and fights of its technique, and the actions: rename, hide, add an arrow, delete an arrow.
 */
@Component({
  selector: 'app-map-panel',
  imports: [RouterLink, FormatTimePipe, MapEdgeFormComponent],
  host: { '(document:keydown.escape)': 'closed.emit()' },
  templateUrl: './map-panel.component.html',
  styleUrl: './map-panel.component.scss'
})
export class MapPanelComponent {
  private map = inject(GameMapService);

  selection = input.required<MapSelection>();
  nodes = input<MapNode[]>([]);
  catalog = input<TechniqueSummary[]>([]);
  nameOf = input.required<NameOf>();
  closed = output<void>();
  /** Something changed on the map; reload it. */
  changed = output<void>();
  selected = output<MapSelection>();
  play = output<MapClip>();

  readonly day = dayLabel;
  readonly percent = percent;

  node = signal<NodeDetail | null>(null);
  edge = signal<EdgeDetail | null>(null);
  error = signal('');
  busy = signal(false);
  renaming = signal(false);
  creating = signal(false);

  title = computed(() => {
    const n = this.node()?.node;
    if (n) return nodeName(n, this.nameOf());
    const e = this.edge()?.edge;
    return e ? edgeName(e, this.nameOf()) : '';
  });

  eyebrow = computed(() => {
    const e = this.edge()?.edge;
    if (!e) return 'Posição';
    if (e.kind === 'finalizacao') return e.againstMe ? 'Finalização sofrida' : 'Finalização';
    return kindLabel(e.kind);
  });

  /** "Guarda fechada · por baixo → Montada · por cima" for a transition, "de Cem quilos · por baixo" for a submission. */
  route = computed(() => {
    const e = this.edge()?.edge;
    if (!e) return '';
    const from = this.nodes().find(n => n.id === e.fromNodeId);
    const to = this.nodes().find(n => n.id === e.toNodeId);
    const name = (n: MapNode | undefined) => (n ? nodeName(n, this.nameOf()) : '?');
    return e.kind === 'finalizacao' ? `de ${name(from)}` : `${name(from)} → ${name(to)}`;
  });

  /** Other visible positions an arrow can go to. */
  targets = computed(() => this.nodes().filter(n => !n.hidden && n.id !== this.node()?.node.id));


  constructor() {
    effect(() => {
      const selection = this.selection();
      untracked(() => this.load(selection));
    });
  }

  load(selection: MapSelection): void {
    this.error.set('');
    this.renaming.set(false);
    this.creating.set(false);
    this.node.set(null);
    this.edge.set(null);
    const fail = () => this.error.set('Não foi possível carregar.');
    if (selection.target === 'node') this.map.node(selection.id).subscribe({ next: d => this.node.set(d), error: fail });
    else this.map.edge(selection.id).subscribe({ next: d => this.edge.set(d), error: fail });
  }

  targetName(node: MapNode): string {
    return nodeName(node, this.nameOf());
  }

  arrowText(e: MapEdge): string {
    const name = edgeName(e, this.nameOf());
    if (e.kind === 'finalizacao') return `${e.againstMe ? '✕' : '✓'} ${name}`;
    const to = this.nodes().find(n => n.id === e.toNodeId);
    return `→ ${name}${to ? ` (${nodeName(to, this.nameOf())})` : ''}`;
  }

  rename(label: string): void {
    const n = this.node()?.node;
    const e = this.edge()?.edge;
    const request = n ? this.map.updateNode(n.id, { label: label.trim() }) : e ? this.map.updateEdge(e.id, { label: label.trim() }) : null;
    this.run(request, () => this.renaming.set(false));
  }

  /** Hidden positions leave the map and the suggestions; the history stays. */
  toggleHidden(): void {
    const n = this.node()?.node;
    if (!n) return;
    this.run(this.map.updateNode(n.id, { hidden: !n.hidden }), () => {});
  }

  removeEdge(): void {
    const e = this.edge()?.edge;
    if (!e) return;
    this.run(this.map.removeEdge(e.id), () => this.closed.emit(), false);
  }

  onCreated(): void {
    this.creating.set(false);
    this.changed.emit();
    this.load(this.selection());
  }

  removeVideo(video: MapVideo): void {
    this.run(this.map.removeVideo(video.id), () => {});
  }

  playPinned(video: MapVideo): void {
    this.play.emit(clipOf(video));
  }

  playSuggested(video: FightVideo): void {
    if (video.moments.length > 0) this.play.emit({ video, moment: video.moments[0] });
  }

  /** Runs a change, then reloads the panel (unless it is gone) and tells the map. */
  private run(request: Observable<unknown> | null, done: () => void, reload = true): void {
    if (!request) return;
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: () => {
        this.busy.set(false);
        done();
        this.changed.emit();
        if (reload) this.load(this.selection());
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        const errors: Record<string, string[]> = e.error?.errors ?? {};
        this.error.set(Object.values(errors).flat()[0] ?? 'Não foi possível salvar.');
      }
    });
  }
}
