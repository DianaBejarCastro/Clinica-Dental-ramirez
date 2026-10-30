import { fireEvent, render, screen, within, waitFor } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { CatalogEditor } from '../modules/inventory/InventoryEditors';
import InventoryPage from '../modules/inventory/InventoryPage';
import { doctorNavigation, navigationGroups } from '../layouts/navigation';
import { ApiError } from '../services/api';
const mock=vi.hoisted(()=>vi.fn());
vi.mock('../services/api',async original=>({...await original<typeof import('../services/api')>(),api:mock}));
vi.mock('../modules/admin/shared',()=>({usePermissions:()=>()=>true}));
const options={categories:[{id:'cat',code:'PROTECTION',name:'Protección',description:null,isActive:true,displayOrder:0}],centers:[{id:'c1',name:'Central'},{id:'c2',name:'Norte'}],doctors:[{id:'d',name:'Ana Doctor',professionalLicense:'LP-123',centerIds:['c1']}]};
const stocks=[{inventoryItemId:'i1',centerId:'c1',center:'Central',code:'G1',name:'Guantes',categoryId:'cat',category:'Protección',unit:'BOX',quantityOnHand:20,minimumStock:5,status:'AVAILABLE',isActive:true},
{inventoryItemId:'i2',centerId:'c1',center:'Central',code:'M1',name:'Mascarillas',categoryId:'cat',category:'Protección',unit:'UNIT',quantityOnHand:2,minimumStock:5,status:'LOW_STOCK',isActive:true},
{inventoryItemId:'i3',centerId:'c2',center:'Norte',code:'A1',name:'Agujas',categoryId:'cat',category:'Protección',unit:'UNIT',quantityOnHand:0,minimumStock:5,status:'OUT_OF_STOCK',isActive:true}];
let reject=false;
beforeEach(()=>{reject=false;mock.mockReset();mock.mockImplementation(async(path:string,method='GET')=>{
 if(method!=='GET'){if(reject)throw new ApiError(409,'Stock insuficiente para completar el retiro.');return 'saved';}
 if(path.includes('/expiration-alerts'))return {expired:0,within30:0,within60:0,within90:0,lowStock:1,outOfStock:1};
 if(path.includes('/lots?'))return [];
 if(path.endsWith('/options'))return options;
 if(path.includes('/movements?')||path.includes('/my-withdrawals?'))return {items:[{id:'m',center:'Central',movementType:'WITHDRAWAL',occurredAt:'2026-10-23T10:00:00Z',registeredBy:'Administrador',doctor:'Ana Doctor',professionalLicense:'LP-123',notes:'Entrega',lines:[{inventoryItemId:'i1',name:'Guantes',unit:'BOX',quantity:3}]}],total:1,page:1,pageSize:50};
 if(path.endsWith('/items'))return stocks.map(s=>({...s,id:s.inventoryItemId}));
 return stocks;
});});
async function ready(doctor=false){render(<InventoryPage doctor={doctor}/>);await screen.findByRole('region',{name:'Existencias por sucursal'});}
function select(label:string,value:string){fireEvent.change(screen.getByLabelText(label),{target:{value}});}
async function openMove(doctor=false){await ready(doctor);fireEvent.click(screen.getByRole('button',{name:doctor?'Registrar mi retiro':'Registrar movimiento'}));const form=screen.getByRole('heading',{name:doctor?'Registrar mi retiro':'Registrar movimiento'}).closest('form')!;
 fireEvent.change(within(form).getByLabelText('Sucursal'),{target:{value:'c1'}});select('Artículo 1','i1');select('Cantidad 1','3');return form;}
