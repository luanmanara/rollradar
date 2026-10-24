import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { Router, provideRouter } from '@angular/router';
import { UserMenuComponent } from './user-menu.component';

@Component({ template: '' })
class EmptyPage {}

describe('UserMenuComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: '**', component: EmptyPage }])]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(me: object | null) {
    const fixture = TestBed.createComponent(UserMenuComponent);
    fixture.detectChanges();
    const request = http.expectOne('/api/eu');
    if (me) request.flush(me);
    else request.flush(null, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();
    return fixture;
  }

  it('shows nothing until the API answers', () => {
    const fixture = TestBed.createComponent(UserMenuComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe('');
    http.expectOne('/api/eu').flush(null, { status: 401, statusText: 'Unauthorized' });
  });

  it('offers the Google sign-in, coming back to the current page', async () => {
    await TestBed.inject(Router).navigateByUrl('/tecnica/armbar?tecnica=armlock');
    const fixture = await render(null);
    const link = (fixture.nativeElement as HTMLElement).querySelector('a') as HTMLAnchorElement;

    expect(link.textContent).toContain('Entrar com Google');
    expect(link.getAttribute('href')).toBe('/api/auth/login?returnUrl=%2Ftecnica%2Farmbar%3Ftecnica%3Darmlock');
  });

  it('shows the user\'s name and signs out', async () => {
    const fixture = await render({ id: 'u1', email: 'ana@exemplo.com', nome: 'Ana Souza' });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.user__name')?.textContent).toContain('Ana Souza');
    expect(el.querySelector('.user__name')?.getAttribute('href')).toBe('/conta');
    const diary = el.querySelector('.user__btn--diary');
    expect(diary?.textContent).toContain('Diário');
    expect(diary?.getAttribute('href')).toBe('/treinos');

    (el.querySelector('button') as HTMLButtonElement).click();
    http.expectOne(r => r.method === 'POST' && r.url === '/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(el.querySelector('.user__name')).toBeNull();
    expect(el.querySelector('a')?.textContent).toContain('Entrar com Google');
  });

  it('says so when signing out fails', async () => {
    const fixture = await render({ id: 'u1', email: 'ana@exemplo.com', nome: null });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.user__name')?.textContent).toContain('ana@exemplo.com');

    (el.querySelector('button') as HTMLButtonElement).click();
    http.expectOne('/api/auth/logout').flush(null, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Não foi possível sair');
    expect(el.querySelector('.user__name')).not.toBeNull();
  });
});
