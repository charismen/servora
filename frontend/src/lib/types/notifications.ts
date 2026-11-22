export type WhatsAppProviderType = 'Manual' | 'MetaCloud' | 'Fonnte' | 'Waha' | 'CustomHttp';
export type TwoFactorEnforcementPolicy = 'Optional' | 'AdminsOnly' | 'AllStaff';
export type TwoFactorMethod = 'None' | 'AuthenticatorTotp' | 'EmailOtp';

export interface NotificationChannelsDto {
	emailEnabled: boolean;
	useSystemSmtp: boolean;
	systemSmtpConfigured: boolean;
	smtpHost: string | null;
	smtpPort: number;
	smtpUseSsl: boolean;
	smtpUsername: string | null;
	hasSmtpPassword: boolean;
	smtpPasswordLast4: string | null;
	fromEmail: string | null;
	fromName: string | null;
	staffNotificationEmails: string | null;
	whatsAppEnabled: boolean;
	whatsAppProvider: WhatsAppProviderType;
	whatsAppApiUrl: string | null;
	whatsAppPhoneNumberId: string | null;
	hasWhatsAppApiToken: boolean;
	whatsAppApiTokenLast4: string | null;
	whatsAppAuthHeaderName: string | null;
	whatsAppCustomPayloadTemplate: string | null;
	telegramEnabled: boolean;
	hasTelegramBotToken: boolean;
	telegramBotTokenLast4: string | null;
	telegramChatId: string | null;
	telegramThreadId: number | null;
	twoFactorEnforcement: TwoFactorEnforcementPolicy;
	requireOtpForPublicQuoteApproval: boolean;
	canStoreSecrets: boolean;
}

export interface NotificationRuleDto {
	eventType: string;
	eventKey: string;
	sendCustomerEmail: boolean;
	sendCustomerWhatsApp: boolean;
	sendStaffEmail: boolean;
	sendStaffTelegram: boolean;
	sendWebhook: boolean;
}

export interface WebhookEndpointDto {
	id: number;
	name: string;
	targetUrl: string;
	isActive: boolean;
	signingSecretLast4: string;
	subscribedEvents: string[];
	customHeaders: Record<string, string> | null;
	lastTriggeredAt: string | null;
	lastStatusCode: number | null;
	createdAt: string;
}

export interface NotificationDeliveryDto {
	id: number;
	channel: string;
	provider: string;
	eventType: string;
	recipient: string;
	subject: string | null;
	payloadPreview: string;
	status: string;
	httpStatusCode: number | null;
	errorMessage: string | null;
	attemptCount: number;
	createdAt: string;
	sentAt: string | null;
}

export interface NotificationDeliveriesPageDto {
	items: NotificationDeliveryDto[];
	page: number;
	pageSize: number;
	totalItems: number;
	totalPages: number;
}

export interface TwoFactorStatusDto {
	enabled: boolean;
	method: TwoFactorMethod;
	recoveryCodesLeft: number;
	emailOtpAvailable: boolean;
	enforcementRequired: boolean;
	maskedEmail: string | null;
}

export interface PublicOtpChannelOption {
	channel: 'WhatsApp' | 'Email';
	maskedDestination: string;
}

export interface PublicQuoteWorkItemDto {
	id: number;
	approvalRequestId: number | null;
	title: string;
	quantity: number;
	unitPrice: number;
	lineTotal: number;
	status: string;
}

export interface PublicQuoteApprovalDto {
	id: number;
	title: string;
	description: string;
	estimatedAmount: number | null;
	status: string;
	resolutionNote: string | null;
	createdAt: string;
	resolvedAt: string | null;
	proposedWorkItems: PublicQuoteWorkItemDto[];
}

export interface PublicQuoteApprovalsPayload {
	approvals: PublicQuoteApprovalDto[];
	currencyCode: string;
	requiresOtp: boolean;
	otpChannels: PublicOtpChannelOption[];
}
