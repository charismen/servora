import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { TerminologyConfig } from '$lib/types/auth';
import type { FieldValueDto } from '$lib/types/blueprints';
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
};

export const load: PageServerLoad = async ({ fetch, params }) => {
	try {
		const tracking = await serverApiRequest<PublicTracking>(fetch, `/track/${params.token}`);
		return { tracking, notFound: false };
	} catch (error) {
		if (error instanceof ServerApiError && error.status === 404) {
			return { tracking: null, notFound: true };
		}
		throw error;
	}
};

export const actions: Actions = {
	verifyPhotos: async ({ request, fetch, params }) => {
		const form = await request.formData();
		const phoneLastFour = String(form.get('phoneLastFour') ?? '').replace(/\D/g, '').trim();
		if (phoneLastFour.length !== 4) {
			return fail(400, { errorKey: 'tracking.invalidPhoneLastFour' });
		}

		try {
			const response = await serverApiRequest<{ attachments: PublicTrackingAttachment[] }>(
				fetch,
				`/track/${params.token}/verify-photos`,
				{
					method: 'POST',
					body: { phoneLastFour }
				}
			);
			return { verified: true, attachments: response.attachments };
		} catch (error) {
			if (error instanceof ServerApiError) {
				if (error.status === 429) {
					return fail(429, { errorKey: 'tracking.tooManyAttempts' });
				}
				return fail(error.status, { errorKey: 'tracking.invalidPhoneLastFour' });
			}
			throw error;
		}
	}
};
