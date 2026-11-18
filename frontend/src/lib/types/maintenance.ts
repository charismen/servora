export type MaintenanceTriggerType = 'Calendar' | 'Meter' | 'Both';
export type MaintenanceIntervalUnit = 'Days' | 'Weeks' | 'Months';
export type MaintenanceScheduleStatus = 'Active' | 'Paused' | 'Completed';
export type ServiceLocationType = 'InShop' | 'OnSite' | 'PickupDelivery';

export type MaintenanceSchedule = {
	id: number;
	serviceAssetId: number;
	assetDisplayName: string;
	assetIdentifier: string | null;
	currentMeterReading: number | null;
	meterUnit: string | null;
	customerName: string;
	serviceTemplateId: number | null;
	serviceTemplateName: string | null;
	title: string;
	description: string | null;
	triggerType: MaintenanceTriggerType;
	intervalValue: number | null;
	intervalUnit: MaintenanceIntervalUnit | null;
	meterInterval: number | null;
	lastCompletedMeter: number | null;
	nextDueMeter: number | null;
	lastCompletedAt: string | null;
	nextDueAt: string | null;
	leadTimeDays: number;
	autoCreateTicket: boolean;
	assignedTechnicianUserId: number | null;
	assignedTechnicianName: string | null;
	defaultLocationType: ServiceLocationType;
	lastGeneratedTicketId: number | null;
	lastGeneratedTicketNumber: string | null;
	status: MaintenanceScheduleStatus;
	isDue: boolean;
	isUpcoming: boolean;
	createdAt: string;
};

export type CustomerLocation = {
	id: number;
	customerId: number | null;
	customerName: string | null;
	label: string;
	address: string;
	city: string | null;
	postalCode: string | null;
	contactName: string | null;
	contactPhone: string | null;
	latitude: number | null;
	longitude: number | null;
	notes: string | null;
	isDefault: boolean;
	createdAt: string;
};

export type WorkspaceKpi = {
	workspaceId: number;
	name: string;
	slug: string;
	operatingMode: string;
	industryBlueprintKey: string;
	currencyCode: string;
	openTickets: number;
	overdueTickets: number;
	completedToday: number;
	activeServiceAssets: number;
	dueMaintenanceSchedules: number;
	revenuePaid: number;
};

export type OrganizationSummary = {
	organizationId: number;
	organizationName: string;
	organizationSlug: string;
	totalWorkspaces: number;
	totalOpenTickets: number;
	totalOverdueTickets: number;
	totalCompletedToday: number;
	totalServiceAssets: number;
	totalDueMaintenanceSchedules: number;
	workspaces: WorkspaceKpi[];
};
