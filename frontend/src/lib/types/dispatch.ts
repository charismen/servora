export type ServiceResourceType = 'Bay' | 'Lift' | 'Bench' | 'Vehicle' | 'Room' | 'Other';
export type DispatchStatus = 'Unscheduled' | 'Scheduled' | 'EnRoute' | 'ArrivedOnSite' | 'VisitCompleted';

export interface ServiceResource {
	id: number;
	name: string;
	code?: string | null;
	resourceType: ServiceResourceType | string;
	color: string;
	notes?: string | null;
	sortOrder: number;
	isActive: boolean;
}

export interface DispatchTechnician {
	userId: number;
	fullName: string;
	email?: string | null;
}

export interface DispatchConflict {
	ticketId: number;
	ticketNumber: string;
	customerName: string;
	deviceSnapshot: string;
	scheduledVisitStart: string;
	scheduledVisitEnd: string;
	conflictType: 'Technician' | 'Resource' | 'Both' | string;
}

export interface DispatchTicket {
	id: number;
	ticketNumber: string;
	customerName: string;
	customerPhone: string;
	deviceSnapshot: string;
	status: string;
	statusColor: string;
	priority: string;
	serviceLocationType: string;
	siteAddressSnapshot?: string | null;
	assignedTechnicianUserId?: number | null;
	assignedTechnicianName?: string | null;
	serviceResourceId?: number | null;
	serviceResourceName?: string | null;
	serviceResourceColor?: string | null;
	scheduledVisitStart?: string | null;
	scheduledVisitEnd?: string | null;
	dispatchStatus: DispatchStatus | string;
	visitStartedAt?: string | null;
	visitCompletedAt?: string | null;
	dispatchNotes?: string | null;
	slaDueAt?: string | null;
	isInternalWorkOrder: boolean;
	hasConflict: boolean;
	conflictReasons: string[];
	createdAt: string;
}

export interface DispatchDueMaintenance {
	id: number;
	title: string;
	assetName: string;
	assetIdentifier?: string | null;
	customerName?: string | null;
	defaultLocationType: string;
	nextDueAt?: string | null;
	assignedTechnicianUserId?: number | null;
	assignedTechnicianName?: string | null;
	isOverdue: boolean;
}

export interface DispatchSummary {
	scheduledCount: number;
	unscheduledCount: number;
	enRouteOrOnSiteCount: number;
	conflictCount: number;
	slaAtRiskCount: number;
	duePmCount: number;
}

export interface DispatchOperationalHours {
	openTime: string;
	closeTime: string;
	timeZone: string;
}

export interface DispatchBoardResponse {
	from: string;
	to: string;
	operationalHours: DispatchOperationalHours;
	technicians: DispatchTechnician[];
	resources: ServiceResource[];
	scheduledTickets: DispatchTicket[];
	unscheduledTickets: DispatchTicket[];
	dueMaintenanceSchedules: DispatchDueMaintenance[];
	summary: DispatchSummary;
}
