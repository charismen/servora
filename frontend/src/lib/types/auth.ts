export type OperatingMode = 'Commercial' | 'InternalMaintenance' | 'Hybrid';

export type TerminologyLabels = {
	assetSingular: string;
	assetPlural: string;
	ticketSingular: string;
	ticketPlural: string;
	requesterSingular: string;
	requesterPlural: string;
	primaryIdentifierLabel: string;
	secondaryIdentifierLabel: string;
	meterLabel: string;
};

export type TerminologyConfig = {
	en: TerminologyLabels;
	id: TerminologyLabels;
};

export type WorkspaceContext = {
	id: number;
	organizationId: number;
	organizationName: string;
	name: string;
	slug: string;
	operatingMode: OperatingMode;
	industryBlueprintKey: string;
	terminology: TerminologyConfig;
	defaultLocale: string;
	timeZone: string;
	currencyCode: string;
	setupCompleted: boolean;
};

export type WorkspaceSummary = {
	id: number;
	organizationId: number;
	organizationName: string;
	name: string;
	slug: string;
	operatingMode: OperatingMode;
	industryBlueprintKey: string;
	defaultLocale: string;
	timeZone: string;
	currencyCode: string;
	setupCompleted: boolean;
};

export type OrganizationContext = {
	id: number;
	name: string;
	slug: string;
	logoUrl: string | null;
	role: string | null;
};

export type CurrentUser = {
	id: string | number;
	email: string;
	fullName: string;
	shop: WorkspaceContext;
	workspace: WorkspaceContext;
	organization: OrganizationContext | null;
	accessibleWorkspaces: WorkspaceSummary[];
	roles: string[];
	permissions: string[];
};

export type LoginResponse = {
	accessToken: string;
	expiresAt: string;
	user: CurrentUser;
};
