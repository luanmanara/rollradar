import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  FocusPoint, FocusReview, Partner, RecentPositions, TrainingFilter, TrainingPage, TrainingSession, TrainingSessionInput
} from './training.model';

/** The signed-in user's training diary. Every call answers 401 without a session. */
@Injectable({ providedIn: 'root' })
export class TrainingService {
  private http = inject(HttpClient);

  list(filter: TrainingFilter = {}): Observable<TrainingPage> {
    let params = new HttpParams();
    if (filter.attire) params = params.set('roupa', filter.attire);
    if (filter.format) params = params.set('formato', filter.format);
    if (filter.partnerId) params = params.set('parceiro', filter.partnerId);
    if (filter.q?.trim()) params = params.set('q', filter.q.trim());
    if (filter.page && filter.page > 1) params = params.set('pagina', filter.page);
    if (filter.pageSize) params = params.set('tamanho', filter.pageSize);
    return this.http.get<TrainingPage>('/api/treinos', { params });
  }

  get(id: string): Observable<TrainingSession> {
    return this.http.get<TrainingSession>(`/api/treinos/${encodeURIComponent(id)}`);
  }

  /** Safe to repeat: the API answers 200 with the stored session when the id is already there. */
  create(session: TrainingSessionInput): Observable<TrainingSession> {
    return this.http.post<TrainingSession>('/api/treinos', session);
  }

  update(session: TrainingSessionInput): Observable<TrainingSession> {
    return this.http.put<TrainingSession>(`/api/treinos/${encodeURIComponent(session.id)}`, session);
  }

  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/treinos/${encodeURIComponent(id)}`);
  }

  /** The open point to fix, or null (204). */
  focus(): Observable<FocusPoint | null> {
    return this.http.get<FocusPoint | null>('/api/treinos/foco', { observe: 'response' }).pipe(map(r => r.body ?? null));
  }

  reviewFocus(id: number, review: FocusReview, sessionId: string | null = null): Observable<void> {
    return this.http.put<void>(`/api/treinos/foco/${id}`, { review, sessionId });
  }

  partners(q = ''): Observable<Partner[]> {
    const params = q.trim() ? new HttpParams().set('q', q.trim()) : undefined;
    return this.http.get<Partner[]>('/api/parceiros', { params });
  }

  recentPositions(): Observable<RecentPositions> {
    return this.http.get<RecentPositions>('/api/mapa/posicoes-recentes');
  }
}
