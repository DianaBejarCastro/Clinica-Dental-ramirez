import { beforeEach, afterEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import ClinicGallery from '../modules/admin/clinic/ClinicGallery';
import MediaGallery from '../modules/public/MediaGallery';
const api=vi.hoisted(()=>vi.fn());
vi.mock('../services/api',()=>({api,apiUrl:(p:string)=>p,getSession:()=>null}));
vi.mock('../modules/admin/shared',async original=>({...await original<object>(),usePermissions:()=>()=>true}));
const image={id:'image',mediaType:'IMAGE',title:'Clínica',altText:'Nuestra clínica',width:640,height:400,isActive:true,sortOrder:0};
const video={...image,id:'video',mediaType:'VIDEO_UPLOAD',title:'Recorrido'};
const tiktok={...image,id:'tiktok',mediaType:'TIKTOK',title:'Consejos',externalUrl:'https://www.tiktok.com/@clinic/video/1234567890123'};
beforeEach(()=>{api.mockReset().mockResolvedValue({items:[image,video,tiktok],total:9,videoUploadAvailable:false});vi.stubGlobal('fetch',vi.fn(async()=>({ok:true,json:async()=>({items:[image,video,tiktok],total:9}),blob:async()=>new Blob()})));URL.createObjectURL=vi.fn(()=> 'blob:test');URL.revokeObjectURL=vi.fn();});
afterEach(()=>vi.unstubAllGlobals());
it('public filters request bounded pages and display image, video and TikTok',async()=>{
 render(<MediaGallery/>);await screen.findByRole('button',{name:'Abrir Clínica'});
 expect(screen.getByRole('link',{name:'Ver Consejos en TikTok'}).getAttribute('rel')).toBe('noopener noreferrer');
 fireEvent.click(screen.getByRole('button',{name:'Videos'}));await waitFor(()=>expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('type=VIDEO_UPLOAD&page=1&pageSize=7'),expect.anything()));
 await screen.findByRole('button',{name:'Abrir Recorrido'});fireEvent.click(screen.getByRole('button',{name:'Abrir Recorrido'}));
 expect(document.querySelector<HTMLVideoElement>('dialog video')?.preload).toBe('metadata');expect(document.querySelector<HTMLVideoElement>('dialog video')?.autoplay).toBe(false);
 fireEvent.click(screen.getByLabelText('Cerrar'));expect(document.querySelector<HTMLVideoElement>('dialog video')).toBeNull();
 fireEvent.click(screen.getByLabelText('Página siguiente'));await waitFor(()=>expect(fetch).toHaveBeenLastCalledWith(expect.stringContaining('page=2'),expect.anything()));
});
it('admin offers type forms, blocks unconfigured video storage and confirms deletion',async()=>{
 render(<ClinicGallery/>);await screen.findByText('Clínica');
 fireEvent.click(screen.getByText('↑ Subir contenido'));expect(screen.getByLabelText('Texto alternativo')).toBeTruthy();
 fireEvent.change(screen.getByLabelText('Tipo de contenido'),{target:{value:'VIDEO_UPLOAD'}});expect((screen.getByText('Guardar contenido') as HTMLButtonElement).disabled).toBe(true);
 fireEvent.change(screen.getByLabelText('Tipo de contenido'),{target:{value:'TIKTOK'}});expect(screen.getByLabelText('Enlace de TikTok')).toBeTruthy();
 fireEvent.change(screen.getByLabelText('Enlace de TikTok'),{target:{value:'https://www.tiktok.com/@clinic/video/1234567890123'}});
 fireEvent.click(screen.getByText('Guardar contenido'));await waitFor(()=>expect(api).toHaveBeenCalledWith('/api/admin/clinic/media','POST',expect.any(FormData)));
 await waitFor(()=>expect(screen.queryByLabelText('Tipo de contenido')).toBeNull());
 fireEvent.click(screen.getAllByText('Eliminar')[0]);expect(api).not.toHaveBeenCalledWith(expect.anything(),'DELETE');
 fireEvent.click(screen.getByText('Confirmar eliminación'));await waitFor(()=>expect(api).toHaveBeenCalledWith('/api/admin/clinic/media/image','DELETE'));
});
it('admin visibility, order and pagination use existing item requests',async()=>{
 render(<ClinicGallery/>);await screen.findByText('Clínica');fireEvent.click(screen.getAllByText('Ocultar')[0]);await waitFor(()=>expect(api).toHaveBeenCalledWith('/api/admin/clinic/media/image','PUT',expect.objectContaining({isActive:false})));
 fireEvent.click(screen.getAllByText('Editar / ordenar')[0]);fireEvent.change(screen.getByLabelText('Orden'),{target:{value:'4'}});fireEvent.click(screen.getByText('Guardar contenido'));await waitFor(()=>expect(api).toHaveBeenCalledWith('/api/admin/clinic/media/image','PUT',expect.objectContaining({sortOrder:4})));
});
