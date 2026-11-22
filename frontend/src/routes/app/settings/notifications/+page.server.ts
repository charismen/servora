import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type {
	NotificationChannelsDto,
	NotificationDeliveriesPageDto,
	NotificationRuleDto,
	WebhookEndpointDto
} from '$lib/types/notifications';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');
	const tabParam = url.searchParams.get('tab') ?? 'channels';
	const tab = ['channels', 'rules', 'webhooks', 'deliveries'].includes(tabParam) ? tabParam : 'channels';
	const channelFilter = url.searchParams.get('channel') ?? '';
	const statusFilter = url.searchParams.get('status') ?? '';
	const page = Math.max(1, Number(url.searchParams.get('page') ?? 1) || 1);

	const deliveryQuery = new URLSearchParams({ page: String(page), pageSize: '25' });
	if (channelFilter) deliveryQuery.set('channel', channelFilter);
	if (statusFilter) deliveryQuery.set('status', statusFilter);

	const [channels, rules, webhooks, deliveries] = await Promise.all([
		serverApiRequest<NotificationChannelsDto>(fetch, '/settings/notification-channels', { token }),
		serverApiRequest<NotificationRuleDto[]>(fetch, '/settings/notification-rules', { token }),
		serverApiRequest<WebhookEndpointDto[]>(fetch, '/settings/webhooks', { token }),
		serverApiRequest<NotificationDeliveriesPageDto>(fetch, `/settings/notification-deliveries?${deliveryQuery}`, { token })
	]);

	return {
		tab,
		channels,
		rules,
		webhooks,
		deliveries,
		filters: {
			channel: channelFilter,
			status: statusFilter,
			page
		}
	};
};

