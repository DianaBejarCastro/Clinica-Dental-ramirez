export const units = {UNIT:'Unidad',BOX:'Caja',PACKAGE:'Paquete',BOTTLE:'Frasco',SYRINGE:'Jeringa',ROLL:'Rollo',PAIR:'Par',KIT:'Kit',BAG:'Bolsa',TUBE:'Tubo'} as const;
export const movementTypes = {ENTRY:'Entrada',WITHDRAWAL:'Retiro',RETURN:'Devolución',ADJUSTMENT:'Ajuste'} as const;
export const stockStatuses = {OUT_OF_STOCK:'Sin stock',LOW_STOCK:'Stock bajo',AVAILABLE:'Disponible'} as const;
export interface Category {id:string;code:string;name:string;description:string|null;isActive:boolean;displayOrder:number}
export interface Item {id:string;categoryId:string;code:string;name:string;category:string;description:string|null;unit:keyof typeof units;minimumStock:number;isActive:boolean;tracksLots?:boolean;tracksExpiration?:boolean;packageUnit?:keyof typeof units|null;unitsPerPackage?:number|null;hasMovements?:boolean}
export interface Stock {inventoryItemId:string;centerId:string;center:string;code:string;name:string;categoryId:string;category:string;unit:keyof typeof units;quantityOnHand:number;minimumStock:number;status:keyof typeof stockStatuses;isActive:boolean;tracksLots?:boolean;tracksExpiration?:boolean}
export interface Options {categories:Category[];centers:{id:string;name:string}[];doctors:{id:string;name:string;professionalLicense:string;centerIds:string[]}[]}
export interface Movement {id:string;centerId:string;center:string;movementType:keyof typeof movementTypes;occurredAt:string;registeredBy:string;doctor:string|null;professionalLicense:string|null;notes:string|null;adjustmentDirection:string|null;lines:{inventoryItemId:string;code:string;name:string;unit:keyof typeof units;quantity:number;inventoryLotId?:string|null;batchNumber?:string|null;expirationDate?:string|null}[]}
export interface MovementPage {items:Movement[];total:number;page:number;pageSize:number}
export interface Line {inventoryItemId:string;quantity:string;inventoryLotId?:string;batchNumber?:string;expirationDate?:string;manufacturedAt?:string;supplierName?:string}

export const lotStatuses={EXPIRED:'Vencido',EXPIRING:'Por vencer',VALID:'Vigente',NO_EXPIRATION:'Sin vencimiento'} as const;
export interface Lot {id:string;inventoryItemId:string;item:string;categoryId:string;batchNumber:string;centerId:string|null;center:string|null;quantityOnHand:number;receivedAt:string;expirationDate:string|null;manufacturedAt:string|null;supplierName:string|null;isActive:boolean;daysRemaining:number|null;status:keyof typeof lotStatuses}
export interface ExpirationAlerts {expired:number;within30:number;within60:number;within90:number;lowStock:number;outOfStock:number}
