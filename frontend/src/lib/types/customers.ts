export type RequesterType = 'Customer' | 'InternalEmployee' | 'Department' | 'Branch';

export interface Customer {
	id: number;
	fullName: string;
	phoneNumber: string;
	email?: string;
	address?: string;
	requesterType?: RequesterType;
	companyName?: string | null;
	departmentName?: string | null;
	departmentCode?: string | null;
	costCenter?: string | null;
	employeeId?: string | null;
	isBanned: boolean;
	banReason?: string;
	isStarred: boolean;
	notes?: string;
	createdAt: string;
	updatedAt?: string;
	totalTickets: number;
}

export interface CustomersResponse {
	items: Customer[];
	totalCount: number;
	page: number;
	pageSize: number;
}
