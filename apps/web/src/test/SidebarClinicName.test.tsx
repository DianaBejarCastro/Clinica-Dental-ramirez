import { afterEach, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import SidebarClinicName, { notifyClinicNameChanged } from '../layouts/SidebarClinicName';
afterEach(()=>vi.unstubAllGlobals());
it('uses a generic fallback while loading, then the configured capitalization',async()=>{
 let resolve!:(value:Response)=>void;vi.stubGlobal('fetch',vi.fn(()=>new Promise<Response>(r=>{resolve=r;})));
 render(<SidebarClinicName/>);expect(screen.getByText('Clínica')).toBeTruthy();expect(screen.queryByText(/RAMÍREZ/)).toBeNull();
 await act(async()=>resolve(new Response(JSON.stringify({name:'Centro dental Horizonte'}))));expect(await screen.findByText('Centro dental Horizonte')).toBeTruthy();
});
it('refreshes the name after clinic save without remounting',async()=>{
 const fetch=vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({name:'Nombre anterior'}))).mockResolvedValueOnce(new Response(JSON.stringify({name:'Nuevo nombre'})));vi.stubGlobal('fetch',fetch);
 render(<SidebarClinicName/>);await screen.findByText('Nombre anterior');act(()=>notifyClinicNameChanged());expect(await screen.findByText('Nuevo nombre')).toBeTruthy();expect(fetch).toHaveBeenCalledTimes(2);
});
it.each([404,500])('retains fallback for HTTP %s',async status=>{
 vi.stubGlobal('fetch',vi.fn().mockResolvedValue(new Response('{}',{status})));await act(async()=>{render(<SidebarClinicName/>);});expect(screen.getByText('Clínica')).toBeTruthy();
});
it('cancels pending read on unmount',()=>{
 const fetch=vi.fn(()=>new Promise<Response>(()=>{}));vi.stubGlobal('fetch',fetch);const view=render(<SidebarClinicName/>);view.unmount();expect((fetch.mock.calls as unknown as [string,RequestInit][])[0][1].signal?.aborted).toBe(true);
});
