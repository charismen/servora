export type EmploymentType = 'FullTime' | 'Freelance';
export type BasePayMode = 'Hourly' | 'TieredDaily' | 'TieredDailyAverage' | 'TieredMonthly';

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
	basePayMode: BasePayMode;
	baseHourlyRate: number;
	tieredRates: WageTierBracket[];
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