it('admin and doctor navigation expose separate inventory routes',()=>{expect(navigationGroups.flatMap(g=>g.items)).toContainEqual({label:'Inventario',route:'/admin/inventory',permission:'inventory.movement.view'});expect(doctorNavigation.items).toContainEqual({label:'Inventario',route:'/doctor/inventory',permission:'inventory.view'});});
it('loads item stock with branch category and units',async()=>{await ready();expect(screen.getByText('Guantes')).toBeTruthy();expect(screen.getAllByText('Central').length).toBeGreaterThan(0);expect(screen.getByText('Caja')).toBeTruthy();expect(mock).toHaveBeenCalledWith('/api/admin/inventory/options');});
it.each(['Stock bajo','Sin stock','Disponible'])('shows derived status %s',async label=>{await ready();expect(screen.getAllByText(label).length).toBeGreaterThan(0);});
it('filters name category branch and stock',async()=>{await ready();select('Estado','OUT_OF_STOCK');expect(screen.queryByText('Guantes')).toBeNull();expect(within(screen.getByRole('region',{name:'Existencias por sucursal'})).getByText('Agujas')).toBeTruthy();select('Sucursal','c1');expect(screen.getByText('No hay artículos para estos filtros.')).toBeTruthy();});
it('creates item with category unit minimum and no initial stock',async()=>{await ready();fireEvent.click(screen.getByRole('button',{name:'Crear artículo'}));select('Código','X1');select('Nombre','Nuevo');fireEvent.change(within(screen.getByRole('heading',{name:'Crear artículo'}).closest('form')!).getByLabelText('Categoría'),{target:{value:'cat'}});select('Unidad de stock','SYRINGE');select('Stock mínimo','2');fireEvent.click(screen.getByRole('button',{name:'Guardar artículo'}));await waitFor(()=>expect(mock).toHaveBeenCalledWith('/api/admin/inventory/items','POST',expect.objectContaining({unit:'SYRINGE',minimumStock:2})));expect(screen.queryByLabelText('Stock inicial')).toBeNull();});
it('edits item without editing stock',async()=>{await ready();fireEvent.click(screen.getByRole('button',{name:'Editar Guantes'}));select('Nombre','Guantes actualizados');fireEvent.click(screen.getByRole('button',{name:'Guardar cambios'}));await waitFor(()=>expect(mock).toHaveBeenCalledWith('/api/admin/inventory/items/i1','PUT',expect.objectContaining({name:'Guantes actualizados'})));});
it('creates category',async()=>{await ready();fireEvent.click(screen.getByRole('button',{name:'Crear categoría'}));select('Código','NEW');select('Nombre','Nueva');fireEvent.click(screen.getByRole('button',{name:'Guardar'}));await waitFor(()=>expect(mock).toHaveBeenCalledWith('/api/admin/inventory/categories','POST',expect.objectContaining({code:'NEW'})));});
it.each(['ENTRY','WITHDRAWAL','RETURN','ADJUSTMENT'])('records %s through movement endpoint',async type=>{await openMove();select('Tipo de movimiento',type);if(type==='WITHDRAWAL'||type==='RETURN')select('Doctor','d');if(type==='ADJUSTMENT'){select('Dirección','DECREASE');select('Motivo obligatorio','Corrección');}fireEvent.click(screen.getByRole('button',{name:'Confirmar movimiento'}));await waitFor(()=>expect(mock).toHaveBeenCalledWith('/api/admin/inventory/movements/'+type.toLowerCase(),'POST',expect.objectContaining({lines:[{inventoryItemId:'i1',quantity:3}]})));});
it('doctor options show full name and professional license',async()=>{await openMove();select('Tipo de movimiento','WITHDRAWAL');expect(screen.getByRole('option',{name:'Ana Doctor · LP-123'})).toBeTruthy();});
it('registers multiple items in one movement',async()=>{await openMove();fireEvent.click(screen.getByRole('button',{name:'Agregar artículo'}));select('Artículo 2','i2');select('Cantidad 2','1');fireEvent.click(screen.getByRole('button',{name:'Confirmar movimiento'}));await waitFor(()=>expect(mock).toHaveBeenCalledWith(expect.any(String),'POST',expect.objectContaining({lines:[{inventoryItemId:'i1',quantity:3},{inventoryItemId:'i2',quantity:1}]})));});
it('insufficient stock retains form and displays controlled error',async()=>{await openMove();reject=true;fireEvent.click(screen.getByRole('button',{name:'Confirmar movimiento'}));expect(await screen.findByRole('alert')).toHaveProperty('textContent',expect.stringContaining('Stock insuficiente'));expect(screen.getByLabelText('Cantidad 1')).toHaveProperty('value','3');});
it('history shows creator doctor dates and filters',async()=>{await ready();fireEvent.click(screen.getByRole('button',{name:'Historial de movimientos'}));expect(await screen.findByText('Administrador')).toBeTruthy();expect(within(screen.getByRole('region',{name:'Historial de inventario'})).getByText('Ana Doctor · LP-123')).toBeTruthy();select('Tipo','ENTRY');await waitFor(()=>expect(mock).toHaveBeenCalledWith(expect.stringContaining('type=ENTRY')));});
it('doctor sees stock and self withdrawal without recipient selector',async()=>{await openMove(true);expect(screen.queryByLabelText('Doctor')).toBeNull();expect(screen.queryByLabelText('Tipo de movimiento')).toBeNull();expect(screen.queryByRole('button',{name:'Crear artículo'})).toBeNull();fireEvent.click(screen.getByRole('button',{name:'Confirmar retiro'}));await waitFor(()=>expect(mock).toHaveBeenCalledWith('/api/doctor/inventory/withdraw','POST',expect.not.objectContaining({recipientDoctorProfileId:expect.anything()})));});
it('doctor reads only my withdrawals endpoint',async()=>{await ready(true);fireEvent.click(screen.getByRole('button',{name:'Mis retiros'}));await screen.findByText('Administrador');expect(mock).toHaveBeenCalledWith(expect.stringContaining('/api/doctor/inventory/my-withdrawals?'));});
it('chart region supports internal scrolling and keyboard focus',async()=>{await ready();const region=screen.getByRole('region',{name:'Existencias por sucursal'});expect(region.className).toBe('inventory-scroll');expect(region.tabIndex).toBe(0);});
it('loading state and failed request are controlled',async()=>{mock.mockRejectedValue(new Error('private stack'));render(<InventoryPage/>);expect(screen.getByRole('status').textContent).toContain('Cargando');expect(await screen.findByRole('alert')).toHaveProperty('textContent',expect.stringContaining('No se pudo'));expect(screen.queryByText('private stack')).toBeNull();});

