import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { CurationItem, CurationStatus } from './curation.model';

/** Header the API checks against Curation:AdminKey. */
export const CURATOR_KEY_HEADER = 'X-Curator-Key';
const STORAGE_KEY = 'rollradar.curatorKey';

/** Filter of the curation queue: one status, or every status. */
export type StatusFilter = CurationStatus | 'todos';

@Injectable({ providedIn: 'root' })
export class CurationService {
  private http = inject(HttpClient);

  /** Curator key for this browser tab, kept until the tab is closed. */
  key = signal(read());

  setKey(key: string | null): void {
    this.key.set(key);
    try {
      if (key) sessionStorage.setItem(STORAGE_KEY, key);
      else sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage may be blocked; the key then lasts until the page reloads.
    }
  }

  /** Completes when the key is right; fails with 401 when wrong and 403 when curation is closed. */
  check(key: string): Observable<void> {
    return this.http.get<void>('/api/curation', { headers: headers(key) });
  }

  queue(technique: string, status: StatusFilter): Observable<CurationItem[]> {
    let params = new HttpParams().set('status', status);
    if (technique) params = params.set('technique', technique);
    return this.http.get<CurationItem[]>('/api/curation/queue', { params, headers: headers(this.key() ?? '') });
  }

  setStatus(momentId: string, status: CurationStatus): Observable<void> {
    return this.http.put<void>(`/api/curation/moments/${encodeURIComponent(momentId)}/status`, { status },
      { headers: headers(this.key() ?? '') });
  }
}

function headers(key: string): HttpHeaders {
  return new HttpHeaders({ [CURATOR_KEY_HEADER]: key });
}

function read(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}
