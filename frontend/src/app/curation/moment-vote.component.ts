import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Observable, catchError, map, of, switchMap, tap } from 'rxjs';
import { VoteService } from './vote.service';

/**
 * "Does this moment show the technique?" with yes/no buttons. Shown in the player, so only people who
 * opened the video at that moment vote. Clicking the chosen answer again removes the vote.
 */
@Component({
  selector: 'app-moment-vote',
  templateUrl: './moment-vote.component.html',
  styleUrl: './moment-vote.component.scss'
})
export class MomentVoteComponent {
  private votes = inject(VoteService);

  momentId = input.required<string>();
  /** Emits the answer once the API saved it (not when a vote is removed), so the player can move on. */
  voted = output<boolean>();

  /** The visitor's vote on the current moment; null when none. */
  useful = signal<boolean | null>(null);
  busy = signal(false);
  message = signal('');

  constructor() {
    toObservable(this.momentId).pipe(
      tap(() => {
        this.useful.set(null);
        this.message.set('');
      }),
      switchMap(id => this.votes.mine(id).pipe(catchError(() => of({ useful: null })))),
      takeUntilDestroyed()
    ).subscribe(vote => this.useful.set(vote.useful));
  }

  answer(useful: boolean): void {
    const id = this.momentId();
    const request: Observable<boolean | null> = this.useful() === useful
      ? this.votes.remove(id).pipe(map(() => null))
      : this.votes.vote(id, useful).pipe(map(vote => vote.useful));

    this.busy.set(true);
    request.subscribe({
      next: vote => {
        this.useful.set(vote);
        this.message.set(vote === null ? 'Avaliação removida.' : 'Obrigado pela avaliação!');
        this.busy.set(false);
        if (vote !== null) this.voted.emit(vote);
      },
      error: (error: HttpErrorResponse) => {
        this.message.set(error.status === 429
          ? 'Muitas avaliações em pouco tempo. Tente de novo daqui a pouco.'
          : 'Não foi possível registrar a avaliação.');
        this.busy.set(false);
      }
    });
  }
}
