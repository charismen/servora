import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import type { BonusType, ShopPermission, ShopRole, ShopUser, ShopUserPage, WageTierBracket } from '$lib/types/shop-users';
import type { Actions, PageServerLoad } from './$types';

const VALID_SORTS = new Set(['name-asc', 'name-desc', 'email-asc', 'status-asc']);
const VALID_STATUSES = new Set(['Active', 'Inactive']);
const VALID_CONTRACTS = new Set(['FullTime', 'Freelance']);

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	await parent();
	const token = cookies.get('servora_session');

	const tab = url.searchParams.get('tab') === 'roles' ? 'roles' : 'users';
	const search = (url.searchParams.get('search') ?? '').trim();
	const statusParam = url.searchParams.get('status') ?? '';
	const status = VALID_STATUSES.has(statusParam) ? statusParam : '';
	const contractParam = url.searchParams.get('contract') ?? '';
	const contract = VALID_CONTRACTS.has(contractParam) ? contractParam : '';
	const roleIdRaw = Number(url.searchParams.get('roleId') ?? 0);
	const roleId = Number.isInteger(roleIdRaw) && roleIdRaw > 0 ? roleIdRaw : 0;
	const sortParam = url.searchParams.get('sort') ?? 'name-asc';
	const sort = VALID_SORTS.has(sortParam) ? sortParam : 'name-asc';
	const page = Math.max(1, Number(url.searchParams.get('page') ?? 1) || 1);
	const pageSize = Math.min(100, Math.max(5, Number(url.searchParams.get('pageSize') ?? 10) || 10));

	const hasLocalFilters = Boolean(status || contract || roleId);

	const query = new URLSearchParams({
		page: String(page),
		pageSize: String(pageSize),
		sort
	});
	if (search) query.set('search', search);

	const [backendUsersPage, directoryPage, roles, permissions, bonusTypes] = await Promise.all([
		hasLocalFilters ? Promise.resolve(null) : serverApiRequest<ShopUserPage>(fetch, `/shop-users?${query}`, { token }),
		serverApiRequest<ShopUserPage>(fetch, '/shop-users?page=1&pageSize=100&sort=name-asc', { token }),
		serverApiRequest<ShopRole[]>(fetch, '/shop-users/roles', { token }),
		serverApiRequest<ShopPermission[]>(fetch, '/shop-users/permissions', { token }),
		serverApiRequest<BonusType[]>(fetch, '/shop-users/bonus-types', { token }).catch(() => [])
	]);

	let users: ShopUserPage;
	if (hasLocalFilters && directoryPage.totalItems <= 100) {
		const filtered = filterShopUsers(directoryPage.items, { search, status, contract, roleId });
		const sorted = sortShopUsers(filtered, sort);
		const totalItems = sorted.length;
		const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
		const safePage = Math.min(page, totalPages);
		const start = (safePage - 1) * pageSize;
		users = {
			items: sorted.slice(start, start + pageSize),
			page: safePage,
			pageSize,
			totalItems,
			totalPages
		};
	} else if (backendUsersPage) {
		users = backendUsersPage;
	} else {
		users = await serverApiRequest<ShopUserPage>(fetch, `/shop-users?${query}`, { token });
	}

	const roleCounts: Record<number, number> = {};
	let activeCount = 0;
	let freelanceCount = 0;
	for (const member of directoryPage.items) {
		if (member.status === 'Active') activeCount += 1;
		if (member.employmentType === 'Freelance') freelanceCount += 1;
		for (const role of member.roles) {
			roleCounts[role.id] = (roleCounts[role.id] ?? 0) + 1;
		}
	}

	return {
		tab,
		users,
		roles,
		permissions,
		bonusTypes,
		summary: {
			totalUsers: directoryPage.totalItems || directoryPage.items.length,
			activeUsers: activeCount,
			freelanceUsers: freelanceCount,
			rolesCount: roles.length,
			permissionsCount: permissions.length,
			roleCounts
		},
		filters: {
			search,
			status,
			contract,
			roleId,
			sort
		}
	};
};

