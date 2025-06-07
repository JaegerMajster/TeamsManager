using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter mapujący typ operacji na odpowiednią ikonę Material Design
    /// </summary>
    public class OperationTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string operationType)
                return PackIconKind.HelpCircle;

            return operationType.ToLowerInvariant() switch
            {
                // Team Operations
                "teamcreated" or "createteam" => PackIconKind.AccountMultiplePlus,
                "teamupdated" or "updateteam" => PackIconKind.AccountMultiple,
                "teamdeleted" or "deleteteam" => PackIconKind.AccountMultipleMinus,
                "teamarchived" or "archiveteam" => PackIconKind.Archive,
                "teamrestored" or "restoreteam" => PackIconKind.Restore,
                "teamcloned" or "cloneteam" => PackIconKind.ContentCopy,

                // User Operations
                "usercreated" or "createuser" => PackIconKind.AccountPlus,
                "userupdated" or "updateuser" => PackIconKind.AccountEdit,
                "userdeleted" or "deleteuser" => PackIconKind.AccountMinus,
                "useractivated" or "activateuser" => PackIconKind.AccountCheck,
                "userdeactivated" or "deactivateuser" => PackIconKind.AccountCancel,
                "userimported" or "importusers" => PackIconKind.AccountMultipleOutline,

                // Group Operations
                "groupcreated" or "creategroup" => PackIconKind.AccountGroup,
                "groupupdated" or "updategroup" => PackIconKind.AccountGroupOutline,
                "groupdeleted" or "deletegroup" => PackIconKind.AccountMultipleRemove,
                "groupmemberadded" or "addgroupmember" => PackIconKind.AccountMultiplePlus,
                "groupmemberremoved" or "removegroupmember" => PackIconKind.AccountMultipleMinus,

                // System Operations
                "systembackup" or "backup" => PackIconKind.CloudUpload,
                "systemrestore" or "restore" => PackIconKind.CloudDownload,
                "systemupdate" or "update" => PackIconKind.Update,
                "systemmaintenance" or "maintenance" => PackIconKind.Wrench,
                "systemmonitoring" or "monitoring" => PackIconKind.Monitor,

                // Data Operations
                "dataimport" or "import" => PackIconKind.DatabaseImport,
                "dataexport" or "export" => PackIconKind.DatabaseExport,
                "datasync" or "sync" => PackIconKind.Sync,
                "datamigration" or "migration" => PackIconKind.DatabaseArrowRight,
                "datacleanup" or "cleanup" => PackIconKind.DatabaseRemove,

                // Security Operations
                "securityaudit" or "audit" => PackIconKind.Security,
                "securitypolicyupdated" or "updatepolicy" => PackIconKind.Shield,
                "securityalert" or "securityevent" => PackIconKind.ShieldAlert,
                "passwordreset" or "resetpassword" => PackIconKind.KeyVariant,
                "permissionchanged" or "changepermission" => PackIconKind.KeyChange,

                // Notification Operations
                "notificationsent" or "sendnotification" => PackIconKind.EmailSend,
                "emailsent" or "sendemail" => PackIconKind.Email,
                "smssent" or "sendsms" => PackIconKind.Message,
                "alertsent" or "sendalert" => PackIconKind.Bell,

                // Configuration Operations
                "configurationupdated" or "updateconfig" => PackIconKind.Settings,
                "settingchanged" or "changesetting" => PackIconKind.Cog,
                "templateupdated" or "updatetemplate" => PackIconKind.FileDocument,
                "policyupdated" or "updatepolicy" => PackIconKind.FileDocumentEdit,

                // Report Operations
                "reportgenerated" or "generatereport" => PackIconKind.FileChart,
                "reportemailed" or "emailreport" => PackIconKind.FileChartOutline,
                "analyticsrun" or "runanalytics" => PackIconKind.ChartLine,
                "dashboard" or "dashboardupdate" => PackIconKind.ViewDashboard,

                // File Operations
                "fileupload" or "upload" => PackIconKind.CloudUpload,
                "filedownload" or "download" => PackIconKind.CloudDownload,
                "filedeleted" or "deletefile" => PackIconKind.FileRemove,
                "fileprocessed" or "processfile" => PackIconKind.FileDocumentEdit,

                // PowerShell Operations
                "powershellscript" or "scriptexecution" => PackIconKind.Console,
                "commandexecution" or "executecommand" => PackIconKind.Console,
                "automationrun" or "automation" => PackIconKind.Robot,

                // Calendar/School Operations
                "schoolyearstarted" or "startschoolyear" => PackIconKind.CalendarStart,
                "schoolyearended" or "endschoolyear" => PackIconKind.CalendarEnd,
                "semesterchanged" or "changesemester" => PackIconKind.Calendar,
                "classscheduled" or "scheduleclass" => PackIconKind.CalendarClock,

                // License Operations
                "licenseassigned" or "assignlicense" => PackIconKind.License,
                "licenserevoked" or "revokelicense" => PackIconKind.License,
                "licenseexpired" or "expirelicense" => PackIconKind.License,

                // Department Operations
                "departmentcreated" or "createdepartment" => PackIconKind.Domain,
                "departmentupdated" or "updatedepartment" => PackIconKind.DomainPlus,
                "departmentdeleted" or "deletedepartment" => PackIconKind.Domain,
                "departmentmoved" or "movedepartment" => PackIconKind.FileMove,

                // Default dla nieznanych typów
                _ => PackIconKind.HelpCircle
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for OperationTypeToIconConverter");
        }
    }
} 