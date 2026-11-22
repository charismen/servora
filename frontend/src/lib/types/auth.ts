export type OperatingMode = 'Commercial' | 'InternalMaintenance' | 'Hybrid';
export type SecurityCredentialMode = 'None' | 'PasscodeOnly' | 'PasscodeAndPattern';

export type WorkspaceFeatureConfig = {
	showSecurityCredentials: boolean;
	securityCredentialMode: SecurityCredentialMode;
	showAccessories: boolean;
	quickAccessoriesEn: string[];
	quickAccessoriesId: string[];
	showPreRepairChecklist: boolean;
	showPhysicalConditionNotes: boolean;
	showWarrantySection: boolean;
	showBillingAndPayments: boolean;
	showCustomerApprovals: boolean;
	enableFieldService: boolean;
	defaultServiceLocationType: 'InShop' | 'OnSite' | 'PickupDelivery';
	enablePreventiveMaintenance: boolean;
	attachmentTypes: string[];
};

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
	featureConfig: WorkspaceFeatureConfig;
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
	twoFactorEnabled?: boolean;
	requiresTwoFactorSetup?: boolean;
};

export type LoginResponse = {
	accessToken: string;
	expiresAt: string;
	user: CurrentUser;
};

export type TwoFactorChallengeResponse = {
	requiresTwoFactor: true;
	challengeToken: string;
	method: 'AuthenticatorTotp' | 'EmailOtp' | string;
	maskedEmail?: string | null;
};

export type LoginApiResponse = LoginResponse | TwoFactorChallengeResponse;
