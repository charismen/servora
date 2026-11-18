export interface Device {
	id: number;
	customerId: number;
	deviceType: string;
	brand: string;
	model: string;
	serialNumber?: string;
	identifierLabel?: string;
	meterReading?: number | null;
	meterUnit?: string | null;
	siteLocationId?: number | null;
	siteLocationLabel?: string | null;
	customerName?: string | null;
	notes?: string;
	createdAt: string;
	updatedAt?: string;
}
