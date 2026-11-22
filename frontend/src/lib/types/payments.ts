export type PaymentGatewayProvider = 'None' | 'Xendit' | 'Stripe';
export type PaymentGatewayEnvironment = 'Sandbox' | 'Live';
export type PaymentCheckoutStatus = 'Pending' | 'Paid' | 'Expired' | 'Failed' | 'Cancelled' | 'Superseded';

export interface PaymentGatewaySettings {
	provider: PaymentGatewayProvider | string;
	environment: PaymentGatewayEnvironment | string;
	isEnabled: boolean;
	hasApiKey: boolean;
	apiKeyLast4?: string | null;
	hasWebhookSecret: boolean;
	webhookSecretLast4?: string | null;
	webhookUrl: string;
	defaultLinkExpiryHours: number;
	enablePublicTrackingPayment: boolean;
	requirePhoneVerificationForPublicPayment: boolean;
	workspaceEligible: boolean;
	workspaceEligibilityReason?: string | null;
	encryptionKeyConfigured: boolean;
	canStoreSecrets: boolean;
}

export interface PaymentGatewayTestResult {
	success: boolean;
	message: string;
}

export interface PaymentCheckoutIntent {
	id: number;
	serviceTicketId: number;
	provider: PaymentGatewayProvider | string;
	environment: PaymentGatewayEnvironment | string;
	status: PaymentCheckoutStatus | string;
	amount: number;
	currencyCode: string;
	externalReference: string;
	providerCheckoutId?: string | null;
	paymentUrl?: string | null;
	qrPayload?: string | null;
	expiresAt: string;
	paidAt?: string | null;
	providerPaymentMethod?: string | null;
	settledPaymentRecordId?: number | null;
	createdByName?: string | null;
	failureReason?: string | null;
	createdAt: string;
}

export interface TicketPaymentLinksResponse {
	gatewayEnabled: boolean;
	provider?: string | null;
	environment?: string | null;
	workspaceEligible: boolean;
	workspaceEligibilityReason?: string | null;
	defaultLinkExpiryHours: number;
	links: PaymentCheckoutIntent[];
}

export interface PublicTrackingPaymentSummary {
	onlinePaymentEnabled: boolean;
	requiresPhoneVerification: boolean;
	paymentStatus: string;
	totalAmount?: number | null;
	amountPaid: number;
	remainingBalance: number;
	currencyCode: string;
	activePaymentUrl?: string | null;
	activePaymentExpiresAt?: string | null;
}
