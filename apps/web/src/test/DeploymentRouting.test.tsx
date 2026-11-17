import { afterEach, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { useRoute } from '../router/Router';
function Route() { return <p>{useRoute()}</p>; }
afterEach(() => window.history.replaceState({}, '', '/'));
it('reads a direct patient URL and query after an SPA fallback', () => {
  window.history.replaceState({}, '', '/patient/book?doctor=example');
  render(<Route />); expect(screen.getByText('/patient/book?doctor=example')).toBeTruthy();
});
it('preserves hash routing precedence over direct paths', () => {
  window.history.replaceState({}, '', '/login#/doctor/appointments');
  render(<Route />); expect(screen.getByText('/doctor/appointments')).toBeTruthy();
});
