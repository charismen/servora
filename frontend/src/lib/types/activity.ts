export interface ActivityLog {
	id: number;
	entityType: string;
	entityId: number;
	actionType: string;
	title: string;
	description?: string;
	createdAt: string;
}

export interface ActivityPage {
	items: ActivityLog[];
	page: number;
	pageSize: number;
	totalItems: number;
	totalPages: number;
}

export function activityEntityHref(entityType: string, entityId: number): string | null {
	switch (entityType) {
		case 'ServiceTicket':
			return entityId > 0 ? `/app/tickets/${entityId}` : '/app/tickets';
		case 'Customer':
			return entityId > 0 ? `/app/customers/${entityId}` : '/app/customers';
		case 'InventoryItem':
			return entityId > 0 ? `/app/inventory/${entityId}` : '/app/inventory';
		case 'Asset':
			return entityId > 0 ? `/app/assets/${entityId}` : '/app/assets';
		case 'Settings':
			return '/app/settings';
		case 'Attendance':
			return '/app/attendance';
		case 'ShopUser':
			return '/app/users-roles';
		case 'Role':
			return '/app/users-roles?tab=roles';
		default:
			return null;
	}
}
