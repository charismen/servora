import { fail } from '@sveltejs/kit';
import { serverApiRequest, ServerApiError } from '$lib/server/api';
import { can, permissions } from '$lib/auth/permissions';
import type { AttendanceListResponse } from '$lib/types/payroll';
import type { ShopRole, StaffUser } from '$lib/types/shop-users';
import type { Actions, PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ parent, cookies, fetch, url }) => {
	const parentData = await parent();
	const token = cookies.get('servora_session');

	const canManage =
		can(parentData.user.permissions, permissions.attendanceWrite) ||
		can(parentData.user.permissions, permissions.attendanceApprove);

	const now = new Date();
	const defaultFrom = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`;
	const lastDay = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
	const defaultTo = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(lastDay).padStart(2, '0')}`;

	const from = url.searchParams.get('from') || defaultFrom;
	const to = url.searchParams.get('to') || defaultTo;
	const status = url.searchParams.get('status') || '';

	// When user cannot manage attendance, lock to their own attendance only
	const userId = canManage ? (url.searchParams.get('userId') || '') : String(parentData.user.id);
	const roleId = canManage ? (url.searchParams.get('roleId') || '') : '';
	const employmentType = canManage ? (url.searchParams.get('employmentType') || '') : '';

	const query = new URLSearchParams({ from, to });
	if (userId) query.set('userId', userId);
	if (roleId) query.set('roleId', roleId);
	if (status) query.set('status', status);
	if (employmentType) query.set('employmentType', employmentType);

	const attendanceDataPromise = serverApiRequest<AttendanceListResponse>(fetch, `/attendances?${query.toString()}`, { token })
		.catch(() => ({
			from,
			to,
			summary: {
				totalEntries: 0,
				pendingEntries: 0,
				approvedEntries: 0,
				rejectedEntries: 0,
				totalApprovedHours: 0,
				operationalOpenTime: '09:00',
				operationalCloseTime: '18:00',
				defaultBreakMinutes: 60,
				operationalTimeZone: ''
			},
			items: []
		}));

	const staffPromise = canManage
		? serverApiRequest<StaffUser[]>(fetch, '/shop-users/staff', { token }).catch(() => [])
		: Promise.resolve([] as StaffUser[]);

	const rolesPromise = canManage
		? serverApiRequest<ShopRole[]>(fetch, '/shop-users/roles', { token }).catch(() => [])
		: Promise.resolve([] as ShopRole[]);

	const [attendanceData, staff, roles] = await Promise.all([
		attendanceDataPromise,
		staffPromise,
		rolesPromise
	]);

	return {
		canManage,
		attendanceData,
		staff,
		roles,
		filters: {
			from,
			to,
			userId,
			roleId,
			status,
			employmentType
		}
	};
};

export const actions: Actions = {
	saveAttendance: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') ?? 0);
		const userId = Number(form.get('userId') ?? 0);
		const workDate = String(form.get('workDate') ?? '');
		const checkInTime = String(form.get('checkInTime') ?? '09:00');
		const checkOutTime = String(form.get('checkOutTime') ?? '').trim();
		const breakMinutes = Number(form.get('breakMinutes') ?? 60);
		const isAdminOverride = form.get('isAdminOverride') === 'on';
		const overrideReason = String(form.get('overrideReason') ?? '').trim() || null;
		const notes = String(form.get('notes') ?? '').trim() || null;
		const approveImmediately = form.get('approveImmediately') === 'on';

		const checkInAt = `${workDate}T${checkInTime}:00`;
		const checkOutAt = checkOutTime ? `${workDate}T${checkOutTime}:00` : null;

		try {
			await serverApiRequest(fetch, id > 0 ? `/attendances/${id}` : '/attendances', {
				method: id > 0 ? 'PUT' : 'POST',
				token,
				body: {
					userId,
					workDate,
					checkInTime,
					checkOutTime: checkOutTime || null,
					checkInAt,
					checkOutAt,
					breakMinutes,
					isAdminOverride,
					overrideReason,
					notes,
					approveImmediately
				}
			});
			return { success: true, messageKey: id > 0 ? 'attendance.updated' : 'attendance.saved' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, {
					success: false,
					message: error.message
				});
			}
			throw error;
		}
	},
	bulkStatus: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const attendanceIds = form.getAll('attendanceIds').map(Number).filter(Boolean);
		const status = String(form.get('status') ?? 'Approved');

		try {
			await serverApiRequest(fetch, '/attendances/approve-bulk', {
				method: 'POST',
				token,
				body: { attendanceIds, status }
			});
			return { success: true, messageKey: 'attendance.bulkUpdated' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { success: false, message: error.message });
			}
			throw error;
		}
	},
	deleteAttendance: async ({ request, cookies, fetch }) => {
		const token = cookies.get('servora_session');
		const form = await request.formData();
		const id = Number(form.get('id') ?? 0);

		try {
			await serverApiRequest(fetch, `/attendances/${id}`, {
				method: 'DELETE',
				token
			});
			return { success: true, messageKey: 'attendance.deleted' };
		} catch (error) {
			if (error instanceof ServerApiError) {
				return fail(error.status, { success: false, message: error.message });
			}
			throw error;
		}
	}
};

