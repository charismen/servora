import { fail } from '@sveltejs/kit';
import { resolveSupportedImageMime } from '$lib/media';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import { createTranslator } from '$lib/i18n';
import { sanitizePhoneNumber } from '$lib/utils/phone';
import type { ShopProfileSettings } from '$lib/types/settings';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch }) => {
	const parentData = await parent();
	const token = cookies.get('servora_session');
	const settings = await serverApiRequest<ShopProfileSettings>(fetch, '/settings/shop-profile', { token })
		.catch(() => parentData.shopProfile);

	if (!settings) throw new ServerApiError('Settings could not be loaded.', 503);

	return { settings };
};

export const actions: Actions = {
	save: async ({ request, cookies, fetch, locals }) => {
		const t = createTranslator(locals.locale);
		let payload: ShopProfileSettings | undefined;
		try {
			const token = cookies.get('servora_session');
			const form = await request.formData();
			payload = readSettingsForm(form);
			const logo = form.get('logo');
			let settings = await serverApiRequest<ShopProfileSettings>(fetch, '/settings/shop-profile', {
				method: 'PUT',
				token,
				body: payload
			});
			if (logo && typeof logo !== 'string' && logo.size > 0) {
				if (logo.size > 5 * 1024 * 1024) return fail(400, { settings: payload, message: t('settings.logoTooLarge') });
				const logoMime = resolveSupportedImageMime(logo);
				if (!logoMime) return fail(400, { settings: payload, message: t('settings.logoInvalidType') });
				const normalizedLogo = logo.type.toLowerCase() === logoMime
					? logo
					: new File([logo], logo.name, { type: logoMime, lastModified: logo.lastModified });
				settings = await uploadLogo(fetch, token, normalizedLogo);
			}

			return { settings, message: t('settings.updated') };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					settings: payload,
					message: error.status === 409 ? t('settings.conflict') : error.message
				});
			}

			return fail(400, {
				message: error instanceof Error ? error.message : t('settings.saveFailed')
			});
		}
	},
	resetDefaults: async ({ cookies, fetch, locals }) => {
		const t = createTranslator(locals.locale);
		try {
			const settings = await serverApiRequest<ShopProfileSettings>(fetch, '/settings/shop-profile/reset-defaults', {
				method: 'POST',
				token: cookies.get('servora_session')
			});
			return { settings, message: t('settings.resetDefaultsDone') };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(400, { message: error.message });
			throw error;
		}
	}
};

function readSettingsForm(form: FormData): ShopProfileSettings {
	return {
		shopName: String(form.get('shopName') ?? ''),
		phoneNumber: sanitizePhoneNumber(String(form.get('phoneNumber') ?? '')),
		whatsAppNumber: sanitizePhoneNumber(String(form.get('whatsAppNumber') ?? '')),
		email: String(form.get('email') ?? ''),
		address: String(form.get('address') ?? ''),
		ticketPrefix: String(form.get('ticketPrefix') ?? ''),
		ticketDateFormat: String(form.get('ticketDateFormat') ?? 'yyyyMMdd'),
		ticketStartNumber: numberValue(form, 'ticketStartNumber', 1),
		ticketMinDigits: numberValue(form, 'ticketMinDigits', 4),
		currencyCode: String(form.get('currencyCode') ?? 'IDR') as ShopProfileSettings['currencyCode'],
		receiptFooter: String(form.get('receiptFooter') ?? ''),
		inventoryDefaultTaxPercent: numberValue(form, 'inventoryDefaultTaxPercent', 11),
		inventoryRoundingIncrement: numberValue(form, 'inventoryRoundingIncrement', 1000),
		inventoryDefaultUnit: String(form.get('inventoryDefaultUnit') ?? 'pcs'),
		inventoryAllowDecimalQuantity: form.get('inventoryAllowDecimalQuantity') === 'on',
		inventoryAutoCreateCategoryOnImport: form.get('inventoryAutoCreateCategoryOnImport') === 'on',
		requirePhotoEvidenceOnIntake: form.get('requirePhotoEvidenceOnIntake') === 'on',
		showPhotoEvidenceOnPublicTracking: form.get('showPhotoEvidenceOnPublicTracking') === 'on',
		requireApprovalEstimate: form.get('requireApprovalEstimate') === 'on',
		allowUnpaidPickupOverride: form.get('allowUnpaidPickupOverride') === 'on',
		operationalOpenTime: String(form.get('operationalOpenTime') ?? '09:00'),
		operationalCloseTime: String(form.get('operationalCloseTime') ?? '18:00'),
		operationalRequiredWorkingDaysPerWeek: numberValue(form, 'operationalRequiredWorkingDaysPerWeek', 6),
		operationalRequiredWorkingDaysPerMonth: numberValue(form, 'operationalRequiredWorkingDaysPerMonth', 26),
		operationalDefaultBreakMinutes: numberValue(form, 'operationalDefaultBreakMinutes', 60),
		operationalTimeZone: String(form.get('operationalTimeZone') ?? '').trim() || undefined,
		logoUrl: String(form.get('logoUrl') ?? '') || null,
		updatedAt: String(form.get('updatedAt') ?? '') || null
	};
}

function numberValue(form: FormData, key: string, fallback: number) {
	const value = Number(form.get(key) ?? fallback);
	return Number.isFinite(value) ? value : fallback;
}

async function uploadLogo(fetcher: typeof fetch, token: string | undefined, logo: File) {
	const body = new FormData();
	body.set('file', logo);
	return serverApiRequest<ShopProfileSettings>(fetcher, '/settings/shop-profile/logo', {
		method: 'POST',
		token,
		body
	});
}
