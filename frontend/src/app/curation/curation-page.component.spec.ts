import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CurationItem } from './curation.model';
import { CurationPageComponent } from './curation-page.component';
import { CURATOR_KEY_HEADER } from './curation.service';

const item: CurationItem = {
  video: {
    id: 'a1', title: 'Langaker vs O\'Flanagan', url: 'https://www.youtube.com/watch?v=a1', thumbnailUrl: '',
    channel: 'FloGrappling', uploadDate: '2026-10-09', moments: []
  },
  moment: { id: 'a1.armbar.66', technique: 'armbar', startSeconds: 66, snippet: 'goes for the armbar', techniqueId: 'armbar' },
  votes: { useful: 1, notUseful: 3, total: 4 },
  status: 'pendente'
};

describe('CurationPageComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function signIn() {
    const fixture = TestBed.createComponent(CurationPageComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    (el.querySelector('input[type="password"]') as HTMLInputElement).value = 'segredo';
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    const check = http.expectOne('/api/curation');
    expect(check.request.headers.get(CURATOR_KEY_HEADER)).toBe('segredo');
    check.flush(null, { status: 204, statusText: 'No Content' });
    return fixture;
  }

  it('says when the key is wrong', async () => {
    const fixture = TestBed.createComponent(CurationPageComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    (el.querySelector('input[type="password"]') as HTMLInputElement).value = 'errada';
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    http.expectOne('/api/curation').flush('', { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Chave incorreta');
  });

  it('lists the pending moments with their votes and marks one as bad', async () => {
    const fixture = await signIn();
    const queue = http.expectOne(r => r.url === '/api/curation/queue');
    expect(queue.request.params.get('status')).toBe('pendente');
    expect(queue.request.headers.get(CURATOR_KEY_HEADER)).toBe('segredo');
    queue.flush([item]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('.item h3')?.textContent).toBe('Langaker vs O\'Flanagan');
    expect(el.querySelector('.votes')?.textContent).toContain('👎 3');

    (el.querySelector('.mark--bad') as HTMLButtonElement).click();
    const put = http.expectOne('/api/curation/moments/a1.armbar.66/status');
    expect(put.request.body).toEqual({ status: 'ruim' });
    put.flush(null);
    await fixture.whenStable();

    expect(el.querySelector('.item')?.classList).toContain('item--bad');
    expect(el.querySelector('.mark--bad')?.getAttribute('aria-pressed')).toBe('true');
  });

  it('filters by technique and status', async () => {
    const fixture = await signIn();
    http.expectOne(r => r.url === '/api/curation/queue').flush([]);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    (el.querySelector('input[type="search"]') as HTMLInputElement).value = 'armbar';
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    expect(http.expectOne(r => r.url === '/api/curation/queue').request.params.get('technique')).toBe('armbar');

    ([...el.querySelectorAll('.tab')] as HTMLButtonElement[]).find(t => t.textContent === 'Ruins')!.click();
    const queue = http.expectOne(r => r.url === '/api/curation/queue');
    expect(queue.request.params.get('status')).toBe('ruim');
    expect(queue.request.params.get('technique')).toBe('armbar');
  });
});
