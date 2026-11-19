// The game map, as /api/mapa sends it. Kinds and sides are the API's snake_case names.
import { FightVideo } from '../videos/fight-video.model';
import { NodeSide } from '../training/training.model';

export type EdgeKind = 'raspagem' | 'passagem' | 'fuga' | 'queda' | 'transicao' | 'finalizacao';

export interface NodeStats {
  steps: number;
  good: number;
  bad: number;
  finishesFor: number;
  finishesAgainst: number;
  /** Fui bem / (fui bem + sofri); null while neither was marked. */
  successRate: number | null;
}

export interface EdgeStats {
  /** Tries of the transition in detailed rolls, or times the submission happened. */
  uses: number;
  succeeded: number;
  successRate: number | null;
}

export interface MapNode {
  id: number;
  techniqueId: string;
  label: string | null;
  side: NodeSide;
  hidden: boolean;
  x: number | null;
  y: number | null;
  stats: NodeStats;
}

export interface MapEdge {
  id: number;
  fromNodeId: number;
  /** Null for submissions. */
  toNodeId: number | null;
  kind: EdgeKind;
  techniqueId: string | null;
  label: string | null;
  /** Submissions only: the partner finishes the user from the position. */
  againstMe: boolean;
  stats: EdgeStats;
}

export interface Highlight {
  target: 'node' | 'edge';
  id: number;
  score: number;
  occurrences: number;
}

export interface GameMapView {
  nodes: MapNode[];
  edges: MapEdge[];
  highlights: { strong: Highlight[]; weak: Highlight[] };
}

export interface MapNote {
  sessionId: string;
  date: string;
  partnerName: string | null;
  notes: string;
}

export interface MapVideo {
  id: number;
  videoId: string;
  title: string;
  url: string;
  thumbnailUrl: string;
  channel: string;
  uploadDate: string | null;
  startSeconds: number | null;
  note: string | null;
}

export interface NodeDetail {
  node: MapNode;
  outgoing: MapEdge[];
  notes: MapNote[];
  videos: MapVideo[];
  suggested: FightVideo[];
}

export interface EdgeDetail {
  edge: MapEdge;
  notes: MapNote[];
  videos: MapVideo[];
  suggested: FightVideo[];
}

export interface EdgeInput {
  fromNodeId: number;
  toNodeId: number | null;
  kind: EdgeKind;
  techniqueId: string | null;
  label?: string | null;
  againstMe: boolean;
}

/** What a panel shows: a position or an arrow of the map. */
export type MapSelection = { target: 'node'; id: number } | { target: 'edge'; id: number };
