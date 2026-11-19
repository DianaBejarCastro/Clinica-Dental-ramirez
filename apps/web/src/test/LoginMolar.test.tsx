import { expect, it } from 'vitest';
import { fireEvent, render } from '@testing-library/react';
import LoginMolar, { loginMolarSources } from '../modules/auth/LoginMolar';

it('keeps a request-free fallback when the authorized asset is absent', () => {
  const { container } = render(<svg><LoginMolar sources={{}}><path data-testid="fallback" /></LoginMolar></svg>);
  expect(container.querySelector('img')).toBeNull();
  expect(container.querySelector('[data-login-molar-fallback]')?.getAttribute('visibility')).toBe('visible');
});
it('reserves the existing footprint and reveals the image only after loading', () => {
  const { container } = render(<svg><LoginMolar sources={{ webp: '/molar.webp', avif: '/molar.avif', mobile: '/mobile.webp' }}><path /></LoginMolar></svg>);
  const img = container.querySelector('img')!;
  expect(img.alt).toBe(''); expect(img.getAttribute('loading')).toBe('eager');
  expect(img.getAttribute('fetchpriority')).toBe('high');
  expect(container.querySelector('foreignObject')?.getAttribute('height')).toBe('260');
  expect(container.querySelectorAll('source')).toHaveLength(2);
  expect(img.style.opacity).toBe('0');
  fireEvent.load(img);
  expect(img.style.opacity).toBe('1');
  expect(container.querySelector('[data-login-molar-fallback]')?.getAttribute('visibility')).toBe('hidden');
});
it('keeps the SVG tooth on image failure without propagating an error', () => {
  const { container } = render(<svg><LoginMolar sources={{ webp: '/molar.webp' }}><path /></LoginMolar></svg>);
  fireEvent.error(container.querySelector('img')!);
  expect(container.querySelector('img')).toBeNull();
  expect(container.querySelector('[data-login-molar-fallback]')?.getAttribute('visibility')).toBe('visible');
});
it('only opts into the image pipeline when a WebP source exists', () => {
  expect(typeof loginMolarSources).toBe('object');
  const { container } = render(<svg><LoginMolar sources={{ avif: '/molar.avif' }}><path /></LoginMolar></svg>);
  expect(container.querySelector('img')).toBeNull();
});
