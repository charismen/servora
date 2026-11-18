export const permissions = {
	customersRead: 'Permissions.Customers.Read',
	customersWrite: 'Permissions.Customers.Write',
	customersDelete: 'Permissions.Customers.Delete',
	devicesRead: 'Permissions.Devices.Read',
	devicesWrite: 'Permissions.Devices.Write',
	devicesDelete: 'Permissions.Devices.Delete',
	serviceTicketsRead: 'Permissions.ServiceTickets.Read',
	serviceTicketsWrite: 'Permissions.ServiceTickets.Write',
	serviceTicketsMove: 'Permissions.ServiceTickets.Move',
	serviceTicketsDelete: 'Permissions.ServiceTickets.Delete',
	inventoryRead: 'Permissions.Inventory.Read',
	inventoryWrite: 'Permissions.Inventory.Write',
	inventoryDelete: 'Permissions.Inventory.Delete',
	inventoryCostRead: 'Permissions.Inventory.Cost.Read',
	reportsRead: 'Permissions.Reports.Read',
	settingsRead: 'Permissions.Settings.Read',
	settingsWrite: 'Permissions.Settings.Write',
	attendanceRead: 'Permissions.Attendance.Read',
	attendanceWrite: 'Permissions.Attendance.Write',
	attendanceApprove: 'Permissions.Attendance.Approve',
	payrollRead: 'Permissions.Payroll.Read',
	payrollManage: 'Permissions.Payroll.Manage',
	assetsRead: 'Permissions.Assets.Read',
	assetsWrite: 'Permissions.Assets.Write',
	assetsDelete: 'Permissions.Assets.Delete',
	maintenanceRead: 'Permissions.Maintenance.Read',
	maintenanceWrite: 'Permissions.Maintenance.Write',
	maintenanceDelete: 'Permissions.Maintenance.Delete',
	workspacesManage: 'Permissions.Workspaces.Manage',
	organizationsRead: 'Permissions.Organizations.Read'
} as const;

export function can(userPermissions: string[], permission: string) {
	return userPermissions.includes(permission);
}
