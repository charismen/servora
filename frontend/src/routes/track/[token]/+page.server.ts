import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { TerminologyConfig } from '$lib/types/auth';
import type { FieldValueDto } from '$lib/types/blueprints';
import type { PublicTrackingPaymentSummary } from '$lib/types/payments';
import type { Actions, PageServerLoad } from './$types';

export type PublicTrackingAttachment = {
	fileName: string;
	mimeType: string;
	attachmentType: string;
	url: string;
	createdAt: string;
};

type PublicTracking = {
	shopName: string;
	shopLogoUrl?: string | null;
	operatingMode?: string;
	terminology?: TerminologyConfig | null;
	ticketNumber: string;
	deviceSnapshot: string;
	serviceLocationType?: string;
	siteAddressSnapshot?: string | null;
	scheduledVisitStart?: string | null;
	status: string;
	color: string;
	publicPhotoCount: number;
	publicFields?: FieldValueDto[];
	history: {
		id: number;
		eventType: string;
		fromStatus?: string;
		toStatus?: string;
		note?: string;
		createdAt: string;
	}[];
	attachments: PublicTrackingAttachment[];
	onlinePaymentEnabled?: boolean;
	paymentRequiresPhoneVerification?: boolean;
	paymentStatus?: string | null;
	totalAmount?: number | null;
	amountPaid?: number | null;
	remainingBalance?: number | null;
	currencyCode?: string | null;
	activePaymentUrl?: string | null;
	activePaymentExpiresAt?: string | null;
};

export const load: PageServerLoad = async ({ fetch, params, url }) => {
	const paymentReturn = url.searchParams.get('payment');
	try {
		const tracking = await serverApiRequest<PublicTracking>(fetch, `/track/${params.token}`);
		return { tracking, notFound: false, paymentReturn };
	} catch (error) {
		if (error instanceof ServerApiError && error.status === 404) {
			return { tracking: null, notFound: true, paymentReturn };
		}
		throw error;
	}
};

export const actions: Actions = {
	verifyPhotos: async ({ request, fetch, params }) => {
		const form = await request.formData();
		const phoneLastFour = String(form.get('phoneLastFour') ?? '').replace(/\D/g, '').trim();
		const includePayment = form.get('includePayment') === 'true';
		const includePhotos = form.get('includePhotos') !== 'false';
		if (phoneLastFour.length !== 4) {
			return fail(400, { errorKey: 'tracking.invalidPhoneLastFour' });
		}

		try {
			let attachments: PublicTrackingAttachment[] = [];
			let paymentSummary: PublicTrackingPaymentSummary | null = null;

			if (includePhotos) {
				const response = await serverApiRequest<{ attachments: PublicTrackingAttachment[] }>(
					fetch,
					`/track/${params.token}/verify-photos`,
					{
						method: 'POST',
						body: { phoneLastFour }
					}
				);
				attachments = response.attachments;
			}

			if (includePayment) {
				paymentSummary = await serverApiRequest<PublicTrackingPaymentSummary>(
					fetch,
					`/track/${params.token}/payment-summary`,
					{
						method: 'POST',
						body: { phoneLastFour }
					}
				);
			}

			return { verified: true, verifiedPhoneLastFour: phoneLastFour, attachments, paymentSummary };
		} catch (error) {
			if (error instanceof ServerApiError) {
				if (error.status === 429) {
					return fail(429, { errorKey: 'tracking.tooManyAttempts' });
				}
				return fail(error.status, { errorKey: 'tracking.invalidPhoneLastFour' });
			}
			throw error;
		}
	},

	startCheckout: async ({ request, fetch, params }) => {
		const form = await request.formData();
		const phoneLastFour = String(form.get('phoneLastFour') ?? '').replace(/\D/g, '').trim() || null;

		try {
			const response = await serverApiRequest<{
				paymentUrl: string;
				expiresAt: string;
				amount: number;
				currencyCode: string;
			}>(fetch, `/track/${params.token}/checkout`, {
				method: 'POST',
				body: { phoneLastFour }
			});
			return { checkoutUrl: response.paymentUrl };
		} catch (error) {
			if (error instanceof ServerApiError) {
				if (error.status === 429) {
					return fail(429, { paymentErrorKey: 'tracking.tooManyAttempts' });
				}
				return fail(error.status, { paymentErrorMessage: error.message });
			}
			throw error;
		}
	}
};
