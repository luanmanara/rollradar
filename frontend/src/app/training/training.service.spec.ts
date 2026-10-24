import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TrainingService } from './training.service';

describe('TrainingService', () => {
  let http: HttpTestingController;
  let training: TrainingService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    training = TestBed.inject(TrainingService);
  });

  afterEach(() => http.verify());

  it('sends the history filters with the API names, leaving out the empty ones', () => {
    training.list({ attire: 'no_gi', format: '', partnerId: 7, q: '  raspagem ', page: 2, pageSize: 20 }).subscribe();

    const request = http.expectOne(r => r.url === '/api/treinos');
    expect(request.request.params.toString()).toBe('roupa=no_gi&parceiro=7&q=raspagem&pagina=2&tamanho=20');
    request.flush({ items: [], total: 0, page: 2, pageSize: 20 });
  });

  it('reads 204 as no open point to fix', () => {
    let focus: unknown = 'not yet';
    training.focus().subscribe(f => (focus = f));
    http.expectOne('/api/treinos/foco').flush(null, { status: 204, statusText: 'No Content' });
    expect(focus).toBeNull();
  });

  it('answers the point to fix', () => {
    training.reviewFocus(3, 'parcial').subscribe();
    const request = http.expectOne('/api/treinos/foco/3');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ review: 'parcial', sessionId: null });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('edits and deletes by id', () => {
    training.update({ id: 'a b' } as never).subscribe();
    expect(http.expectOne('/api/treinos/a%20b').request.method).toBe('PUT');
    training.remove('x1').subscribe();
    expect(http.expectOne('/api/treinos/x1').request.method).toBe('DELETE');
  });
});
