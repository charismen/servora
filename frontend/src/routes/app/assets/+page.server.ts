import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { AssetCategory, AssetsPage } from '$lib/types/assets';
import type { Actions, PageServerLoad } from './$types';

interface StaffUser {
	id: number;
	fullName: string;
	email: string;
	roles: string[];
}

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');

	const query = new URLSearchParams();
	for (const key of ['search', 'categoryId', 'status', 'condition', 'assignedUserId', 'companyName', 'eolOnly', 'sort', 'page', 'pageSize']) {
		const val = url.searchParams.get(key);
		if (val) query.set(key, val);
	}
	if (!query.has('page')) query.set('page', '1');
	if (!query.has('pageSize')) query.set('pageSize', '25');

	const suffix = `?${query.toString()}`;

	const [assetsPage, categories, staff] = await Promise.all([
		serverApiRequest<AssetsPage>(fetch, `/assets${suffix}`, { token }).catch(() => ({
			items: [],
			summary: {
				totalAssets: 0,
				activeCount: 0,
				availableCount: 0,
				underMaintenanceCount: 0,
				eolApproachingCount: 0,
				totalPurchaseCost: 0,
				totalNetBookValue: 0,
				totalAccumulatedDepreciation: 0,
				monthlyDepreciationExpense: 0,
				totalEstimatedSellingValue: 0,
				totalMaintenanceCost: 0
			},
			totalCount: 0,
			page: 1,
			pageSize: 25
		})),
		serverApiRequest<AssetCategory[]>(fetch, '/assets/categories', { token }).catch(() => []),
		serverApiRequest<StaffUser[]>(fetch, '/shop-users/staff', { token }).catch(() => [])
	]);

	return {
		assetsPage,
		categories,
		staff,
		filters: Object.fromEntries(query)
	};
};

export const actions: Actions = {
	saveAsset: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = form.get('id') ? Number(form.get('id')) : null;

		const body = {
			assetTag: form.get('assetTag')?.toString().trim() || null,
			name: form.get('name')?.toString().trim() ?? '',
			categoryId: form.get('categoryId') ? Number(form.get('categoryId')) : null,
			description: form.get('description')?.toString().trim() || null,
			companyName: form.get('companyName')?.toString().trim() || null,
			department: form.get('department')?.toString().trim() || null,
			location: form.get('location')?.toString().trim() || null,
			brand: form.get('brand')?.toString().trim() || null,
			model: form.get('model')?.toString().trim() || null,
			serialNumber: form.get('serialNumber')?.toString().trim() || null,
			status: form.get('status')?.toString().trim() || 'Active',
			condition: form.get('condition')?.toString().trim() || 'Good',
			assignedUserId: form.get('assignedUserId') ? Number(form.get('assignedUserId')) : null,
			supplierName: form.get('supplierName')?.toString().trim() || null,
			invoiceNumber: form.get('invoiceNumber')?.toString().trim() || null,
			purchaseDate: form.get('purchaseDate') ? new Date(form.get('purchaseDate')!.toString()).toISOString() : null,
			inServiceDate: form.get('inServiceDate') ? new Date(form.get('inServiceDate')!.toString()).toISOString() : null,
			warrantyExpiresAt: form.get('warrantyExpiresAt') ? new Date(form.get('warrantyExpiresAt')!.toString()).toISOString() : null,
			purchaseCost: Number(form.get('purchaseCost') ?? 0),
			salvageValue: Number(form.get('salvageValue') ?? 0),
			usefulLifeMonths: Number(form.get('usefulLifeMonths') ?? 36),
			depreciationMethod: form.get('depreciationMethod')?.toString().trim() || 'StraightLine',
			estimatedSellingValue: form.get('estimatedSellingValue') ? Number(form.get('estimatedSellingValue')) : null
		};

		try {
			if (id) {
				await serverApiRequest(fetch, `/assets/${id}`, { method: 'PUT', token, body });
			} else {
				await serverApiRequest(fetch, '/assets', { method: 'POST', token, body });
			}
			return { success: true, messageKey: 'assets.saved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { error: error.message });
			}
			throw error;
		}
	},

	deleteAsset: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id'));

		try {
			await serverApiRequest(fetch, `/assets/${id}`, { method: 'DELETE', token });
			return { success: true, messageKey: 'assets.deleted' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { error: error.message });
			}
			throw error;
		}
	},

	saveCategory: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = form.get('id') ? Number(form.get('id')) : null;

		const body = {
			name: form.get('name')?.toString().trim() ?? '',
			description: form.get('description')?.toString().trim() || null,
			defaultUsefulLifeMonths: form.get('defaultUsefulLifeMonths') ? Number(form.get('defaultUsefulLifeMonths')) : 36,
			defaultSalvagePercent: form.get('defaultSalvagePercent') ? Number(form.get('defaultSalvagePercent')) : 0,
			isActive: form.get('isActive') !== 'false'
		};

		try {
			if (id) {
				await serverApiRequest(fetch, `/assets/categories/${id}`, { method: 'PUT', token, body });
			} else {
				await serverApiRequest(fetch, '/assets/categories', { method: 'POST', token, body });
			}
			return { success: true, messageKey: 'common.saved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { error: error.message });
			}
			throw error;
		}
	}
};
