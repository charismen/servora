import { fail, redirect } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { AssetCategory, AssetDetail } from '$lib/types/assets';
import type { Actions, PageServerLoad } from './$types';

interface StaffUser {
	id: number;
	fullName: string;
	email: string;
	roles: string[];
}

interface ServiceTicketListItem {
	id: number;
	ticketNumber: string;
	device: string;
	status: string;
}

export const load: PageServerLoad = async ({ parent, cookies, fetch, params }) => {
	await parent();
	const token = cookies.get('servora_session');
	const id = Number(params.id);

	try {
		const [asset, categories, staff, tickets] = await Promise.all([
			serverApiRequest<AssetDetail>(fetch, `/assets/${id}`, { token }),
			serverApiRequest<AssetCategory[]>(fetch, '/assets/categories', { token }).catch(() => []),
			serverApiRequest<StaffUser[]>(fetch, '/shop-users/staff', { token }).catch(() => []),
			serverApiRequest<{ items: ServiceTicketListItem[] }>(fetch, '/service-tickets?pageSize=50', { token }).catch(() => ({ items: [] }))
		]);

		return {
			asset,
			categories,
			staff,
			tickets: tickets.items
		};
	} catch {
		throw redirect(303, '/app/assets');
	}
};

export const actions: Actions = {
	updateAsset: async ({ request, cookies, fetch, params }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(params.id);

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
			await serverApiRequest(fetch, `/assets/${id}`, { method: 'PUT', token, body });
			return { success: true, messageKey: 'assets.saved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { error: error.message });
			}
			throw error;
		}
	},

	updateCustody: async ({ request, cookies, fetch, params }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(params.id);

		const body = {
			userId: form.get('userId') ? Number(form.get('userId')) : null,
			actionType: form.get('actionType')?.toString().trim() || 'CheckOut',
			location: form.get('location')?.toString().trim() || null,
			condition: form.get('condition')?.toString().trim() || 'Good',
			notes: form.get('notes')?.toString().trim() || null
		};

		try {
			await serverApiRequest(fetch, `/assets/${id}/custody`, { method: 'POST', token, body });
			return { success: true, messageKey: 'assets.custodyUpdated' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { error: error.message });
			}
			throw error;
		}
	},

	addMaintenance: async ({ request, cookies, fetch, params }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(params.id);

		const body = {
			serviceTicketId: form.get('serviceTicketId') ? Number(form.get('serviceTicketId')) : null,
			maintenanceType: form.get('maintenanceType')?.toString().trim() || 'Preventive',
			title: form.get('title')?.toString().trim() ?? '',
			description: form.get('description')?.toString().trim() || null,
			cost: Number(form.get('cost') ?? 0),
			performedBy: form.get('performedBy')?.toString().trim() || null,
			performedAt: form.get('performedAt') ? new Date(form.get('performedAt')!.toString()).toISOString() : null,
			nextDueAt: form.get('nextDueAt') ? new Date(form.get('nextDueAt')!.toString()).toISOString() : null,
			setUnderMaintenance: form.get('setUnderMaintenance') === 'true'
		};

		try {
			await serverApiRequest(fetch, `/assets/${id}/maintenance`, { method: 'POST', token, body });
			return { success: true, messageKey: 'assets.maintenanceLogged' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { error: error.message });
			}
			throw error;
		}
	},

	disposeAsset: async ({ request, cookies, fetch, params }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(params.id);

		const body = {
			status: form.get('status')?.toString().trim() || 'Sold',
			disposedAt: form.get('disposedAt') ? new Date(form.get('disposedAt')!.toString()).toISOString() : null,
			actualSoldPrice: form.get('actualSoldPrice') ? Number(form.get('actualSoldPrice')) : null,
			disposalNotes: form.get('disposalNotes')?.toString().trim() || null
		};

		try {
			await serverApiRequest(fetch, `/assets/${id}/dispose`, { method: 'POST', token, body });
			return { success: true, messageKey: 'assets.disposed' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { error: error.message });
			}
			throw error;
		}
	},

	deleteAsset: async ({ cookies, fetch, params }) => {
		const token = cookies.get('servora_session');
		const id = Number(params.id);

		try {
			await serverApiRequest(fetch, `/assets/${id}`, { method: 'DELETE', token });
			throw redirect(303, '/app/assets');
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { error: error.message });
			}
			throw error;
		}
	}
};
