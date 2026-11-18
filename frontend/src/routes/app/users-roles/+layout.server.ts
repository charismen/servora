import { redirect } from '@sveltejs/kit';
import { can, permissions } from '$lib/auth/permissions';
import type { LayoutServerLoad } from './$types';

export const load: LayoutServerLoad = async ({ parent }) => {
	const parentData = await parent();
	if (!can(parentData.user.permissions, permissions.settingsRead)) {
		throw redirect(303, '/app');
	}
	return {};
};
