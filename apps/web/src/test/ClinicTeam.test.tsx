import { beforeEach, afterEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ClinicTeam from '../modules/admin/clinic/ClinicTeam';
import TeamSection from '../modules/public/TeamSection';
const api=vi.hoisted(()=>vi.fn());const permissions=vi.hoisted(()=>({write:true}));
vi.mock('../services/api',()=>({api,apiUrl:(p:string)=>p,getSession:()=>({accessToken:'test'})}));
vi.mock('../modules/auth/AuthProvider',()=>({useAuth:()=>({session:{user:{roles:['ADMINISTRATOR'],permissions:permissions.write?['clinic.view','clinic.update','users.view','users.update']:['clinic.view']}}})}));
const doctor={doctorProfileId:'d1',userId:'u1',firstName:'Juan',lastName:'Ramirez',professionalLicense:'123',specialties:['Cirugía'],photoVersion:null as string|null};
const featured={...doctor,id:'f1',displayOrder:0,isEligible:true};
let selected:typeof featured[],available:typeof doctor[];
beforeEach(()=>{
 permissions.write=true;selected=[];available=[{...doctor}];
 api.mockReset().mockImplementation(async(path:string,method='GET',body?:{displayOrder:number})=>{
  if(path.includes('/profile-photo'))return {};
  if(path.endsWith('/available'))return [...available];
  if(method==='POST'){selected=[{...featured}];available=[];return {id:'f1'};}
  if(method==='DELETE'){selected=[];available=[{...doctor}];return;}
  if(method==='PUT'){selected[0].displayOrder=body!.displayOrder;return;}
  return [...selected];
 });
 vi.stubGlobal('fetch',vi.fn(async()=>({ok:false})));
 Object.defineProperty(URL,'createObjectURL',{configurable:true,value:vi.fn(()=>'blob:photo')});
 Object.defineProperty(URL,'revokeObjectURL',{configurable:true,value:vi.fn()});
 HTMLDialogElement.prototype.showModal=vi.fn(function(this:HTMLDialogElement){this.setAttribute('open','');});
 HTMLCanvasElement.prototype.getContext=vi.fn(()=>({drawImage:vi.fn()})) as unknown as typeof HTMLCanvasElement.prototype.getContext;
 HTMLCanvasElement.prototype.toBlob=vi.fn(callback=>callback(new Blob(['image'],{type:'image/webp'})));
});
afterEach(()=>vi.unstubAllGlobals());
it('shows available professionals and adds without duplicate options',async()=>{
 render(<ClinicTeam/>);await screen.findByText('No hay profesionales seleccionados para el inicio.');
 await userEvent.click(screen.getByText('+ Agregar profesional'));await screen.findByText('Juan Ramirez');
 await userEvent.click(screen.getByText('Agregar al inicio'));await screen.findByText('Quitar del inicio');
 expect(api).toHaveBeenCalledWith('/api/admin/clinic/team','POST',{doctorProfileId:'d1',displayOrder:0});
 expect(screen.queryByText('Agregar al inicio')).toBeNull();
});
it('removes publication only and saves order',async()=>{
 selected=[{...featured}];available=[];render(<ClinicTeam/>);await screen.findByText('Quitar del inicio');
 fireEvent.change(screen.getByLabelText('Orden de Juan Ramirez'),{target:{value:'3'}});await userEvent.click(screen.getByText('Guardar orden'));
 await waitFor(()=>expect(api).toHaveBeenCalledWith('/api/admin/clinic/team/f1/order','PUT',{displayOrder:3}));
 await userEvent.click(screen.getByText('Quitar del inicio'));await screen.findByText('No hay profesionales seleccionados para el inicio.');
 expect(api).toHaveBeenCalledWith('/api/admin/clinic/team/f1','DELETE');expect(api.mock.calls.some(x=>x[0].includes('/doctors'))).toBe(false);
});
it('warns about missing photo and opens the existing portrait cropper',async()=>{
 selected=[{...featured}];render(<ClinicTeam/>);await screen.findByText(/todavía no tiene foto/);
 await userEvent.click(screen.getByRole('button',{name:'Subir foto'}));
 fireEvent.change(screen.getByLabelText('Subir foto'),{target:{files:[new File(['png'],'photo.png',{type:'image/png'})]}});
 expect(screen.getByText(/1080 × 1350 px. Arrastra/)).toBeTruthy();
 await userEvent.click(screen.getByRole('button',{name:'Cancelar'}));expect(screen.queryByText('Recortar foto de perfil')).toBeNull();
 expect(api.mock.calls.some(x=>x[1]==='PUT')).toBe(false);
});
it('uploads the shared crop to the existing UserProfilePhotos endpoint',async()=>{
 selected=[{...featured}];render(<ClinicTeam/>);await screen.findByText(/todavía no tiene foto/);await userEvent.click(screen.getByRole('button',{name:'Subir foto'}));
 fireEvent.change(screen.getByLabelText('Subir foto'),{target:{files:[new File(['png'],'photo.png',{type:'image/png'})]}});
 const image=document.querySelector('.cover-crop-frame img') as HTMLImageElement;
 if(image){Object.defineProperties(image,{naturalWidth:{value:1080},naturalHeight:{value:1350}});fireEvent.load(image);}
 const apply=screen.getByRole('button',{name:/Aplicar recorte/});await userEvent.click(apply);
 await userEvent.click(screen.getByRole('button',{name:'Guardar foto'}));
 await waitFor(()=>expect(api).toHaveBeenCalledWith('/api/admin/users/u1/profile-photo','PUT',expect.any(FormData)));
});
it('existing photo uses shared preview and change action',async()=>{
 selected=[{...featured,photoVersion:'v1'}];vi.stubGlobal('fetch',vi.fn(async()=>({ok:true,blob:async()=>new Blob(['image'])})));
 render(<ClinicTeam/>);await screen.findByText('Cambiar foto');await waitFor(()=>expect(document.querySelector('.profile-avatar img')).toBeTruthy());
});
it('read-only permissions hide mutation controls',async()=>{
 selected=[{...featured}];permissions.write=false;render(<ClinicTeam/>);await screen.findByText('Juan Ramirez');expect(screen.queryByText('Quitar del inicio')).toBeNull();expect(screen.queryByText('+ Agregar profesional')).toBeNull();
});
it('reports loading errors without pretending list is empty',async()=>{
 api.mockRejectedValue(new Error('Error de carga'));render(<ClinicTeam/>);expect(screen.getByRole('status')).toBeTruthy();await screen.findByText('Error de carga');expect(screen.queryByText('No hay profesionales seleccionados para el inicio.')).toBeNull();
});
it('public empty state is preserved',async()=>{vi.stubGlobal('fetch',vi.fn(async()=>({ok:true,json:async()=>[]})));render(<TeamSection/>);expect(await screen.findByText('Conoce a quienes te acompañan')).toBeTruthy();});
it('public cards use real names, ordered response, photos and fallback',async()=>{
 vi.stubGlobal('fetch',vi.fn(async()=>({ok:true,json:async()=>[{id:'b',firstName:'Maria',lastName:'Perez',specialties:['Ortodoncia'],photoVersion:'v1'},{id:'a',firstName:'Juan',lastName:'Ramirez',specialties:[],photoVersion:null}]})));
 render(<TeamSection/>);await screen.findByText('Maria Perez');expect(screen.getByAltText('Maria Perez').getAttribute('src')).toContain('/api/clinic/team/b/photo');expect(screen.getByLabelText('Sin foto de Juan Ramirez').textContent).toBe('JR');
 expect([...document.querySelectorAll('.public-team-card h3')].map(x=>x.textContent)).toEqual(['Maria Perez','Juan Ramirez']);expect(document.querySelectorAll('.public-team-grid .public-team-card')).toHaveLength(2);
 expect(document.querySelector('.public-team-caption p span')?.textContent).toContain('Ortodoncia');expect(screen.getByRole('button',{name:'Reservar cita con Maria Perez'})).toBeTruthy();expect(fetch).toHaveBeenCalledTimes(1);
 fireEvent.error(screen.getByAltText('Maria Perez'));expect(screen.getByLabelText('Sin foto de Maria Perez')).toBeTruthy();
});
it('keeps a single API professional in one compact card',async()=>{
 vi.stubGlobal('fetch',vi.fn(async()=>({ok:true,json:async()=>[{id:'featured-1',firstName:'Maria',lastName:'Perez',specialties:['Ortodoncia'],photoVersion:null}]})));
 render(<TeamSection/>);await screen.findByText('Maria Perez');
 expect(document.querySelectorAll('.public-team-grid .public-team-card')).toHaveLength(1);
 expect(screen.getByRole('button',{name:'Reservar cita con Maria Perez'})).toBeTruthy();
});
it('shows real specialty and branch preview labels while keeping controls disabled',async()=>{
 const branches=[{id:'branch-1',name:'Sucursal Centro',address:'Calle Uno',city:'Llallagua',department:'Potosí',phone:null,latitude:null,longitude:null}];
 vi.stubGlobal('fetch',vi.fn(async()=>({ok:true,json:async()=>[{id:'featured-1',firstName:'Maria',lastName:'Perez',specialties:['Ortodoncia'],photoVersion:null}]})));
 render(<TeamSection branches={branches}/>);await screen.findByText('Maria Perez');
 expect(screen.getByText('Sucursal Centro',{selector:'.team-filter-option span'})).toBeTruthy();
 expect(screen.getByRole('note').textContent).toContain('todavía no están activos');
 expect(screen.getByRole('searchbox',{name:'Buscar profesionales (vista previa)'}).hasAttribute('disabled')).toBe(true);
 expect(screen.getByLabelText('Especialidades (vista previa)').hasAttribute('disabled')).toBe(true);
 expect(screen.getByLabelText('Sucursales (vista previa)').hasAttribute('disabled')).toBe(true);
 expect([...screen.getAllByRole('checkbox')].every(control=>control.hasAttribute('disabled'))).toBe(true);
 expect(screen.getByRole('button',{name:'Ordenar profesionales'}).hasAttribute('disabled')).toBe(true);
 expect(screen.getByText('Paginación no activa')).toBeTruthy();
 expect(screen.queryByText(/reseñas|disponible hoy|Ver perfil/i)).toBeNull();
});

