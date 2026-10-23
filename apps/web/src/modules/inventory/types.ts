export const units = {UNIT:'Unidad',BOX:'Caja',PACKAGE:'Paquete',BOTTLE:'Frasco',SYRINGE:'Jeringa',ROLL:'Rollo',PAIR:'Par',KIT:'Kit',BAG:'Bolsa',TUBE:'Tubo'} as const;
export const movementTypes = {ENTRY:'Entrada',WITHDRAWAL:'Retiro',RETURN:'Devolución',ADJUSTMENT:'Ajuste'} as const;
export const stockStatuses = {OUT_OF_STOCK:'Sin stock',LOW_STOCK:'Stock bajo',AVAILABLE:'Disponible'} as const;
export interface Category {id:string;code:string;name:string;description:string|null;isActive:boolean;displayOrder:number}
export interface Item {id:string;categoryId:string;code:string;name:string;category:string;description:string|null;unit:keyof typeof units;minimumStock:number;isActive:boolean}
export interface Stock {inventoryItemId:string;centerId:string;center:string;code:string;name:string;categoryId:string;category:string;unit:keyof typeof units;quantityOnHand:number;minimumStock:number;status:keyof typeof stockStatuses;isActive:boolean}
export interface Options {categories:Category[];centers:{id:string;name:string}[];doctors:{id:string;name:string;professionalLicense:string;centerIds:string[]}[]}
export interface Movement {id:string;centerId:string;center:string;movementType:keyof typeof movementTypes;occurredAt:string;registeredBy:string;doctor:string|null;professionalLicense:string|null;notes:string|null;adjustmentDirection:string|null;lines:{inventoryItemId:string;code:string;name:string;unit:keyof typeof units;quantity:number}[]}
export interface MovementPage {items:Movement[];total:number;page:number;pageSize:number}
export interface Line {inventoryItemId:string;quantity:string}
