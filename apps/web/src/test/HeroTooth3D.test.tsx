import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import HeroTooth3D from '../modules/public/HeroTooth3D';
const mock=vi.hoisted(()=>({rotate:vi.fn(),setPaused:vi.fn(),dispose:vi.fn(),create:vi.fn()}));
vi.mock('../modules/public/toothScene',()=>({createToothScene:mock.create}));
beforeEach(()=>{vi.clearAllMocks();mock.create.mockReturnValue(mock);});
it('loads without blocking and exposes accessible controls',async()=>{
 render(<HeroTooth3D/>);expect(screen.getByRole('status').textContent).toContain('Preparando');
 expect(await screen.findByRole('button',{name:'Pausar giro'})).toBeTruthy();
 expect(screen.getByRole('group').tabIndex).toBe(0);
});
it('supports keyboard rotation and pause/resume',async()=>{
 render(<HeroTooth3D/>);await screen.findByRole('button',{name:'Pausar giro'});
 fireEvent.keyDown(screen.getByRole('group'),{key:'ArrowRight'});expect(mock.rotate).toHaveBeenCalledWith(0,.15);
 await userEvent.click(screen.getByRole('button',{name:'Pausar giro'}));expect(mock.setPaused).toHaveBeenLastCalledWith(true);
 await userEvent.click(screen.getByRole('button',{name:'Reanudar giro'}));expect(mock.setPaused).toHaveBeenLastCalledWith(false);
});
it('shows fallback on renderer failure',async()=>{
 mock.create.mockImplementationOnce(()=>{throw new Error('No WebGL');});render(<HeroTooth3D/>);
 expect(await screen.findByText('Cuidado profesional, en cada detalle.')).toBeTruthy();expect(screen.queryByRole('button')).toBeNull();
});
it('releases resources on unmount',async()=>{
 const view=render(<HeroTooth3D/>);await screen.findByRole('button',{name:'Pausar giro'});view.unmount();expect(mock.dispose).toHaveBeenCalledOnce();
});
it('handles context loss with fallback and disposal',async()=>{
 render(<HeroTooth3D/>);await screen.findByRole('button',{name:'Pausar giro'});
 mock.create.mock.calls[0][1]();await waitFor(()=>expect(screen.getByRole('status').textContent).toContain('Cuidado profesional'));expect(mock.dispose).toHaveBeenCalledOnce();
});
