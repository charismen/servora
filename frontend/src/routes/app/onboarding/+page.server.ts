import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { IndustryBlueprintDefinition } from '$lib/types/blueprints';
import type { LoginResponse, WorkspaceSummary } from '$lib/types/auth';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ cookies, fetch, url }) => {
	const token = cookies.get('servora_session');
	if (!token) throw redirect(303, '/login');

	const mode = url.searchParams.get('mode') === 'create' ? 'create' : 'setup';
	const blueprints = await serverApiRequest<IndustryBlueprintDefinition[]>(fetch, '/blueprints', { token }).catch(() => []);

	return { blueprints, wizardMode: mode };
};

export const actions: Actions = {
	submit: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		if (!token) throw redirect(303, '/login');

		const formData = await request.formData();
		const wizardMode = String(formData.get('wizardMode') ?? 'setup');
		const organizationName = String(formData.get('organizationName') ?? '').trim();
		const organizationLogoUrl = String(formData.get('organizationLogoUrl') ?? '').trim() || null;
		const workspaceName = String(formData.get('workspaceName') ?? '').trim();
		const workspaceSlug = String(formData.get('workspaceSlug') ?? '').trim() || null;
		const phoneNumber = String(formData.get('phoneNumber') ?? '').trim() || null;
		const address = String(formData.get('address') ?? '').trim() || null;
		const operatingMode = String(formData.get('operatingMode') ?? 'Commercial');
		const industryBlueprintKey = String(formData.get('industryBlueprintKey') ?? 'it_computer');
		const defaultLocale = String(formData.get('defaultLocale') ?? 'en');
		const timeZone = String(formData.get('timeZone') ?? 'Asia/Jakarta');
		const currencyCode = String(formData.get('currencyCode') ?? 'IDR');
		const replaceStatuses = formData.get('replaceStatuses') === 'on';
		const replaceFieldsAndChecklists = formData.get('replaceFieldsAndChecklists') === 'on';
		const seedSampleData = formData.get('seedSampleData') === 'on';
		const terminologyRaw = String(formData.get('terminologyJson') ?? '');

		let terminology: unknown = null;
		if (terminologyRaw) {
			try {
				terminology = JSON.parse(terminologyRaw);
			} catch {
				terminology = null;
			}
		}

		if (!workspaceName || (wizardMode === 'setup' && !organizationName)) {
			return fail(400, { success: false, message: 'Workspace and organization names are required.' });
		}

		try {
			if (wizardMode === 'create') {
				const created = await serverApiRequest<WorkspaceSummary>(fetch, '/workspaces', {
					method: 'POST',
					token,
					body: {
						name: workspaceName,
						slug: workspaceSlug,
						phoneNumber,
						address,
						operatingMode,
						industryBlueprintKey,
						defaultLocale,
						timeZone,
						currencyCode,
						terminology,
						seedSampleData
					}
				});

				// Automatically switch JWT into the newly created workspace
				const switched = await serverApiRequest<LoginResponse>(fetch, '/auth/switch-workspace', {
					method: 'POST',
					token,
					body: { workspaceId: created.id }
				});
				cookies.set('servora_session', switched.accessToken, {
					path: '/',
					httpOnly: true,
					sameSite: 'lax',
					secure: false,
					maxAge: 60 * 60 * 8
				});

				throw redirect(303, '/app');
			}

			await serverApiRequest(fetch, '/workspaces/current/setup', {
				method: 'PUT',
				token,
				body: {
					organizationName,
					organizationLogoUrl,
					workspaceName,
					workspaceSlug,
					phoneNumber,
					address,
					operatingMode,
					industryBlueprintKey,
					defaultLocale,
					timeZone,
					currencyCode,
					terminology,
					replaceStatuses,
					replaceFieldsAndChecklists,
					seedSampleData
				}
			});

			throw redirect(303, '/app');
		} catch (error) {
			if (error && typeof error === 'object' && 'status' in error && (error as { status: number }).status === 303) {
				throw error;
			}
			if (error instanceof ServerApiError) {
				return fail(error.status, { success: false, message: error.message });
			}
			return fail(500, { success: false, message: 'Setup failed.' });
		}
	}
};
