export type EmploymentType = 'FullTime' | 'Freelance';
export type BasePayMode =
	| 'Hourly'
	| 'TieredDaily'
	| 'TieredDailyAverage'
	| 'TieredMonthly'
	| 'FixedDaily'
	| 'FixedMonthly'
	| 'CommissionOnly'
	| 'RevenueShare';

export type FallbackPayStrategy = 'ProratedHourly' | 'LowerTierFloor' | 'Zero';
export type TaskPayoutTrigger = 'TicketFinalized' | 'TaskCompleted';

export interface FreelancePayPolicy {
	fixedDailyRate?: number;
	fixedMonthlySalary?: number;
	standardWorkDaysPerMonth?: number;
	prorateMonthlyByAttendance?: boolean;
	revenueSharePercent?: number;
	fallbackStrategy?: FallbackPayStrategy;
	overtimeThresholdHoursPerDay?: number | null;
	overtimeMultiplier?: number;
	weekendMultiplier?: number;
	taskBonusTrigger?: TaskPayoutTrigger;
}

export interface BonusTaskDefinition {
	id: number;
	taskKey: string;
	label: string;
	description?: string | null;
	category?: string | null;
	defaultStandardRate: number;
	defaultGamingRate: number;
	defaultCustomerPrice: number;
	defaultWorkItemType: 'Service' | 'NonStockPart' | 'Fee' | string;
	defaultIsInternalOnly: boolean;
	legacyKeywords?: string | null;
	isActive: boolean;
	sortOrder: number;
	createdAt?: string;
	updatedAt?: string | null;
}

export interface TemplateBonusConfig {
	bonusTypeCode: string;
	isActive: boolean;
	parameters: Record<string, unknown>;
}

export interface CompensationTemplate {
	id: number;
	name: string;
	description?: string | null;
	basePayMode: BasePayMode;
	baseHourlyRate: number;
	tieredRates: WageTierBracket[];
	payPolicy: FreelancePayPolicy;
	bonusConfigs: TemplateBonusConfig[];
	isActive: boolean;
	sortOrder: number;
	createdAt?: string;
	updatedAt?: string | null;
}

export interface CompletionMilestone {
	minUnits: number;
	maxUnits: number;
	rewardPerUnit: number;
	flatReward?: number;
}

export interface TaskItemizedRuleConfig {
	taskKey: string;
	taskLabel: string;
	matcherKeyword: string;
	deviceCategory: 'Any' | 'Standard' | 'Gaming';
	rewardPerUnit: number;
}

export interface WageTierBracket {
	minHours: number;
	maxHours: number;
	flatRate: number;
	hourlyRate?: number | null;
}

export interface BonusType {
	id: number;
	code: string;
	name: string;
	description?: string;
	isSystem: boolean;
}

export interface WorkerBonusConfig {
	bonusTypeId: number;
	bonusTypeCode: string;
	bonusTypeName: string;
	isActive: boolean;
	parameters: Record<string, unknown>;
}

export interface FreelanceProfile {
	id: number;
	shopUserId: number;
	compensationTemplateId?: number | null;
	basePayMode: BasePayMode;
	baseHourlyRate: number;
	tieredRates: WageTierBracket[];
	payPolicy?: FreelancePayPolicy | null;
	bonusConfigs: WorkerBonusConfig[];
}

export interface StaffUser {
	id: number;
	fullName: string;
	email: string;
	roles: string[];
	employmentType?: EmploymentType;
}

export interface ShopRole {
	id: number;
	name: string;
	description?: string;
	permissions?: string[];
}

export interface ShopUser {
	id: number;
	userId: number;
	fullName: string;
	email: string;
	status: string;
	roles: ShopRole[];
	employmentType: EmploymentType;
	freelanceProfile?: FreelanceProfile | null;
	twoFactorEnabled?: boolean;
}

export interface ShopUserPage {
	items: ShopUser[];
	page: number;
	pageSize: number;
	totalItems: number;
	totalPages: number;
}

export interface ShopPermission {
	id: number;
	code: string;
	description?: string;
}
