import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { timeout } from 'rxjs';
import { FieldErrors, TrainingSession, TrainingSessionInput } from './training.model';
import { TrainingService } from './training.service';

const DRAFT_KEY = 'rollradar.training.draft';
const OUTBOX_KEY = 'rollradar.training.outbox';

/** An answer slower than this counts as a failure; sending again is safe (the id is the client's). */
export const SEND_TIMEOUT_MS = 15_000;
/** Waits between attempts; the last one repeats until the session gets through. */
export const RETRY_DELAYS_MS = [2_000, 5_000, 15_000, 30_000, 60_000];
/** How long the form waits for the API before moving on with the session still on its way. */
export const QUICK_ANSWER_MS = 4_000;

/**
 * What became of a recorded session, as the form needs it: saved by the API, still on its way (kept on this device),
 * refused for invalid data, or waiting for a new sign-in.
 */
export type SubmitOutcome = 'saved' | 'pending' | 'refused' | 'signed_out';

/**
 * idle: nothing to send · sending: kept on this device, on its way (or waiting to try again) ·
 * saved: the API has it · error: the API refused it (see `error`).
 */
export type SyncState = 'idle' | 'sending' | 'saved' | 'error';

/**
 * Keeps the diary working on a bad gym connection: the form's draft is kept in this browser on every change, and
 * a recorded session waits in an outbox (also in this browser) until the API confirms it, trying again by itself.
 */
@Injectable({ providedIn: 'root' })
export class TrainingSyncService {
  private api = inject(TrainingService);

  readonly state = signal<SyncState>('idle');
  readonly error = signal('');
  /** Sessions recorded here that the API has not confirmed yet, oldest first. */
  readonly pending = signal<TrainingSessionInput[]>(readJson<TrainingSessionInput[]>(OUTBOX_KEY) ?? []);
  /** The last session the API confirmed. */
  readonly lastSaved = signal<TrainingSession | null>(null);
  /** The session the API refused (invalid data), until the form takes it back. */
  readonly refused = signal<TrainingSessionInput | null>(null);
  /** Why the API refused it, by field. */
  readonly refusedErrors = signal<FieldErrors>({});

  private sending = false;
  private attempt = 0;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;
  /** The form waiting to hear about a session it recorded, by session id. */
  private waiting = new Map<string, (outcome: SubmitOutcome) => void>();

  constructor() {
    try {
      addEventListener('online', () => this.flush());
    } catch {
      // No window (tests, server): retries still run on their timer.
    }
  }

  loadDraft(): TrainingSessionInput | null {
    return readJson<TrainingSessionInput>(DRAFT_KEY);
  }

  saveDraft(draft: TrainingSessionInput): void {
    writeJson(DRAFT_KEY, draft);
  }

  clearDraft(): void {
    writeJson(DRAFT_KEY, null);
  }

  /**
   * Records the session: into the outbox first (so it survives closing the page), then to the API. Resolves with the
   * first answer, or with "pending" when the API takes longer than {@link QUICK_ANSWER_MS} (it keeps trying).
   */
  submit(session: TrainingSessionInput): Promise<SubmitOutcome> {
    this.setPending([...this.pending().filter(p => p.id !== session.id), session]);
    this.clearDraft();
    this.error.set('');
    this.attempt = 0;
    const outcome = new Promise<SubmitOutcome>(resolve => {
      const timer = setTimeout(() => this.settle(session.id, 'pending'), QUICK_ANSWER_MS);
      this.waiting.set(session.id, result => {
        clearTimeout(timer);
        resolve(result);
      });
    });
    // A send already on its way finishes first; this one follows it.
    if (this.sending) this.state.set('sending');
    else this.flush();
    return outcome;
  }

  private settle(id: string, outcome: SubmitOutcome): void {
    this.waiting.get(id)?.(outcome);
    this.waiting.delete(id);
  }

  /** Sends what is waiting now, e.g. when the page opens signed in or the connection comes back. */
  flush(): void {
    const next = this.pending()[0];
    if (!next || this.sending) return;
    this.clearRetry();
    this.sending = true;
    this.state.set('sending');

    this.api.create(next).pipe(timeout(SEND_TIMEOUT_MS)).subscribe({
      next: saved => {
        this.sending = false;
        this.attempt = 0;
        this.remove(next.id);
        this.lastSaved.set(saved);
        this.settle(next.id, 'saved');
        if (this.pending().length > 0) this.flush();
        else this.state.set('saved');
      },
      error: (error: unknown) => {
        this.sending = false;
        const status = error instanceof HttpErrorResponse ? error.status : 0;
        if (status === 400 || status === 409) {
          // Refused for good: back to the draft, so nothing typed is lost (the form offers to reopen it).
          this.remove(next.id);
          this.refused.set(next);
          this.refusedErrors.set(error instanceof HttpErrorResponse && status === 400 ? error.error?.errors ?? {} : {});
          this.saveDraft(next);
          this.settle(next.id, 'refused');
          this.fail(status === 400
            ? 'O treino tem dados inválidos e voltou para o formulário. Confira e salve de novo.'
            : 'Este treino já existe em outra conta.');
        } else if (status === 401) {
          // Kept in the outbox: it goes as soon as the user signs in again.
          this.fail('Sua sessão terminou. Entre de novo: o treino continua guardado neste aparelho.');
          this.settle(next.id, 'signed_out');
        } else {
          this.scheduleRetry();
          this.settle(next.id, 'pending');
        }
      }
    });
  }

  private scheduleRetry(): void {
    const delay = RETRY_DELAYS_MS[Math.min(this.attempt, RETRY_DELAYS_MS.length - 1)];
    this.attempt++;
    this.retryTimer = setTimeout(() => this.flush(), delay);
  }

  private clearRetry(): void {
    if (this.retryTimer !== null) clearTimeout(this.retryTimer);
    this.retryTimer = null;
  }

  private fail(message: string): void {
    this.error.set(message);
    this.state.set('error');
  }

  private remove(id: string): void {
    this.setPending(this.pending().filter(p => p.id !== id));
  }

  private setPending(sessions: TrainingSessionInput[]): void {
    this.pending.set(sessions);
    writeJson(OUTBOX_KEY, sessions.length > 0 ? sessions : null);
  }
}

function readJson<T>(key: string): T | null {
  try {
    const text = localStorage.getItem(key);
    return text ? (JSON.parse(text) as T) : null;
  } catch {
    // No storage, or something unreadable in it: start empty.
    return null;
  }
}

function writeJson(key: string, value: unknown): void {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // Storage blocked (private window, disabled site data): the draft lasts until the page is closed.
  }
}
