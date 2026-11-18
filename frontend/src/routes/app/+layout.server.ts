import { redirect } from '@sveltejs/kit';
import { serverApiRequest } from '$lib/server/api';
import { can, permissions } from '$lib/auth/permissions';
import type { CurrentUser } from '$lib/types/auth';
import type { ShopProfileSettings } from '$lib/types/settings';
import type { ActivityPage } from '$lib/types/activity';
import type { WorkspaceBlueprintConfig } from '$lib/types/blueprints';
import type { LayoutServerLoad } from './$types';

export const load: LayoutServerLoad = async ({ cookies, fetch }) => {
	const token = cookies.get('servora_session');

	if (!token) {
		throw redirect(303, '/login');
	}

	try {
		const [user, shopProfile, blueprintConfig] = await Promise.all([
			serverApiRequest<CurrentUser>(fetch, '/auth/me', { token }),
			serverApiRequest<ShopProfileSettings>(fetch, '/settings/shop-profile', { token }).catch(() => null),
			serverApiRequest<WorkspaceBlueprintConfig>(fetch, '/blueprints/workspace-config', { token }).catch(() => null)
		]);
		const activityPage = can(user.permissions, permissions.reportsRead)
			? await serverApiRequest<ActivityPage>(fetch, '/activity?page=1&pageSize=5', { token }).catch(() => null)
			: null;
		return { user, shopProfile, blueprintConfig, recentActivities: activityPage?.items ?? [] };
	} catch {
		cookies.delete('servora_session', { path: '/' });
		throw redirect(303, '/login');
	}
};
