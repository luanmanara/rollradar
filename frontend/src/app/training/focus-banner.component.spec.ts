import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../account/auth.service';
import { FocusBannerComponent } from './focus-banner.component';

describe('FocusBannerComponent', () => {
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  it('shows nothing to visitors and asks nothing', async () => {
    const fixture = TestBed.createComponent(FocusBannerComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectNone('/api/treinos/foco');
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
  });

  it('shows the open point and takes the answer', async () => {
    auth.user.set({ id: 'u1', email: null, nome: 'Ana' });
    const fixture = TestBed.createComponent(FocusBannerComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne('/api/treinos/foco').flush({ id: 4, text: 'Postura na guarda', originSessionId: 's1', originDate: '2026-10-21', createdAt: '' });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Postura na guarda');
    const answer = [...el.querySelectorAll('button')].find(b => b.textContent?.includes('Em parte'))!;
    answer.click();

    const put = http.expectOne('/api/treinos/foco/4');
    expect(put.request.body).toEqual({ review: 'parcial', sessionId: null });
    put.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne('/api/treinos/foco').flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    expect(el.textContent?.trim()).toBe('');
  });
});
