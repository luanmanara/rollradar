import { dayLabel, durationLabel, finishesLabel, today, weekLabel, weekStart } from './training-labels';
import { rollsLine, sessionLine } from './training-summary';
import { TrainingSession } from './training.model';

describe('training labels', () => {
  it('starts weeks on Monday', () => {
    expect(weekStart('2026-10-23')).toBe('2026-10-19'); // Friday
    expect(weekStart('2026-10-19')).toBe('2026-10-19'); // Monday
    expect(weekStart('2026-10-25')).toBe('2026-10-19'); // Sunday
    expect(weekLabel('2026-10-19')).toBe('Semana de 19/10/2026');
  });

  it('shows the day without the time zone moving it', () => {
    expect(dayLabel('2026-10-21')).toMatch(/^quarta, 21\/10$/);
    expect(today(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05');
  });

  it('shows lengths as people say them', () => {
    expect(durationLabel(45)).toBe('45 min');
    expect(durationLabel(60)).toBe('1h');
    expect(durationLabel(90)).toBe('1h30');
  });

  it('sums up a session and its rolls', () => {
    const roll = (...byMe: boolean[]) => ({ finishes: byMe.map(b => ({ byMe: b })) }) as never;
    const s = { format: 'open', attire: 'no_gi', durationMinutes: 75, rolls: [roll(true), roll(true, false), roll(), roll()] } as unknown as TrainingSession;

    expect(sessionLine(s)).toBe('Open mat · Sem kimono · 1h15');
    expect(rollsLine(s)).toBe('4 rolas · 2 finalizações · 1 vez finalizado');
    expect(rollsLine({ ...s, rolls: [] })).toBe('');
    expect(sessionLine({ ...s, attire: 'gi' })).toBe('Open mat · Com kimono · 1h15');
  });

  it('sums up the submissions of a roll', () => {
    expect(finishesLabel([])).toBe('Ninguém finalizou');
    expect(finishesLabel([{ byMe: true }, { byMe: true }])).toBe('Finalizei 2');
    expect(finishesLabel([{ byMe: false }, { byMe: true }, { byMe: false }, { byMe: false }])).toBe('Finalizei 1 · Fui finalizado 3');
  });
});