function filterShopUsers(
	items: ShopUser[],
	filters: { search: string; status: string; contract: string; roleId: number }
): ShopUser[] {
	const term = filters.search.toLowerCase();
	return items.filter((user) => {
		if (filters.status && user.status !== filters.status) return false;
		if (filters.contract && user.employmentType !== filters.contract) return false;
		if (filters.roleId && !user.roles.some((r) => r.id === filters.roleId)) return false;
		if (term) {
			const haystack = `${user.fullName} ${user.email} ${user.status} ${user.employmentType}`.toLowerCase();
			if (!haystack.includes(term)) return false;
		}
		return true;
	});
}

function sortShopUsers(items: ShopUser[], sort: string): ShopUser[] {
	const copy = [...items];
	copy.sort((a, b) => {
		switch (sort) {
			case 'name-desc':
				return b.fullName.localeCompare(a.fullName);
			case 'email-asc':
				return a.email.localeCompare(b.email) || a.fullName.localeCompare(b.fullName);
			case 'status-asc':
				return a.status.localeCompare(b.status) || a.fullName.localeCompare(b.fullName);
			default:
				return a.fullName.localeCompare(b.fullName);
		}
	});
	return copy;
}

export const actions: Actions = {
	createUser: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const email = String(form.get('email') ?? '');
		const firstName = String(form.get('firstName') ?? '');
		const lastName = String(form.get('lastName') ?? '');
		const employmentType = String(form.get('employmentType') ?? 'FullTime');
		const roleIds = form.getAll('roleIds').map(Number).filter(Boolean);

		try {
			await serverApiRequest(fetch, '/shop-users', {
				method: 'POST',
				token,
				body: {
					email,
					password: String(form.get('password') ?? ''),
					firstName,
					lastName,
					roleIds,
					employmentType
				}
			});
			return { success: true, messageKey: 'settings.userSaved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					success: false,
					message: error.message,
					email,
					firstName,
					lastName,
					employmentType,
					roleIds
				});
			}
			throw error;
		}
	},
	updateRoles: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const userId = Number(form.get('userId'));
		const roleIds = form.getAll('roleIds').map(Number).filter(Boolean);

		return save(fetch, `/shop-users/${userId}/roles`, 'PUT', token, { roleIds }, 'settings.userRolesUpdated');
	},
	updateUser: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const userId = Number(form.get('userId'));
		const email = String(form.get('email') ?? '').trim();
		const password = String(form.get('password') ?? '').trim();
		try {
			await serverApiRequest(fetch, `/shop-users/${userId}`, {
				method: 'PUT',
				token,
				body: {
					firstName: String(form.get('firstName') ?? ''),
					lastName: String(form.get('lastName') ?? ''),
					email: email || undefined,
					password: password || undefined,
					employmentType: String(form.get('employmentType') ?? '') || undefined
				}
			});
			return { success: true, updatedUserId: userId, messageKey: 'settings.userUpdated' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { success: false, updatedUserId: userId, message: error.message });
			}
			throw error;
		}
	},
	setEmploymentType: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const userId = Number(form.get('userId'));
		const employmentType = String(form.get('employmentType') ?? 'FullTime');
		return save(fetch, `/shop-users/${userId}/employment-type`, 'PUT', token, { employmentType }, 'settings.employmentTypeUpdated');
	},
	saveFreelanceProfile: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const userId = Number(form.get('userId'));
		const basePayMode = String(form.get('basePayMode') ?? 'TieredDaily');
		const baseHourlyRate = Number(String(form.get('baseHourlyRate') ?? 0).replace(/[^\d.-]/g, '')) || 0;
		const tieredRatesRaw = String(form.get('tieredRatesJson') ?? '[]');
		let tieredRates: WageTierBracket[] = [];
		try {
			tieredRates = JSON.parse(tieredRatesRaw);
		} catch {
			tieredRates = [];
		}

		const maintenanceActive = form.get('maintenanceActive') === 'on';
		const maintenanceMode = String(form.get('maintenanceMode') ?? 'Simple');
		const maintenanceThreshold = Number(form.get('maintenanceThreshold') ?? 4) || 0;
		const maintenanceRewardPerUnit = Number(String(form.get('maintenanceRewardPerUnit') ?? 15000).replace(/[^\d.-]/g, '')) || 0;
		const maintenanceFlatReward = Number(String(form.get('maintenanceFlatReward') ?? 0).replace(/[^\d.-]/g, '')) || 0;
		const maintenanceCountAll = form.get('maintenanceCountAll') === 'on';
		const maintenanceIsRetroactive = form.get('maintenanceIsRetroactive') === 'on' || form.get('maintenanceIsRetroactive') === 'true';
		const maintenanceMilestonesRaw = String(form.get('maintenanceMilestonesJson') ?? '[]');
		let maintenanceMilestones = [];
		try {
			maintenanceMilestones = JSON.parse(maintenanceMilestonesRaw);
		} catch {
			maintenanceMilestones = [];
		}

		const cancellationActive = form.get('cancellationActive') === 'on';
		const cancellationFeePerIncident = Number(String(form.get('cancellationFeePerIncident') ?? 25000).replace(/[^\d.-]/g, '')) || 0;
		const cancellationRequireEvidence = form.get('cancellationRequireEvidence') === 'on';

		const taskItemizedActive = form.get('taskItemizedActive') === 'on';
		const taskItemizedRulesRaw = String(form.get('taskItemizedRulesJson') ?? '[]');
		let taskItemizedRules = [];
		try {
			taskItemizedRules = JSON.parse(taskItemizedRulesRaw);
		} catch {
			taskItemizedRules = [];
		}

		const bonusConfigs = [
			{
				bonusTypeCode: 'MAINTENANCE_COMPLETION',
				isActive: maintenanceActive,
				parameters: {
					threshold: maintenanceThreshold,
					rewardPerUnit: maintenanceRewardPerUnit,
					flatReward: maintenanceFlatReward,
					countAllUnitsOnceThresholdMet: maintenanceCountAll,
					mode: maintenanceMode,
					isRetroactive: maintenanceIsRetroactive,
					milestones: maintenanceMilestones
				}
			},
			{
				bonusTypeCode: 'CUSTOMER_CANCELLATION',
				isActive: cancellationActive,
				parameters: {
					feePerIncident: cancellationFeePerIncident,
					requireDiagnosisOrInspectionHistory: cancellationRequireEvidence
				}
			},
			{
				bonusTypeCode: 'TASK_ITEMIZED',
				isActive: taskItemizedActive,
				parameters: {
					rules: taskItemizedRules
				}
			}
		];

		return save(
			fetch,
			`/shop-users/${userId}/freelance-profile`,
			'PUT',
			token,
			{ basePayMode, baseHourlyRate, tieredRates, bonusConfigs },
			'settings.freelanceProfileSaved'
		);
	},
	setStatus: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const action = String(form.get('status') ?? '') === 'Active' ? 'deactivate' : 'reactivate';
		return save(fetch, `/shop-users/${form.get('userId')}/${action}`, 'POST', token, {}, 'settings.userStatusUpdated');
	},
	saveRole: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const roleId = Number(form.get('roleId'));
		return save(
			fetch,
			roleId ? `/shop-users/roles/${roleId}` : '/shop-users/roles',
			roleId ? 'PUT' : 'POST',
			token,
			{
				name: String(form.get('name') ?? ''),
				description: nullable(form.get('description')),
				permissions: form.getAll('permissions').map(String)
			},
			'settings.roleSaved'
		);
	},
	deleteRole: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		return save(fetch, `/shop-users/roles/${form.get('roleId')}`, 'DELETE', token, undefined, 'settings.roleDeleted');
	}
};

async function save(
	fetcher: typeof fetch,
	path: string,
	method: 'POST' | 'PUT' | 'DELETE',
	token: string | undefined,
	body: unknown,
	messageKey: string
) {
	try {
		await serverApiRequest(fetcher, path, { method, token, body });
		return { success: true, messageKey };
	} catch (error) {
		if (error instanceof ServerApiError) return fail(error.status, { success: false, message: error.message });
		throw error;
	}
}

function nullable(value: FormDataEntryValue | null) {
	const text = String(value ?? '').trim();
	return text.length > 0 ? text : null;
}