it('admin dashboard shows alerts categories and latest movement navigation',async()=>{
 await ready();
 expect(screen.getByRole('heading',{name:'Alertas de inventario'})).toBeTruthy();
 expect(screen.getByRole('heading',{name:'Stock por categoría'})).toBeTruthy();
 expect(screen.getByRole('heading',{name:'Últimos movimientos'})).toBeTruthy();
 await screen.findByRole('button',{name:'Ver todos los movimientos'});
 fireEvent.click(screen.getByRole('button',{name:'Ver todos los movimientos'}));
 expect(await screen.findByRole('region',{name:'Historial de inventario'})).toBeTruthy();
});
it('doctor keeps original table without administrative dashboard requests',async()=>{
 await ready(true);expect(document.querySelector('.inventory-admin')).toBeNull();
 expect(screen.queryByRole('heading',{name:'Alertas de inventario'})).toBeNull();
 expect(mock.mock.calls.some(([path])=>String(path).includes('/api/admin/inventory'))).toBe(false);
 expect(screen.getByRole('columnheader',{name:'Código'})).toBeTruthy();
});
it('admin table combines code and unit and keeps expiration column',async()=>{
 await ready();expect(screen.queryByRole('columnheader',{name:'Código'})).toBeNull();
 expect(screen.getByRole('columnheader',{name:'Vencimiento'})).toBeTruthy();
 expect(document.querySelector('.inventory-item-code')?.textContent).toBe('G1');
 expect(screen.getByRole('region',{name:'Resumen de inventario'}).textContent).toContain('3');
});

