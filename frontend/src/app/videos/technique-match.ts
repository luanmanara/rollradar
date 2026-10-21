import { AthleteSummary, TechniqueSummary } from './fight-video.model';

/** Lower case, no accents, hyphens as spaces: "Mata-leão" -> "mata leao". */
export function normalize(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/-/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/** Every name a technique can be typed as: Portuguese, each English alternative and the aliases. */
export function techniqueNames(t: TechniqueSummary): string[] {
  return [t.portuguese, ...t.english.split(' / '), ...(t.aliases ?? [])].filter(Boolean);
}

/** Name used to search for a technique: the Portuguese one when the catalog has it. */
export function searchName(t: TechniqueSummary): string {
  return t.portuguese || t.english;
}

/** "Chave de braço (Armbar)", or just the name when both languages use the same one. */
export function techniqueLabel(t: TechniqueSummary): string {
  if (!t.portuguese || normalize(t.portuguese) === normalize(t.english)) return t.english;
  return `${t.portuguese} (${t.english})`;
}

/** Every name an athlete can be typed as: the name and the catalog aliases. */
export function athleteNames(a: AthleteSummary): string[] {
  return [a.name, ...(a.aliases ?? [])].filter(Boolean);
}

/** Athletes shown in the search box suggestions. */
export const ATHLETE_SUGGESTIONS = 4;

/**
 * Athletes whose name or alias has what was typed (two letters at least), ignoring case and accents: names starting
 * with it first, then a word starting with it, then containing it; ties go to athletes with more fights.
 */
export function matchAthletes(query: string, athletes: AthleteSummary[], limit = ATHLETE_SUGGESTIONS): AthleteSummary[] {
  const q = normalize(query);
  if (q.length < 2) return [];
  return athletes
    .map(a => {
      const ranks = athleteNames(a).map(normalize)
        .map(name => name.startsWith(q) ? 0 : name.includes(' ' + q) ? 1 : name.includes(q) ? 2 : -1)
        .filter(rank => rank >= 0);
      return { a, rank: ranks.length ? Math.min(...ranks) : -1 };
    })
    .filter(x => x.rank >= 0)
    .sort((x, y) => x.rank - y.rank || y.a.fights - x.a.fights)
    .slice(0, limit)
    .map(x => x.a);
}

/**
 * Techniques matching what was typed so far, best first: exact name, then names starting with the
 * text, then a word starting with it, then containing it. Ties go to techniques with more videos.
 */
export function matchTechniques(query: string, techniques: TechniqueSummary[], limit = 8): TechniqueSummary[] {
  const q = normalize(query);
  if (!q) return [];

  const scored = techniques
    .map(t => ({ t, score: Math.min(...techniqueNames(t).map(name => score(normalize(name), q))) }))
    .filter(x => x.score < Infinity);

  scored.sort((a, b) =>
    a.score - b.score ||
    b.t.videoCount - a.t.videoCount ||
    searchName(a.t).localeCompare(searchName(b.t), 'pt-BR'));

  return scored.slice(0, limit).map(x => x.t);
}

function score(name: string, q: string): number {
  if (name === q) return 0;
  if (name.startsWith(q)) return 1;
  if (` ${name}`.includes(` ${q}`)) return 2;
  if (name.includes(q)) return 3;
  return Infinity;
}

/**
 * Techniques whose names are a few typos away from the query ("triangolo" -> Triangle), for when
 * matchTechniques finds nothing. Closest first, then the ones with more videos.
 */
export function similarTechniques(query: string, techniques: TechniqueSummary[], limit = 3): TechniqueSummary[] {
  const q = normalize(query);
  if (q.length < 3) return [];
  const allowed = Math.max(1, Math.floor(q.length / 4));
  const multiWord = q.includes(' ');

  return techniques
    .map(t => {
      const names = techniqueNames(t).map(normalize);
      // A single typed word is also compared with each word of the names: "kimora" -> "kimura lock".
      const candidates = multiWord ? names : [...names, ...names.flatMap(name => name.split(' '))];
      return { t, distance: Math.min(...candidates.map(name => editDistance(q, name))) };
    })
    .filter(x => x.distance <= allowed)
    .sort((a, b) => a.distance - b.distance || b.t.videoCount - a.t.videoCount)
    .slice(0, limit)
    .map(x => x.t);
}

/** Levenshtein distance: insertions, deletions and substitutions to turn one text into the other. */
function editDistance(a: string, b: string): number {
  if (Math.abs(a.length - b.length) > 3) return Infinity;
  let previous = Array.from({ length: b.length + 1 }, (_, j) => j);
  for (let i = 1; i <= a.length; i++) {
    const current = [i];
    for (let j = 1; j <= b.length; j++) {
      current[j] = Math.min(previous[j] + 1, current[j - 1] + 1, previous[j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1));
    }
    previous = current;
  }
  return previous[b.length];
}
