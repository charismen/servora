export interface InventoryItem {
	id: number;
	categoryId?: number;
	categoryName?: string;
	sku: string;
	name: string;
	description?: string;
	unit: string;
	costPrice?: number;
	salePrice: number;
	itemType: string;
	condition: string;
	purchasePrice?: number;
	shippingCost?: number;
	serviceFee?: number;
	marginPercent?: number;
	marginAmount?: number;
	taxPercent?: number;
	taxAmount?: number;
	priceBeforeTax?: number;
	priceAfterTax?: number;
	fixedSalePrice: number;
	quality?: string;
	warrantyDays?: number;
	supplierName?: string;
	barcodeValue?: string;
	locationNote?: string;
	lastCountedAt?: string;
	onHandQuantity: number;
	reservedQuantity: number;
	availableQuantity: number;
	reorderLevel: number;
	isActive: boolean;
	updatedAt?: string;
}

export interface InventoryItemsResponse {
	items: InventoryItem[];
	totalCount: number;
	page: number;
	pageSize: number;
}

export interface InventoryItemDetail {
	item: InventoryItem;
	stock: {
		onHandQuantity: number;
		reservedQuantity: number;
		availableQuantity: number;
		reorderLevel: number;
		isLowStock: boolean;
	};
	pricing: {
		purchasePrice?: number;
		shippingCost?: number;
		serviceFee?: number;
		marginPercent?: number;
		marginAmount?: number;
		taxPercent?: number;
		taxAmount?: number;
		priceBeforeTax?: number;
		priceAfterTax?: number;
		fixedSalePrice: number;
	};
	movements: InventoryMovement[];
	linkedTickets: InventoryLinkedTicket[];
}

export interface InventoryLinkedTicket {
	ticketId: number;
	ticketNumber: string;
	quantity: number;
	consumedQuantity: number;
	releasedQuantity: number;
	returnedQuantity: number;
	status: string;
	createdAt: string;
}

export interface InventoryImportPreview {
	batchId: number;
	totalRows: number;
	validRows: number;
	errorRows: number;
	rows: InventoryImportRow[];
}

export interface InventoryImportRow {
	id: number;
	rowNumber: number;
	sku: string;
	name: string;
	categoryName?: string;
	isValid: boolean;
	action?: string;
	errorMessage?: string;
	item?: {
		sku: string;
		name: string;
		startingQuantity: number;
		purchasePrice: number;
	};
}

export interface InventoryImportApplyResult {
	created: number;
	updated: number;
	skipped: number;
}

export interface InventoryCategory {
	id: number;
	name: string;
	description?: string;
	isActive: boolean;
}

export interface InventoryMovement {
	id: number;
	inventoryItemId: number;
	itemName: string;
	type: string;
	quantity: number;
	unitCost?: number;
	serviceTicketId?: number;
	ticketNumber?: string;
	reason?: string;
	createdAt: string;
}

export interface TicketInventoryUsage {
	id: number;
	inventoryItemId: number;
	sku: string;
	itemName: string;
	unit: string;
	quantity: number;
	reservedQuantity: number;
	consumedQuantity: number;
	releasedQuantity: number;
	returnedQuantity: number;
	status: string;
	notes?: string;
	unitPrice?: number;
}
