import { Attire, Belt, FocusReview, NodeSide, StepOutcome, TrainingFormat } from './training.model';

// What the site shows for each choice of the diary. In BJJ "rola" is masculine: "o rola", "um rola", "+ rola".

/** "Modalidade": with or without the gi changes the training, not only the clothes. */
export const ATTIRES: { value: Attire; label: string }[] = [
  { value: 'gi', label: 'Com kimono' },
  { value: 'no_gi', label: 'Sem kimono' }
];

export const FORMATS: { value: TrainingFormat; label: string }[] = [
  { value: 'aula', label: 'Aula' },
  { value: 'open', label: 'Open mat' },
  { value: 'competicao', label: 'Competição' }
];

export const BELTS: { value: Belt; label: string }[] = [
  { value: 'branca', label: 'Branca' },
  { value: 'azul', label: 'Azul' },
  { value: 'roxa', label: 'Roxa' },
  { value: 'marrom', label: 'Marrom' },
  { value: 'preta', label: 'Preta' }
];

export const SIDES: { value: NodeSide; label: string }[] = [
  { value: 'por_baixo', label: 'por baixo' },
  { value: 'por_cima', label: 'por cima' },
  { value: 'neutro', label: 'neutro' }
];

/** How a position went: "estive" (was there, no verdict), "fui bem", "sofri". */
export const OUTCOMES: Record<StepOutcome, string> = { neutro: 'estive', bem: 'fui bem', mal: 'sofri' };

/** "Finalizei 2 · Fui finalizado 3", or "Ninguém finalizou". */
export function finishesLabel(finishes: { byMe: boolean }[]): string {
  const mine = finishes.filter(f => f.byMe).length;
  const theirs = finishes.length - mine;
  if (finishes.length === 0) return 'Ninguém finalizou';
  return [mine ? `Finalizei ${mine}` : '', theirs ? `Fui finalizado ${theirs}` : ''].filter(Boolean).join(' · ');
}

export const REVIEWS: { value: FocusReview; label: string }[] = [
  { value: 'sim', label: 'Trabalhei' },
  { value: 'parcial', label: 'Em parte' },
  { value: 'nao', label: 'Ainda não' }
];

/** Categories of the catalog a roll can end with (same as the API checks). */
export const FINISH_CATEGORIES = ['Estrangulamentos', 'Chaves de braço', 'Chaves de perna'];

export function label<T extends string>(options: { value: T; label: string }[], value: T | null | undefined): string {
  return options.find(o => o.value === value)?.label ?? '';
}

export function sideLabel(side: NodeSide): string {
  return label(SIDES, side);
}

/** "terça, 21/10" for a yyyy-mm-dd date, read as a calendar day (no time zone shift). */
export function dayLabel(date: string): string {
  const d = parseDate(date);
  const weekday = d.toLocaleDateString('pt-BR', { weekday: 'long', timeZone: 'UTC' }).replace('-feira', '');
  return `${weekday}, ${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}`;
}

/** Monday of the week of the date, as yyyy-mm-dd. */
export function weekStart(date: string): string {
  const d = parseDate(date);
  d.setUTCDate(d.getUTCDate() - ((d.getUTCDay() + 6) % 7));
  return d.toISOString().slice(0, 10);
}

/** "Semana de 20/10/2026". */
export function weekLabel(monday: string): string {
  const d = parseDate(monday);
  return `Semana de ${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}/${d.getUTCFullYear()}`;
}

/** Today in the visitor's time zone, as yyyy-mm-dd. */
export function today(now = new Date()): string {
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** "1h30", "45 min". */
export function durationLabel(minutes: number): string {
  if (minutes < 60) return `${minutes} min`;
  const rest = minutes % 60;
  return `${Math.floor(minutes / 60)}h${rest ? pad(rest) : ''}`;
}

function parseDate(date: string): Date {
  return new Date(`${date}T00:00:00Z`);
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}
