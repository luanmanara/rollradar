import { FightVideo, TechniqueMoment } from './fight-video.model';
import { normalize } from './technique-match';

/** How the results are shown: one card per moment (to watch in sequence) or one card per fight. */
export type ResultView = 'momentos' | 'lutas';

/**
 * "melhores": approved and well-voted moments first; "mencoes": fights with more moments first (the API's
 * order); "recentes": newest fights first.
 */
export type ResultSort = 'melhores' | 'mencoes' | 'recentes';

export const SORTS: { value: ResultSort; label: string }[] = [
  { value: 'melhores', label: 'Melhores' },
  { value: 'mencoes', label: 'Mais menções' },
  { value: 'recentes', label: 'Mais recentes' }
];

/** Upload date window, in years back from today; empty for any date. */
export type ResultPeriod = '' | '1a' | '2a';

export interface ResultFilters {
  athlete: string;
  /** Every spelling of the athlete when the catalog knows them; otherwise the typed text is looked up as is. */
  athleteNames?: string[];
  channel: string;
  period: ResultPeriod;
  /** Only the moments the curator approved. */
  approvedOnly: boolean;
}

export const NO_FILTERS: ResultFilters = { athlete: '', channel: '', period: '', approvedOnly: false };

export const PERIODS: { value: ResultPeriod; label: string }[] = [
  { value: '', label: 'Qualquer data' },
  { value: '1a', label: 'Último ano' },
  { value: '2a', label: 'Últimos 2 anos' }
];

/** A moment with the fight it belongs to: one item of the player's queue. */
export interface Clip {
  video: FightVideo;
  moment: TechniqueMoment;
}

export function hasFilters(filters: ResultFilters): boolean {
  return !!(filters.athlete.trim() || filters.channel || filters.period || filters.approvedOnly);
}

export function isApproved(moment: TechniqueMoment): boolean {
  return moment.status === 'bom';
}

/** Approved moments first, then by votes: useful minus not useful. */
export function momentScore(moment: TechniqueMoment): number {
  return (isApproved(moment) ? 1000 : 0) + (moment.votesUseful ?? 0) - (moment.votesNotUseful ?? 0);
}

/**
 * Fights matching the filters: athlete in the title, channel, and upload date within the period. With
 * approvedOnly, each fight keeps only its approved moments and fights without any are left out.
 */
export function filterVideos(videos: FightVideo[], filters: ResultFilters, today = new Date()): FightVideo[] {
  const athlete = normalize(filters.athlete);
  const names = athlete ? (filters.athleteNames?.length ? filters.athleteNames.map(normalize) : [athlete]) : [];
  const since = periodStart(filters.period, today);
  return videos
    .filter(v =>
      (!names.length || names.some(name => normalize(v.title).includes(name))) &&
      (!filters.channel || v.channel === filters.channel) &&
      (!since || (!!v.uploadDate && v.uploadDate >= since)))
    .map(v => filters.approvedOnly ? { ...v, moments: v.moments.filter(isApproved) } : v)
    .filter(v => v.moments.length > 0);
}

/**
 * Fights in the chosen order. "mencoes" keeps the API's order, which already has the most moments first;
 * "melhores" puts first the fights with the best moment, keeping the API's order on ties.
 */
export function sortVideos(videos: FightVideo[], sort: ResultSort): FightVideo[] {
  if (sort === 'recentes') return [...videos].sort((a, b) => (b.uploadDate ?? '').localeCompare(a.uploadDate ?? ''));
  if (sort === 'melhores') return [...videos].sort((a, b) => bestScore(b) - bestScore(a));
  return videos;
}

/**
 * Every moment of the fights, as the "Momentos" view and the player's queue list them. Fight by fight, each
 * fight's moments by time; with "melhores", the best moments first and, among equals, one moment of each
 * fight in turn, so a fight with many mentions doesn't fill the first screen.
 */
export function clipsOf(videos: FightVideo[], sort?: ResultSort): Clip[] {
  if (sort !== 'melhores') return videos.flatMap(video => video.moments.map(moment => ({ video, moment })));

  const inTurns: Clip[] = [];
  const longest = Math.max(0, ...videos.map(v => v.moments.length));
  for (let i = 0; i < longest; i++) {
    for (const video of videos) if (i < video.moments.length) inTurns.push({ video, moment: video.moments[i] });
  }
  return inTurns.sort((a, b) => momentScore(b.moment) - momentScore(a.moment));
}

function bestScore(video: FightVideo): number {
  return Math.max(...video.moments.map(momentScore));
}

/** Athletes offered as suggestions in the athlete filter, most fights first. */
export const ATHLETES_LIMIT = 30;

/**
 * Athlete names taken from titles like "Gordon Ryan vs Felipe Pena | ADCC 2024" or "A vs B / Open": what comes
 * before the event split at "vs", without a lead-in such as "Full Match:", as the API does for the search box. A guess, so titles in other formats give
 * no names, and the filter still searches the whole title. Names in more fights come first.
 */
export function athletesOf(videos: FightVideo[], limit = ATHLETES_LIMIT): string[] {
  const counts = new Map<string, { name: string; fights: number }>();
  for (const video of videos) {
    const sides = video.title.split(/\s+[/|]\s+/)[0].replace(/^.*:\s+/, '').split(/\s+(?:vs\.?|v\.s\.|versus)\s+/i);
    if (sides.length !== 2) continue;
    for (const side of sides) {
      // "- Final" or "(Superfight)" after the name; hyphens inside names (Jean-Jacques) stay.
      const name = side.replace(/\s+[-–]\s.*$|\s*\(.*$/, '').trim();
      if (name.length < 3 || name.length > 40) continue;
      const key = normalize(name);
      const entry = counts.get(key) ?? { name, fights: 0 };
      entry.fights++;
      counts.set(key, entry);
    }
  }
  return [...counts.values()]
    .sort((a, b) => b.fights - a.fights || a.name.localeCompare(b.name, 'pt-BR'))
    .slice(0, limit)
    .map(a => a.name);
}

/** Channels of the fights, in alphabetical order. */
export function channelsOf(videos: FightVideo[]): string[] {
  return [...new Set(videos.map(v => v.channel).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'pt-BR'));
}

/** First ISO date inside the period ("2025-10-19" for the last year on 2026-10-19), or null for any date. */
function periodStart(period: ResultPeriod, today: Date): string | null {
  const years = period === '1a' ? 1 : period === '2a' ? 2 : 0;
  if (!years) return null;
  const start = new Date(Date.UTC(today.getFullYear() - years, today.getMonth(), today.getDate()));
  return start.toISOString().slice(0, 10);
}