it('item sections retain tracking dependency and cancel without writes',async()=>{
 await ready();fireEvent.click(screen.getByRole('button',{name:'Crear artículo'}));
 for(const name of ['Información básica','Control de stock','Vencimiento y lotes','Presentación del producto','Estado','Información'])expect(screen.getByRole('heading',{name})).toBeTruthy();
 expect(screen.getByLabelText('Control de vencimiento')).toHaveProperty('disabled',true);
 fireEvent.click(screen.getByLabelText('Seguimiento de lotes'));
 expect(screen.getByLabelText('Control de vencimiento')).toHaveProperty('disabled',false);
 expect(screen.getByLabelText('Activo')).toHaveProperty('checked',true);
 for(const name of ['Stock actual','Stock inicial','Número de lote','Fecha de vencimiento','Precio','Imagen'])expect(screen.queryByLabelText(name)).toBeNull();
 fireEvent.click(screen.getByRole('button',{name:'Cancelar'}));
 expect(screen.queryByRole('heading',{name:'Crear artículo'})).toBeNull();
 expect(mock.mock.calls.some(([,method])=>method==='POST')).toBe(false);
});

it('item form submits existing fields and tracking flags without stock or lot data',async()=>{
 await ready();fireEvent.click(screen.getByRole('button',{name:'Crear artículo'}));
 const form=screen.getByRole('heading',{name:'Crear artículo'}).closest('form')!;
 expect((within(form).getByLabelText('Código') as HTMLInputElement).checkValidity()).toBe(false);
 select('Código','NEW');select('Nombre','Material');fireEvent.change(within(form).getByLabelText('Categoría'),{target:{value:'cat'}});
 select('Unidad de stock','BOX');select('Stock mínimo','3.5');select('Descripción','Uso clínico');
 fireEvent.click(screen.getByLabelText('Seguimiento de lotes'));fireEvent.click(screen.getByLabelText('Control de vencimiento'));fireEvent.click(screen.getByLabelText('Activo'));
 fireEvent.click(screen.getByRole('button',{name:'Guardar artículo'}));
 await waitFor(()=>expect(mock).toHaveBeenCalledWith('/api/admin/inventory/items','POST',{
 code:'NEW',name:'Material',categoryId:'cat',unit:'BOX',minimumStock:3.5,description:'Uso clínico',isActive:false,tracksLots:true,tracksExpiration:true,packageUnit:null,unitsPerPackage:null
 }));
});

it('initial stock is optional and only shown on creation',async()=>{
 await ready();fireEvent.click(screen.getByRole('button',{name:'Crear artículo'}));
 expect(screen.getByLabelText('Sí, registrar stock')).toHaveProperty('checked',false);
 expect(screen.queryByLabelText('Sucursal inicial')).toBeNull();
 expect(screen.getByText(/No representa el stock actual/)).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Cancelar'}));
 fireEvent.click(screen.getByRole('button',{name:'Editar Guantes'}));
 expect(screen.queryByLabelText('Sí, registrar stock')).toBeNull();
 expect(screen.getByText('El stock se modifica mediante movimientos de inventario.')).toBeTruthy();
});
it.each([false,true])('initial entry uses dedicated operation and conditional lot fields: %s',async(tracked)=>{
 await ready();fireEvent.click(screen.getByRole('button',{name:'Crear artículo'}));
 const form=screen.getByRole('heading',{name:'Crear artículo'}).closest('form')!;
 select('Código','INIT');select('Nombre','Inicial');
 fireEvent.change(within(form).getByLabelText('Categoría'),{target:{value:'cat'}});
 fireEvent.click(screen.getByLabelText('Sí, registrar stock'));
 expect(screen.getByLabelText('Sucursal inicial')).toHaveProperty('required',true);
 expect(screen.getByLabelText('Cantidad inicial')).toHaveProperty('min','.001');
 select('Sucursal inicial','c1');select('Cantidad inicial','20');
 expect(screen.queryByLabelText('Número de lote inicial')).toBeNull();
 if(tracked){
  fireEvent.click(screen.getByLabelText('Seguimiento de lotes'));
  expect(screen.getByLabelText('Número de lote inicial')).toHaveProperty('required',true);
  expect(screen.queryByLabelText('Vencimiento del lote inicial')).toBeNull();
  select('Número de lote inicial','REAL-1');fireEvent.click(screen.getByLabelText('Control de vencimiento'));
  select('Vencimiento del lote inicial','2035-01-01');
 }
 expect(screen.getByRole('region',{name:'Resumen de registro'}).textContent).toContain('20 unidades');
 fireEvent.click(screen.getByRole('button',{name:'Guardar artículo'}));
 await waitFor(()=>expect(mock).toHaveBeenCalledWith('/api/admin/inventory/items/with-initial-stock','POST',expect.objectContaining({
 item:expect.objectContaining({code:'INIT',tracksLots:tracked,tracksExpiration:tracked}),
 initialStock:tracked?{centerId:'c1',quantity:20,batchNumber:'REAL-1',expirationDate:'2035-01-01'}:{centerId:'c1',quantity:20},
 idempotencyKey:expect.any(String)
 })));
});

