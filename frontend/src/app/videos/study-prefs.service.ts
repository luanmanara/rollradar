import { Injectable, signal } from '@angular/core';

/** Seconds the player can start before the mention. */
export const LEAD_IN_OPTIONS = [5, 10, 15] as const;

const LEAD_IN_KEY = 'rollradar.leadIn';
const IN_SEQUENCE_KEY = 'rollradar.inSequence';

/** Player preferences kept in this browser only; without storage they last until the page is closed. */
@Injectable({ providedIn: 'root' })
export class StudyPrefsService {
  /** Seconds played before the mention, so the viewer sees the setup of the technique. */
  readonly leadIn = signal<number>(readLeadIn());
  /** Plays the moments one after the other: each clip stops a little after the mention and the next one starts. */
  readonly inSequence = signal<boolean>(read(IN_SEQUENCE_KEY) === '1');

  setLeadIn(seconds: number): void {
    this.leadIn.set(seconds);
    write(LEAD_IN_KEY, String(seconds));
  }

  setInSequence(on: boolean): void {
    this.inSequence.set(on);
    write(IN_SEQUENCE_KEY, on ? '1' : '0');
  }
}

function readLeadIn(): number {
  const stored = Number(read(LEAD_IN_KEY));
  return (LEAD_IN_OPTIONS as readonly number[]).includes(stored) ? stored : LEAD_IN_OPTIONS[0];
}

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    // No storage: use the default.
    return null;
  }
}

function write(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Storage blocked (private window, disabled site data): keep the choice for this visit only.
  }
}
