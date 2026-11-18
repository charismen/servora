export type AssetStatus = 'Active' | 'Available' | 'UnderMaintenance' | 'Retired' | 'Sold' | 'WrittenOff';
export type AssetCondition = 'New' | 'Good' | 'Fair' | 'Poor' | 'Damaged';
export type DepreciationMethod = 'StraightLine' | 'None';
export type AssetCustodyActionType = 'CheckOut' | 'CheckIn' | 'Transfer';
export type AssetMaintenanceType = 'Preventive' | 'Corrective' | 'Calibration' | 'Upgrade';

export interface AssetValuationBreakdown {
	purchaseCost: number;
	salvageValue: number;
	depreciableBase: number;
	usefulLifeMonths: number;
	ageMonths: number;
	ageYearsPart: number;
	ageMonthsPart: number;
	elapsedDepreciableMonths: number;
	remainingLifeMonths: number;
	lifeConsumedPercent: number;
	monthlyDepreciation: number;
	accumulatedDepreciation: number;
	currentBookValue: number;
	effectiveSellingValue: number;
	estimatedGainLoss: number;
	totalMaintenanceCost: number;
	netTco: number;
	isEolApproaching: boolean;
	isFullyDepreciated: boolean;
	isWarrantyActive: boolean;
}

export interface AssetDepreciationScheduleRow {
	yearNumber: number;
	startMonth: number;
	endMonth: number;
	periodStart: string;
	periodEnd: string;
	openingBookValue: number;
	depreciationExpense: number;
	accumulatedDepreciation: number;
	closingBookValue: number;
	isCurrentPeriod: boolean;
}

export interface AssetCustodyLog {
	id: number;
	userId?: number | null;
	userName?: string | null;
	actionType: AssetCustodyActionType;
	location?: string | null;
	condition: AssetCondition;
	notes?: string | null;
	recordedByUserName?: string | null;
	createdAt: string;
}

export interface AssetMaintenanceLog {
	id: number;
	serviceTicketId?: number | null;
	ticketNumber?: string | null;
	maintenanceType: AssetMaintenanceType;
	title: string;
	description?: string | null;
	cost: number;
	performedBy?: string | null;
	performedAt: string;
	nextDueAt?: string | null;
	recordedByUserName?: string | null;
	createdAt: string;
}

export interface AssetCategory {
	id: number;
	name: string;
	description?: string | null;
	defaultUsefulLifeMonths: number;
	defaultSalvagePercent: number;
	isActive: boolean;
	assetCount: number;
}

export interface AssetListItem {
	id: number;
	assetTag: string;
	publicToken: string;
	name: string;
	categoryId?: number | null;
	categoryName?: string | null;
	companyName?: string | null;
	department?: string | null;
	location?: string | null;
	brand?: string | null;
	model?: string | null;
	serialNumber?: string | null;
	status: AssetStatus;
	condition: AssetCondition;
	assignedUserId?: number | null;
	assignedUserName?: string | null;
	purchaseDate: string;
	inServiceDate: string;
	warrantyExpiresAt?: string | null;
	purchaseCost: number;
	salvageValue: number;
	usefulLifeMonths: number;
	depreciationMethod: DepreciationMethod;
	estimatedSellingValue?: number | null;
	valuation: AssetValuationBreakdown;
	createdAt: string;
}

export interface AssetDetail extends AssetListItem {
	description?: string | null;
	supplierName?: string | null;
	invoiceNumber?: string | null;
	disposedAt?: string | null;
	actualSoldPrice?: number | null;
	disposalNotes?: string | null;
	depreciationSchedule: AssetDepreciationScheduleRow[];
	custodyLogs: AssetCustodyLog[];
	maintenanceLogs: AssetMaintenanceLog[];
	updatedAt?: string | null;
}

export interface AssetsSummary {
	totalAssets: number;
	activeCount: number;
	availableCount: number;
	underMaintenanceCount: number;
	eolApproachingCount: number;
	totalPurchaseCost: number;
	totalNetBookValue: number;
	totalAccumulatedDepreciation: number;
	monthlyDepreciationExpense: number;
	totalEstimatedSellingValue: number;
	totalMaintenanceCost: number;
}

export interface AssetsPage {
	items: AssetListItem[];
	summary: AssetsSummary;
	totalCount: number;
	page: number;
	pageSize: number;
}
