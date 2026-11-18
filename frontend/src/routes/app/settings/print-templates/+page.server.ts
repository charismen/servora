import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import { createTranslator } from '$lib/i18n';
import type { PrintTemplateSettings, ShopProfileSettings } from '$lib/types/settings';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch }) => {
	await parent();
	const token = cookies.get('servora_session');
	const [settings, profile] = await Promise.all([
		serverApiRequest<PrintTemplateSettings>(fetch, '/settings/print-template', { token }),
		serverApiRequest<ShopProfileSettings>(fetch, '/settings/shop-profile', { token })
	]);
	return { settings, profile };
};

export const actions: Actions = {
	default: async ({ request, cookies, fetch, locals }) => {
		const t = createTranslator(locals.locale);
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const payload: PrintTemplateSettings = {
			termsAndConditions: String(form.get('termsAndConditions') ?? ''),
			showPhysicalCondition: form.get('showPhysicalCondition') === 'on',
			showAccessories: form.get('showAccessories') === 'on',
			showPhotoNotes: form.get('showPhotoNotes') === 'on',
			showQrCode: form.get('showQrCode') === 'on',
			showPhoneNumber: form.get('showPhoneNumber') === 'on',
			showPaymentSummary: form.get('showPaymentSummary') === 'on'
		};

		try {
			const settings = await serverApiRequest<PrintTemplateSettings>(fetch, '/settings/print-template', {
				method: 'PUT',
				token,
				body: payload
			});
			return { settings, message: t('settings.printTemplatesUpdated') };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { settings: payload, message: error.message });
			throw error;
		}
	}
};
