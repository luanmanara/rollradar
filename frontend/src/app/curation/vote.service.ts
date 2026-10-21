import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MyVote } from './curation.model';

/** Anonymous votes: the API keeps the voter in a signed cookie, so no account is needed. */
@Injectable({ providedIn: 'root' })
export class VoteService {
  private http = inject(HttpClient);

  mine(momentId: string): Observable<MyVote> {
    return this.http.get<MyVote>(this.url(momentId));
  }

  vote(momentId: string, useful: boolean): Observable<MyVote> {
    return this.http.put<MyVote>(this.url(momentId), { useful });
  }

  remove(momentId: string): Observable<void> {
    return this.http.delete<void>(this.url(momentId));
  }

  private url(momentId: string): string {
    return `/api/moments/${encodeURIComponent(momentId)}/vote`;
  }
}
