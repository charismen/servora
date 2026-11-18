import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ url }) => {
	throw redirect(301, `/app/users-roles${url.search}`);
};
