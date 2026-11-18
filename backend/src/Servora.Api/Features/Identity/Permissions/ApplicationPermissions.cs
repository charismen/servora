namespace Servora.Api.Features.Identity.Permissions;

public static class ApplicationPermissions
{
    public const string CustomersRead = "Permissions.Customers.Read";
    public const string CustomersWrite = "Permissions.Customers.Write";
    public const string CustomersDelete = "Permissions.Customers.Delete";
    public const string DevicesRead = "Permissions.Devices.Read";
    public const string DevicesWrite = "Permissions.Devices.Write";
    public const string DevicesDelete = "Permissions.Devices.Delete";

    public const string ServiceTicketsRead = "Permissions.ServiceTickets.Read";
    public const string ServiceTicketsWrite = "Permissions.ServiceTickets.Write";
    public const string ServiceTicketsMove = "Permissions.ServiceTickets.Move";
    public const string ServiceTicketsDelete = "Permissions.ServiceTickets.Delete";

    public const string InventoryRead = "Permissions.Inventory.Read";
    public const string InventoryWrite = "Permissions.Inventory.Write";
    public const string InventoryDelete = "Permissions.Inventory.Delete";
    public const string InventoryCostRead = "Permissions.Inventory.Cost.Read";

    public const string SettingsRead = "Permissions.Settings.Read";
    public const string SettingsWrite = "Permissions.Settings.Write";
    public const string ReportsRead = "Permissions.Reports.Read";

    public const string AttendanceRead = "Permissions.Attendance.Read";
    public const string AttendanceWrite = "Permissions.Attendance.Write";
    public const string AttendanceApprove = "Permissions.Attendance.Approve";
    public const string PayrollRead = "Permissions.Payroll.Read";
    public const string PayrollManage = "Permissions.Payroll.Manage";

    public const string AssetsRead = "Permissions.Assets.Read";
    public const string AssetsWrite = "Permissions.Assets.Write";
    public const string AssetsDelete = "Permissions.Assets.Delete";

    public const string MaintenanceRead = "Permissions.Maintenance.Read";
    public const string MaintenanceWrite = "Permissions.Maintenance.Write";
    public const string MaintenanceDelete = "Permissions.Maintenance.Delete";

    public const string WorkspacesManage = "Permissions.Workspaces.Manage";
    public const string OrganizationsRead = "Permissions.Organizations.Read";

    public static readonly string[] All =
    [
        CustomersRead,
        CustomersWrite,
        CustomersDelete,
        DevicesRead,
        DevicesWrite,
        DevicesDelete,
        ServiceTicketsRead,
        ServiceTicketsWrite,
        ServiceTicketsMove,
        ServiceTicketsDelete,
        InventoryRead,
        InventoryWrite,
        InventoryDelete,
        InventoryCostRead,
        SettingsRead,
        SettingsWrite,
        ReportsRead,
        AttendanceRead,
        AttendanceWrite,
        AttendanceApprove,
        PayrollRead,
        PayrollManage,
        AssetsRead,
        AssetsWrite,
        AssetsDelete,
        MaintenanceRead,
        MaintenanceWrite,
        MaintenanceDelete,
        WorkspacesManage,
        OrganizationsRead
    ];
}
