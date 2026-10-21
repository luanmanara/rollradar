import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { formatDate } from '@angular/common';
import { FormatTimePipe } from '../videos/format-time.pipe';
import { HighlightPipe } from '../videos/highlight.pipe';
import { FightVideo } from '../videos/fight-video.model';
import { VideoModalComponent } from '../videos/video-modal.component';
import { CurationItem, CurationStatus } from './curation.model';
import { CurationService, StatusFilter } from './curation.service';

export const STATUS_FILTERS: { value: StatusFilter; label: string }[] = [
  { value: 'pendente', label: 'Pendentes' },
  { value: 'ruim', label: 'Ruins' },
  { value: 'bom', label: 'Bons' },
  { value: 'todos', label: 'Todos' }
];

/**
 * The curator's page (/curadoria, not linked from the site): moments ordered by the visitors' votes, to mark
 * as good or bad. Needs the curator key set in the API (Curation:AdminKey).
 */
@Component({
  selector: 'app-curation-page',
  imports: [FormatTimePipe, HighlightPipe, VideoModalComponent],
  templateUrl: './curation-page.component.html',
  styleUrl: './curation-page.component.scss'
})
export class CurationPageComponent {
  private curation = inject(CurationService);

  readonly filters = STATUS_FILTERS;

  signedIn = computed(() => !!this.curation.key());
  keyError = signal('');
  technique = signal('');
  status = signal<StatusFilter>('pendente');
  items = signal<CurationItem[] | null>(null);
  loading = signal(false);
  error = signal('');
  selected = signal<CurationItem | null>(null);

  constructor() {
    if (this.signedIn()) this.load();
  }

  signIn(key: string): void {
    key = key.trim();
    if (!key) return;
    this.keyError.set('');
    this.curation.check(key).subscribe({
      next: () => {
        this.curation.setKey(key);
        this.load();
      },
      error: (error: HttpErrorResponse) => this.keyError.set(error.status === 403
        ? 'A curadoria está fechada: configure a chave Curation:AdminKey na API.'
        : error.status === 401 ? 'Chave incorreta.' : 'Não foi possível falar com a API.')
    });
  }

  signOut(): void {
    this.curation.setKey(null);
    this.items.set(null);
  }

  search(technique: string): void {
    this.technique.set(technique.trim());
    this.load();
  }

  filter(status: StatusFilter): void {
    this.status.set(status);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.curation.queue(this.technique(), this.status()).subscribe({
      next: items => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        if (error.status === 401 || error.status === 403) this.signOut();
        else this.error.set('Não foi possível carregar os momentos. A API está rodando?');
      }
    });
  }

  /** Marks the moment; clicking its current verdict again puts it back to pending. */
  mark(item: CurationItem, status: CurationStatus): void {
    const next: CurationStatus = item.status === status ? 'pendente' : status;
    this.curation.setStatus(item.moment.id, next).subscribe({
      // Stays in the list until the next load, so a wrong click can be undone right there.
      next: () => this.items.update(items => (items ?? []).map(i => i === item ? { ...i, status: next } : i)),
      error: () => this.error.set('Não foi possível salvar. Tente de novo.')
    });
  }

  play(item: CurationItem): void {
    this.selected.set(item);
  }

  close(): void {
    this.selected.set(null);
  }

  meta(video: FightVideo): string {
    const parts = [video.channel];
    if (video.uploadDate) parts.push(formatDate(video.uploadDate, 'dd/MM/yyyy', 'en-US'));
    return parts.filter(Boolean).join(' · ');
  }
}