export const actions: Actions = {
	saveChannels: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();

		const threadIdRaw = String(form.get('telegramThreadId') ?? '').trim();
		const threadId = threadIdRaw ? Number(threadIdRaw) : null;

		const payload = {
			emailEnabled: form.get('emailEnabled') === 'on',
			useSystemSmtp: form.get('useSystemSmtp') === 'on',
			smtpHost: String(form.get('smtpHost') ?? '').trim() || null,
			smtpPort: Number(form.get('smtpPort') ?? 587) || 587,
			smtpUseSsl: form.get('smtpUseSsl') === 'on',
			smtpUsername: String(form.get('smtpUsername') ?? '').trim() || null,
			smtpPassword: String(form.get('smtpPassword') ?? '').trim() || null,
			clearSmtpPassword: form.get('clearSmtpPassword') === 'on',
			fromEmail: String(form.get('fromEmail') ?? '').trim() || null,
			fromName: String(form.get('fromName') ?? '').trim() || null,
			staffNotificationEmails: String(form.get('staffNotificationEmails') ?? '').trim() || null,
			whatsAppEnabled: form.get('whatsAppEnabled') === 'on',
			whatsAppProvider: String(form.get('whatsAppProvider') ?? 'Manual'),
			whatsAppApiUrl: String(form.get('whatsAppApiUrl') ?? '').trim() || null,
			whatsAppPhoneNumberId: String(form.get('whatsAppPhoneNumberId') ?? '').trim() || null,
			whatsAppApiToken: String(form.get('whatsAppApiToken') ?? '').trim() || null,
			clearWhatsAppApiToken: form.get('clearWhatsAppApiToken') === 'on',
			whatsAppAuthHeaderName: String(form.get('whatsAppAuthHeaderName') ?? '').trim() || null,
			whatsAppCustomPayloadTemplate: String(form.get('whatsAppCustomPayloadTemplate') ?? '').trim() || null,
			telegramEnabled: form.get('telegramEnabled') === 'on',
			telegramBotToken: String(form.get('telegramBotToken') ?? '').trim() || null,
			clearTelegramBotToken: form.get('clearTelegramBotToken') === 'on',
			telegramChatId: String(form.get('telegramChatId') ?? '').trim() || null,
			telegramThreadId: Number.isInteger(threadId) ? threadId : null,
			twoFactorEnforcement: String(form.get('twoFactorEnforcement') ?? 'Optional'),
			requireOtpForPublicQuoteApproval: form.get('requireOtpForPublicQuoteApproval') === 'on'
		};

		try {
			const channels = await serverApiRequest<NotificationChannelsDto>(fetch, '/settings/notification-channels', {
				method: 'PUT',
				token,
				body: payload
			});
			return { ok: true, channels, messageKey: 'notifications.channelsSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { ok: false, message: error.message });
			throw error;
		}
	},

	testChannel: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const channel = String(form.get('channel') ?? 'Email');
		const recipient = String(form.get('recipient') ?? '').trim() || null;

		try {
			const result = await serverApiRequest<{ success: boolean; message?: string | null; httpStatusCode?: number | null }>(
				fetch,
				'/settings/notification-channels/test',
				{
					method: 'POST',
					token,
					body: { channel, recipient }
				}
			);
			return {
				ok: result.success,
				testResult: { channel, ...result },
				messageKey: result.success ? 'notifications.testSentSuccess' : undefined,
				message: result.success ? undefined : (result.message ?? 'Test delivery failed.')
			};
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { ok: false, message: error.message });
			throw error;
		}
	},

	saveRules: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const eventTypes = form.getAll('eventType').map(String);

		const rules = eventTypes.map((eventType) => ({
			eventType,
			eventKey: String(form.get(`eventKey_${eventType}`) ?? ''),
			sendCustomerEmail: form.get(`custEmail_${eventType}`) === 'on',
			sendCustomerWhatsApp: form.get(`custWa_${eventType}`) === 'on',
			sendStaffEmail: form.get(`staffEmail_${eventType}`) === 'on',
			sendStaffTelegram: form.get(`staffTg_${eventType}`) === 'on',
			sendWebhook: form.get(`webhook_${eventType}`) === 'on'
		}));

		try {
			const updated = await serverApiRequest<NotificationRuleDto[]>(fetch, '/settings/notification-rules', {
				method: 'PUT',
				token,
				body: { rules }
			});
			return { ok: true, rules: updated, messageKey: 'notifications.rulesSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { ok: false, message: error.message });
			throw error;
		}
	},

	createWebhook: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const name = String(form.get('name') ?? '').trim();
		const targetUrl = String(form.get('targetUrl') ?? '').trim();
		const isActive = form.get('isActive') === 'on';
		const subscribedEvents = form.getAll('subscribedEvents').map(String).filter(Boolean);

		try {
			const res = await serverApiRequest<{ endpoint: WebhookEndpointDto; signingSecret: string }>(
				fetch,
				'/settings/webhooks',
				{
					method: 'POST',
					token,
					body: { name, targetUrl, isActive, subscribedEvents }
				}
			);
			return {
				ok: true,
				revealedSecret: { name: res.endpoint.name, secret: res.signingSecret },
				messageKey: 'notifications.webhookCreated'
			};
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { ok: false, message: error.message });
			throw error;
		}
	},

	toggleWebhook: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id'));
		const name = String(form.get('name') ?? '');
		const targetUrl = String(form.get('targetUrl') ?? '');
		const isActive = form.get('nextActive') === 'true';
		const subscribedEvents = String(form.get('subscribedEventsJson') ?? '[]');
		let parsedEvents: string[] = [];
		try {
			parsedEvents = JSON.parse(subscribedEvents);
		} catch {
			parsedEvents = [];
		}

		try {
			await serverApiRequest(fetch, `/settings/webhooks/${id}`, {
				method: 'PUT',
				token,
				body: { name, targetUrl, isActive, subscribedEvents: parsedEvents }
			});
			return { ok: true, messageKey: 'notifications.webhookUpdated' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { ok: false, message: error.message });
			throw error;
		}
	},

	rotateWebhookSecret: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id'));
		const name = String(form.get('name') ?? 'Webhook');

		try {
			const res = await serverApiRequest<{ signingSecret: string }>(
				fetch,
				`/settings/webhooks/${id}/rotate-secret`,
				{ method: 'POST', token }
			);
			return {
				ok: true,
				revealedSecret: { name, secret: res.signingSecret },
				messageKey: 'notifications.webhookSecretRotated'
			};
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { ok: false, message: error.message });
			throw error;
		}
	},

	testWebhook: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id'));
		try {
			const res = await serverApiRequest<{ success: boolean; httpStatusCode?: number | null; message?: string | null }>(
				fetch,
				`/settings/webhooks/${id}/test`,
				{ method: 'POST', token }
			);
			return {
				ok: res.success,
				messageKey: res.success ? 'notifications.webhookTestSuccess' : undefined,
				message: res.success ? undefined : (res.message ?? 'Webhook ping failed.')
			};
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { ok: false, message: error.message });
			throw error;
		}
	},

	deleteWebhook: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id'));
		try {
			await serverApiRequest(fetch, `/settings/webhooks/${id}`, { method: 'DELETE', token });
			return { ok: true, messageKey: 'notifications.webhookDeleted' };
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { ok: false, message: error.message });
			throw error;
		}
	},

	retryDelivery: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id'));
		try {
			const res = await serverApiRequest<{ success: boolean; message?: string | null }>(
				fetch,
				`/settings/notification-deliveries/${id}/retry`,
				{ method: 'POST', token }
			);
			return {
				ok: res.success,
				messageKey: res.success ? 'notifications.deliveryRetriedSuccess' : undefined,
				message: res.success ? undefined : (res.message ?? 'Retry failed.')
			};
		} catch (error) {
			if (error instanceof ServerApiError) return fail(error.status, { ok: false, message: error.message });
			throw error;
		}
	}
};
