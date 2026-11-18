export type ServiceStatus = {
	id: number;
	name: string;
	color: string;
	sortOrder: number;
	isInitial: boolean;
	isFinal: boolean;
	isCancelled: boolean;
	isBlocked: boolean;
};