it('package preview explains 2 boxes of 100 and submits package quantity for server conversion',async()=>{
 await ready();fireEvent.click(screen.getByRole('button',{name:'Crear artículo'}));
 expect(screen.getByLabelText('Unidad de stock')).toBeTruthy();
 expect(screen.getByText('Es la unidad que el sistema usará para contar y descontar este artículo.')).toBeTruthy();
 select('Código','GLOVE');select('Nombre','Guantes');
 const form=screen.getByRole('heading',{name:'Crear artículo'}).closest('form')!;
 fireEvent.change(within(form).getByLabelText('Categoría'),{target:{value:'cat'}});
 select('Presentación','BOX');select('Unidades por presentación','100');select('Stock mínimo','20');
 expect(screen.getByText('1 caja = 100 unidades')).toBeTruthy();
 expect(screen.getByText('Se mostrará una alerta cuando queden 20 unidades o menos.')).toBeTruthy();
 fireEvent.click(screen.getByLabelText('Sí, registrar stock'));select('Sucursal inicial','c1');select('Número de envases','2');
 expect(screen.getByText('2 cajas × 100 unidades = 200 unidades')).toBeTruthy();
 expect(screen.getByText('Se añadirán 200 unidades al inventario.')).toBeTruthy();
 expect(screen.queryByLabelText('Cantidad inicial')).toBeNull();
 expect(screen.getByRole('region',{name:'Resumen de registro'}).textContent).toContain('200 unidades');
 fireEvent.click(screen.getByRole('button',{name:'Guardar artículo'}));
 await waitFor(()=>expect(mock).toHaveBeenCalledWith('/api/admin/inventory/items/with-initial-stock','POST',expect.objectContaining({item:expect.objectContaining({unit:'UNIT',packageUnit:'BOX',unitsPerPackage:100}),initialStock:{centerId:'c1',quantity:2,quantityInPackages:true}})));
});
it('invalid package content and fractional package counts fail native validation',async()=>{
 await ready();fireEvent.click(screen.getByRole('button',{name:'Crear artículo'}));select('Presentación','BOX');
 const content=screen.getByLabelText('Unidades por presentación') as HTMLInputElement;
 for(const value of ['0','-1']){select('Unidades por presentación',value);expect(content.checkValidity()).toBe(false);}
 select('Unidades por presentación','100');fireEvent.click(screen.getByLabelText('Sí, registrar stock'));select('Número de envases','1.5');
 expect((screen.getByLabelText('Número de envases') as HTMLInputElement).checkValidity()).toBe(false);
 select('Unidad de stock','BOX');expect(screen.getByText('El inventario se llevará por cajas completas.')).toBeTruthy();
 select('Stock mínimo','2');expect(screen.getByText('Se mostrará una alerta cuando queden 2 cajas o menos.')).toBeTruthy();
});

it('editing an item with movement history locks stock unit and packaging but keeps metadata editable',()=>{
 render(<CatalogEditor item={{id:'i',categoryId:'cat',category:'Protección',code:'G1',name:'Guantes',description:null,unit:'UNIT',minimumStock:20,isActive:true,packageUnit:'BOX',unitsPerPackage:100,hasMovements:true}} options={options} busy={false} save={vi.fn()} cancel={vi.fn()}/>);
 for(const label of ['Unidad de stock','Presentación','Unidades por presentación'])expect(screen.getByLabelText(label)).toHaveProperty('disabled',true);
 expect(screen.getByLabelText('Nombre')).toHaveProperty('disabled',false);
 expect(screen.getByText('Esta configuración no se puede modificar porque el artículo ya tiene movimientos de inventario.')).toBeTruthy();
 expect(screen.queryByLabelText('Sí, registrar stock')).toBeNull();
});
