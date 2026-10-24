import { ATTIRES, FORMATS, durationLabel, label } from './training-labels';
import { Attire, TrainingFormat } from './training.model';

/** What the summaries read: a stored session or one still waiting in the outbox. */
interface SessionLike {
  attire: Attire;
  format: TrainingFormat;
  durationMinutes: number;
  rolls: { finishes: { byMe: boolean }[] }[];
}

/** "Aula · Com kimono · 1h30". */
export function sessionLine(s: SessionLike): string {
  return [label(FORMATS, s.format), label(ATTIRES, s.attire), durationLabel(s.durationMinutes)].join(' · ');
}

/** "3 rolas · 2 finalizações · 4 vezes finalizado"; empty without rolls. */
export function rollsLine(s: SessionLike): string {
  const count = s.rolls.length;
  if (count === 0) return '';
  const finishes = s.rolls.flatMap(r => r.finishes);
  const wins = finishes.filter(f => f.byMe).length;
  const losses = finishes.length - wins;
  const parts = [`${count} ${count === 1 ? 'rola' : 'rolas'}`];
  if (wins) parts.push(`${wins} ${wins === 1 ? 'finalização' : 'finalizações'}`);
  if (losses) parts.push(`${losses} ${losses === 1 ? 'vez finalizado' : 'vezes finalizado'}`);
  return parts.join(' · ');
}
