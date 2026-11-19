// Training diary, as the API sends it (/api/treinos, /api/parceiros, /api/mapa). Choices are the API's
// snake_case names; the labels the site shows are in training-labels.ts.

export type Attire = 'gi' | 'no_gi';
export type TrainingFormat = 'aula' | 'open' | 'competicao';
export type Belt = 'branca' | 'azul' | 'roxa' | 'marrom' | 'preta';
export type NodeSide = 'por_cima' | 'por_baixo' | 'neutro';
export type StepOutcome = 'bem' | 'neutro' | 'mal';
export type FocusReview = 'sim' | 'parcial' | 'nao';

/** A position marked in a roll; the API creates the user's game node for it on first use. */
export interface RollStepInput {
  techniqueId: string;
  side: NodeSide;
  outcome: StepOutcome;
  /** Detailed mode: how the user tried to go from this position to the next one. */
  transition?: TransitionInput | null;
}

/**
 * A transition of a detailed roll toward the next marked position: an arrow of the game map (edgeId), or a kind and
 * an optional catalog technique, which the API turns into an arrow (found or created).
 */
export interface TransitionInput {
  edgeId: number | null;
  kind: 'raspagem' | 'passagem' | 'fuga' | 'queda' | 'transicao' | null;
  techniqueId: string | null;
  succeeded: boolean;
}

/** A position of the user's game: a technique of the Posições/Guardas categories and a side. */
export interface PositionRef {
  techniqueId: string;
  side: NodeSide;
}

/** A submission in a roll: by the user (finalizei) or on the user (fui finalizado). Technique and position are optional. */
export interface RollFinishInput {
  byMe: boolean;
  techniqueId: string | null;
  position: PositionRef | null;
}

/** A roll (usually timed): any number of submissions either way; none means nobody was submitted. */
export interface RollInput {
  partnerName: string | null;
  partnerBelt: Belt | null;
  durationMinutes: number | null;
  notes: string | null;
  steps: RollStepInput[];
  finishes: RollFinishInput[];
}

/** A session as sent to POST/PUT /api/treinos; the id is generated here, so sending it again never duplicates it. */
export interface TrainingSessionInput {
  id: string;
  /** yyyy-mm-dd */
  date: string;
  attire: Attire;
  format: TrainingFormat;
  durationMinutes: number;
  techniqueNotes: string | null;
  techniques: string[];
  sleep: number | null;
  pain: number | null;
  energy: number | null;
  focusPoint: string | null;
  rolls: RollInput[];
}

export interface RollStep {
  nodeId: number;
  techniqueId: string;
  side: NodeSide;
  outcome: StepOutcome;
  /** Detailed mode: the arrow tried toward the next position, and whether it worked. */
  edgeId?: number | null;
  edgeSucceeded?: boolean | null;
}

export interface RollFinish extends RollFinishInput {
  nodeId: number | null;
}

export interface Roll {
  order: number;
  partnerId: number | null;
  partnerName: string | null;
  partnerBelt: Belt | null;
  durationMinutes: number | null;
  notes: string | null;
  steps: RollStep[];
  finishes: RollFinish[];
}

export interface TrainingSession {
  id: string;
  date: string;
  attire: Attire;
  format: TrainingFormat;
  durationMinutes: number;
  techniqueNotes: string | null;
  techniques: string[];
  sleep: number | null;
  pain: number | null;
  energy: number | null;
  focusPoint: string | null;
  rolls: Roll[];
  createdAt: string;
  updatedAt: string;
}

export interface TrainingPage {
  items: TrainingSession[];
  total: number;
  page: number;
  pageSize: number;
}

/** Filters of the history; empty values are left out of the request. */
export interface TrainingFilter {
  attire?: Attire | '';
  format?: TrainingFormat | '';
  partnerId?: number | null;
  q?: string;
  page?: number;
  pageSize?: number;
}

export interface Partner {
  id: number;
  name: string;
  belt: Belt | null;
}

export interface FocusPoint {
  id: number;
  text: string;
  originSessionId: string;
  originDate: string;
  createdAt: string;
}

export interface GameNode {
  id: number;
  techniqueId: string;
  label: string | null;
  side: NodeSide;
  hidden: boolean;
  lastUsed: string | null;
  uses: number;
}

export interface CatalogPosition {
  techniqueId: string;
  english: string;
  portuguese: string;
  category: string;
}

export interface RecentPositions {
  recent: GameNode[];
  catalog: CatalogPosition[];
}

/** Problems of each field, as the API answers a 400 ("rolls[0].finishes[1].techniqueId": [...]). */
export type FieldErrors = Record<string, string[]>;
