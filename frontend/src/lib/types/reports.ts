export interface ReportSummary {
	from: string;
	to: string;
	groupBy: string;
	canViewCost: boolean;
	totals: ReportTotals;
	serviceRows: ServiceReportRow[];
	paymentRows: PaymentReportRow[];
	inventoryRows: InventoryReportRow[];
	inventoryMovementRows: InventoryMovementReportRow[];
	inventoryLowStockGroups: InventoryLowStockGroup[];
	inventoryValuationRows: InventoryValuationRow[];
	agingRows: TicketAgingReportRow[];
	approvalRows: ApprovalReportRow[];
}

export interface ReportTotals {
	receivedTickets: number;
	completedTickets: number;
	cancelledTickets: number;
	unpaidTickets: number;
	partialPaidTickets: number;
	totalRevenue: number;
	totalInventoryCost: number | null;
	totalInventoryValue: number | null;
	grossProfit: number | null;
	lowStockItems: number;
}

export interface ServiceReportRow {
	period: string;
	received: number;
	completed: number;
	cancelled: number;
}

export interface PaymentReportRow {
	ticketId: number;
	ticketNumber: string;
	customerName: string;
	paymentStatus: string;
	amountPaid: number;
	totalAmount: number | null;
}

export interface InventoryReportRow {
	itemId: number;
	sku: string;
	name: string;
	onHandQuantity: number;
	reservedQuantity: number;
	availableQuantity: number;
	reorderLevel: number;
	costPrice: number | null;
}

export interface InventoryMovementReportRow {
	period: string;
	type: string;
	quantity: number;
	cost: number | null;
}

export interface InventoryLowStockGroup {
	categoryName: string;
	itemType: string;
	itemCount: number;
	availableQuantity: number;
	reorderLevel: number;
}

export interface InventoryValuationRow {
	categoryName: string;
	itemType: string;
	itemCount: number;
	onHandQuantity: number;
	inventoryValue: number | null;
}

export interface TicketAgingReportRow {
	bucket: string;
	count: number;
}

export interface ApprovalReportRow {
	status: string;
	count: number;
	estimatedAmount: number | null;
}
