import { fail } from '@sveltejs/kit';
import { createTranslator } from '$lib/i18n';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { PaymentGatewaySettings, PaymentGatewayTestResult } from '$lib/types/payments';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch }) => {
	await parent();
	const token = cookies.get('servora_session');
	const settings = await serverApiRequest<PaymentGatewaySettings>(fetch, '/settings/payment-gateway', { token });
	return { settings };
};

export const actions: Actions = {
	save: async ({ request, cookies, fetch, locals }) => {
		const t = createTranslator(locals.locale);
		const token = cookies.get('servora_session');
		const form = await request.formData();

		const payload = {
			provider: String(form.get('provider') ?? 'None'),
			environment: String(form.get('environment') ?? 'Sandbox'),
			isEnabled: form.get('isEnabled') === 'on',
			apiKey: String(form.get('apiKey') ?? '').trim() || null,
			clearApiKey: form.get('clearApiKey') === 'on',
			webhookSecret: String(form.get('webhookSecret') ?? '').trim() || null,
			clearWebhookSecret: form.get('clearWebhookSecret') === 'on',
			defaultLinkExpiryHours: Number(form.get('defaultLinkExpiryHours') ?? 24) || 24,
			enablePublicTrackingPayment: form.get('enablePublicTrackingPayment') === 'on',
			requirePhoneVerificationForPublicPayment: form.get('requirePhoneVerificationForPublicPayment') === 'on'
		};

		try {
			const settings = await serverApiRequest<PaymentGatewaySettings>(fetch, '/settings/payment-gateway', {
				method: 'PUT',
				token,
				body: payload
			});
			return { settings, message: t('settings.paymentGatewaySaved'), ok: true };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { message: error.message, ok: false });
			}
			throw error;
		}
	},

	rotateWebhook: async ({ cookies, fetch, locals }) => {
		const t = createTranslator(locals.locale);
		const token = cookies.get('servora_session');
		try {
			const settings = await serverApiRequest<PaymentGatewaySettings>(
				fetch,
				'/settings/payment-gateway/rotate-webhook-token',
				{ method: 'POST', token }
			);
			return { settings, message: t('settings.paymentGatewayRotated'), ok: true };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { message: error.message, ok: false });
			}
			throw error;
		}
	},

	testConnection: async ({ cookies, fetch }) => {
		const token = cookies.get('servora_session');
		try {
			const testResult = await serverApiRequest<PaymentGatewayTestResult>(
				fetch,
				'/settings/payment-gateway/test',
				{ method: 'POST', token }
			);
			return { testResult, ok: testResult.success };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { testResult: { success: false, message: error.message }, ok: false });
			}
			throw error;
		}
	}
};
