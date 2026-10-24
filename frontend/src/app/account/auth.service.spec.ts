import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  it('loads the signed-in user', () => {
    auth.load();
    expect(auth.loaded()).toBe(false);

    http.expectOne('/api/eu').flush({ id: 'u1', email: 'ana@exemplo.com', nome: 'Ana' });

    expect(auth.user()).toEqual({ id: 'u1', email: 'ana@exemplo.com', nome: 'Ana' });
    expect(auth.loaded()).toBe(true);
  });

  it('treats 401 as nobody signed in', () => {
    auth.load();
    http.expectOne('/api/eu').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.user()).toBeNull();
    expect(auth.loaded()).toBe(true);
  });

  it('builds the login address with the page to come back to', () => {
    expect(auth.loginUrl('/tecnica/armbar?x=1')).toBe('/api/auth/login?returnUrl=%2Ftecnica%2Farmbar%3Fx%3D1');
  });

  it('forgets the user on logout, also when the session had already ended', () => {
    for (const status of [204, 401]) {
      auth.user.set({ id: 'u1', email: null, nome: 'Ana' });
      let done = false;
      auth.logout().subscribe(() => (done = true));

      const request = http.expectOne('/api/auth/logout');
      expect(request.request.method).toBe('POST');
      request.flush(null, { status, statusText: '' });

      expect(done).toBe(true);
      expect(auth.user()).toBeNull();
    }
  });

  it('keeps the user when logout fails', () => {
    auth.user.set({ id: 'u1', email: null, nome: 'Ana' });
    let failed = false;
    auth.logout().subscribe({ error: () => (failed = true) });

    http.expectOne('/api/auth/logout').flush(null, { status: 500, statusText: 'Server Error' });

    expect(failed).toBe(true);
    expect(auth.user()?.nome).toBe('Ana');
  });
});
