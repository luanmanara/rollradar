import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { AthleteSummary, SearchResult, TechniqueSummary } from './fight-video.model';

@Injectable({ providedIn: 'root' })
export class VideoService {
  private http = inject(HttpClient);

  /**
   * The technique can be typed in Portuguese or English; the API translates it to the English caption terms.
   * Several techniques are searched together.
   */
  search(technique: string | string[], athlete: string): Observable<SearchResult> {
    let params = new HttpParams();
    for (const t of [technique].flat().filter(Boolean)) params = params.append('technique', t);
    if (athlete) params = params.set('athlete', athlete);
    return this.http.get<SearchResult>('/api/videos', { params });
  }

  /** Asked once per visit: the home page and the technique pages all use it. */
  private catalog$: Observable<TechniqueSummary[]> | null = null;

  techniques(): Observable<TechniqueSummary[]> {
    this.catalog$ ??= this.http.get<TechniqueSummary[]>('/api/videos/techniques').pipe(shareReplay(1));
    return this.catalog$;
  }

  private athletes$: Observable<AthleteSummary[]> | null = null;

  /** Athletes found in the fight titles, asked once per visit for the search box. */
  athletes(): Observable<AthleteSummary[]> {
    this.athletes$ ??= this.http.get<AthleteSummary[]>('/api/videos/athletes').pipe(shareReplay(1));
    return this.athletes$;
  }
}
