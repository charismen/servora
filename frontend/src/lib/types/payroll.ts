export type AttendanceStatus = 'Pending' | 'Approved' | 'Rejected';

export interface AttendanceItem {
	id: number;
	shopUserId: number;
	userId: number;
	workerName: string;
	workerEmail: string;
	employmentType: string;
	roles: string[];
	workDate: string;
	checkInAt: string;
	checkOutAt?: string | null;
	breakMinutes: number;
	totalHours: number;
	isAdminOverride: boolean;
	overrideReason?: string | null;
	notes?: string | null;
	status: AttendanceStatus;
	approvedByUserId?: number | null;
	approvedByName?: string | null;
	approvedAt?: string | null;
}

export interface AttendanceListSummary {
	totalEntries: number;
	pendingEntries: number;
	approvedEntries: number;
	rejectedEntries: number;
	totalApprovedHours: number;
	operationalOpenTime: string;
	operationalCloseTime: string;
	defaultBreakMinutes: number;
	operationalTimeZone?: string;
}

export interface AttendanceListResponse {
	from: string;
	to: string;
	summary: AttendanceListSummary;
	items: AttendanceItem[];
}

export interface TierBreakdownItem {
	tierLabel: string;
	shiftCount: number;
	totalHours: number;
	rateApplied: number;
	subtotalAmount: number;
}

export interface BonusEvaluationResult {
	bonusTypeCode: string;
	bonusName: string;
	metricCount: number;
	qualifiedCount: number;
	unitRate: number;
	flatReward: number;
	totalBonusAmount: number;
	formulaSummary: string;
	referenceNumbers: string[];
}

export interface PayrollAdjustmentItem {
	id: number;
	shopUserId: number;
	periodStart: string;
	periodEnd: string;
	amount: number;
	reason: string;
	createdAt: string;
}

export interface WorkerPayrollRow {
	shopUserId: number;
	userId: number;
	fullName: string;
	email: string;
	employmentType: string;
	roles: string[];
	basePayMode: string;
	baseHourlyRate: number;
	totalDaysWorked: number;
	totalHoursWorked: number;
	pendingShiftCount: number;
	baseCalculatedWage: number;
	tierBreakdown: TierBreakdownItem[];
	totalBonusAmount: number;
	bonusBreakdown: BonusEvaluationResult[];
	totalAdjustments: number;
	adjustments: PayrollAdjustmentItem[];
	finalPayableAmount: number;
}

export interface PayrollSummaryTotals {
	totalFreelanceWorkers: number;
	totalDaysWorked: number;
	totalApprovedHours: number;
	totalPendingShifts: number;
	totalBaseWages: number;
	totalBonuses: number;
	totalAdjustments: number;
	totalPayableAmount: number;
}

export interface PayrollSummary {
	from: string;
	to: string;
	cycle: 'weekly' | 'monthly';
	totals: PayrollSummaryTotals;
	workers: WorkerPayrollRow[];
}

export interface WorkerPayrollSlip {
	shopName: string;
	shopAddress: string;
	shopPhone: string;
	currencyCode: 'IDR' | 'USD';
	from: string;
	to: string;
	cycle: string;
	worker: WorkerPayrollRow;
	attendances: AttendanceItem[];
}

