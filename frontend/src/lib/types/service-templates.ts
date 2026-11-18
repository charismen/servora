export interface ServiceTemplateChecklistItem {
	id: number;
	title: string;
	description?: string;
	sortOrder: number;
}

export interface ServiceTemplate {
	id: number;
	name: string;
	description?: string;
	isActive: boolean;
	checklistItems: ServiceTemplateChecklistItem[];
}

export interface ServiceTemplatePage {
	items: ServiceTemplate[];
	totalCount: number;
	page: number;
	pageSize: number;
}

export interface TicketChecklistItem {
	id: number;
	title: string;
	description?: string;
	sortOrder: number;
	isCompleted: boolean;
	completedAt?: string;
}
