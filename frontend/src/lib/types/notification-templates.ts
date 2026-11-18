export interface NotificationTemplate {
	id: number;
	key: string;
	channel: string;
	provider: string;
	locale: string;
	name: string;
	body: string;
	isActive: boolean;
}

export interface NotificationTemplatePage {
	items: NotificationTemplate[];
	page: number;
	pageSize: number;
	totalItems: number;
	totalPages: number;
}
