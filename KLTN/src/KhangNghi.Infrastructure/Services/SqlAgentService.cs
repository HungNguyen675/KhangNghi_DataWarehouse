using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Dapper;
using KhangNghi.Core.Configuration;
using KhangNghi.Core.DTOs;
using KhangNghi.Core.Models;
using KhangNghi.Infrastructure.Data;

namespace KhangNghi.Infrastructure.Services
{
    public interface ISqlAgentService
    {
        Task EnsureJobExistsAsync(string? dtexecPath = null, string? packagePath = null);
        Task<SysSchedule> GetScheduleAsync();
        Task<bool> UpdateScheduleAsync(bool isEnabled, int hour, int minute, string freq = "Daily");
        Task<bool> TriggerJobNowAsync();
        Task<List<JobHistoryDto>> GetJobHistoryAsync(int top = 20);
        Task<bool> IsAgentRunningAsync();
    }

    public class SqlAgentService : ISqlAgentService
    {
        private readonly IDatabaseConnectionFactory _connectionFactory;
        private const string DefaultJobName = "KhangNghi_ETL_Daily_Job";
        private readonly EtlSettingsOptions _settings;
        private readonly IDatabaseConnectionFactory _managementConnectionFactory;

        public SqlAgentService(
            IDatabaseConnectionFactory connectionFactory,
            EtlSettingsOptions? etlSettings = null,
            IDatabaseConnectionFactory? managementConnectionFactory = null)
        {
            // Instance có SQL Server Agent: HungNguyen\SQL2025.
            _connectionFactory = connectionFactory;

            // Database quản trị trên HungNguyen.
            _managementConnectionFactory =
                managementConnectionFactory ?? connectionFactory;

            _settings = etlSettings ?? new EtlSettingsOptions();
        }

        public async Task<bool> IsAgentRunningAsync()
        {
            try
            {
                using var conn = _connectionFactory.CreateConnection();
                var status = await conn.ExecuteScalarAsync<int>(@"
                    SELECT CASE 
                        WHEN EXISTS (SELECT 1 FROM master.sys.dm_server_services WHERE servicename LIKE 'SQL Server Agent%' AND status_desc = 'Running') THEN 1
                        ELSE 0
                    END");
                return status == 1;
            }
            catch
            {
                throw new InvalidOperationException("Không xác định được trạng thái Agent; kiểm tra kết nối và quyền xem dịch vụ SQL Server.");
            }
        }

        public async Task EnsureJobExistsAsync(string? dtexecPath = null, string? packagePath = null)
        {
            if (dtexecPath != null || packagePath != null)
                throw new ArgumentException("Agent now runs EtlRunner; configure RunnerPath and RunnerConfigPath.");
            var runner = Environment.GetEnvironmentVariable("KHANGNGHI_RUNNER_PATH") ?? _settings.RunnerPath;
            var config = Environment.GetEnvironmentVariable("KHANGNGHI_RUNNER_CONFIG") ?? _settings.RunnerConfigPath;
            EtlRuntime.ValidateCommandPath(runner);
            EtlRuntime.ValidateCommandPath(config);
            if (!runner.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("RunnerPath must point to the published EtlRunner.exe on the Agent host.");
            var command = $"\"{runner}\" --run --config \"{config}\"";
            using var conn = _connectionFactory.CreateConnection();
            const string sql = @"
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;
                BEGIN TRY
                    DECLARE @rc int, @jobId uniqueidentifier, @scheduleId int;
                    EXEC @rc = msdb.dbo.sp_getapplock @Resource=N'KhangNghi.DWMS.AgentSetup',
                        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
                    IF @rc < 0 THROW 51000, 'Cannot lock Agent configuration.', 1;
                    SELECT @jobId=job_id FROM msdb.dbo.sysjobs WHERE name=@JobName;
                    IF @jobId IS NULL
                    BEGIN
                        EXEC @rc = msdb.dbo.sp_add_job @job_name=@JobName, @enabled=0,
                            @description=N'DWMS runner: SSIS + SSAS + unified execution logs', @job_id=@jobId OUTPUT;
                        IF @rc <> 0 THROW 51000, 'Cannot create Agent job.', 1;
                        EXEC @rc = msdb.dbo.sp_add_jobstep @job_id=@jobId, @step_id=1,
                            @step_name=N'DWMS orchestrator', @subsystem=N'CmdExec', @command=@Command,
                            @cmdexec_success_code=0, @on_success_action=1, @on_fail_action=2, @retry_attempts=0;
                        IF @rc <> 0 THROW 51000, 'Cannot create Agent step.', 1;
                        EXEC @rc = msdb.dbo.sp_add_jobserver @job_id=@jobId, @server_name=N'(local)';
                        IF @rc <> 0 THROW 51000, 'Cannot attach Agent server.', 1;
                    END
                    ELSE
                    BEGIN
                        IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobactivity WHERE job_id=@jobId
                            AND session_id=(SELECT MAX(session_id) FROM msdb.dbo.syssessions)
                            AND start_execution_date IS NOT NULL AND stop_execution_date IS NULL)
                            THROW 51000, 'Job is running; stop configuration changes until it finishes.', 1;
                        IF (SELECT COUNT(*) FROM msdb.dbo.sysjobsteps WHERE job_id=@jobId) <> 1
                            THROW 51000, 'Existing job has unexpected steps; review manually.', 1;
                        IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobsteps WHERE job_id=@jobId AND step_id=1
                            AND (command<>@Command OR subsystem<>N'CmdExec'))
                        BEGIN
                            EXEC @rc = msdb.dbo.sp_update_job @job_id=@jobId, @enabled=0, @start_step_id=1;
                            IF @rc <> 0 THROW 51000, 'Cannot disable old job.', 1;
                        END;
                        EXEC @rc = msdb.dbo.sp_update_jobstep @job_id=@jobId, @step_id=1,
                            @step_name=N'DWMS orchestrator', @subsystem=N'CmdExec', @command=@Command,
                            @cmdexec_success_code=0, @on_success_action=1, @on_fail_action=2, @retry_attempts=0;
                        IF @rc <> 0 THROW 51000, 'Cannot update Agent step.', 1;
                    END;
                    IF (SELECT COUNT(*) FROM msdb.dbo.sysjobschedules WHERE job_id=@jobId) > 1
                        THROW 51000, 'Multiple schedules: review manually before updating.', 1;
                    SELECT @scheduleId=schedule_id FROM msdb.dbo.sysjobschedules WHERE job_id=@jobId;
                    IF @scheduleId IS NOT NULL AND EXISTS (SELECT 1 FROM msdb.dbo.sysjobschedules
                        WHERE schedule_id=@scheduleId AND job_id<>@jobId)
                        THROW 51000, 'Shared schedule: review manually before updating.', 1;
                    IF @scheduleId IS NULL
                    BEGIN
                        EXEC @rc = msdb.dbo.sp_add_jobschedule @job_id=@jobId, @name=N'KhangNghi_ETL_Schedule',
                            @enabled=0, @freq_type=4, @freq_interval=1, @active_start_time=20000;
                        IF @rc <> 0 THROW 51000, 'Cannot create Agent schedule.', 1;
                    END;
                    COMMIT;
                END TRY
                BEGIN CATCH
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    THROW;
                END CATCH";
            await conn.ExecuteAsync(sql, new { JobName = DefaultJobName, Command = command });
        }

        public async Task<SysSchedule> GetScheduleAsync()
        {
            var schedule = new SysSchedule { JobName = DefaultJobName, IsEnabled = false };

            try
            {
                using var conn = _connectionFactory.CreateConnection();

                const string query = @"
                IF (SELECT COUNT(*) FROM msdb.dbo.sysjobschedules js
                    JOIN msdb.dbo.sysjobs j ON j.job_id=js.job_id WHERE j.name=@JobName)>1
                    THROW 51000, 'Multiple schedules: review manually before choosing a schedule.', 1;
                SELECT TOP 1
                    j.name AS JobName,
                    j.enabled AS IsEnabled,
                    s.name AS ScheduleName,
                    s.enabled AS ScheduleEnabled,
                    s.freq_type,
                    s.active_start_time,
                    jh.run_date,
                    jh.run_time,
                    jh.run_status
                FROM msdb.dbo.sysjobs j
                LEFT JOIN msdb.dbo.sysjobschedules js ON j.job_id = js.job_id
                LEFT JOIN msdb.dbo.sysschedules s ON js.schedule_id = s.schedule_id
                OUTER APPLY (
                    SELECT TOP 1 run_date, run_time, run_status
                    FROM msdb.dbo.sysjobhistory h
                    WHERE h.job_id = j.job_id AND h.step_id = 0
                    ORDER BY instance_id DESC
                ) jh
                WHERE j.name = @JobName";

                var row = await conn.QueryFirstOrDefaultAsync<dynamic>(query, new { JobName = DefaultJobName });

                if (row != null)
                {
                    schedule.IsEnabled = row.IsEnabled == 1 && row.ScheduleEnabled != null && row.ScheduleEnabled == 1;

                    int startTime = row.active_start_time != null ? (int)row.active_start_time : 20000;
                    schedule.Hour = startTime / 10000;
                    schedule.Minute = (startTime % 10000) / 100;

                    int freqType = row.freq_type != null ? (int)row.freq_type : 4;
                    schedule.FrequencyType = freqType switch
                    {
                        1 => "Once",
                        4 => "Daily",
                        8 => "Weekly",
                        _ => "Daily"
                    };

                    if (row.run_date != null && row.run_time != null)
                    {
                        string dateStr = row.run_date.ToString().PadLeft(8, '0');
                        string timeStr = row.run_time.ToString().PadLeft(6, '0');
                        if (DateTime.TryParseExact($"{dateStr}{timeStr}", "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime lastRun))
                        {
                            schedule.LastRunTime = lastRun;
                        }
                    }

                    if (row.run_status != null)
                    {
                        int status = (int)row.run_status;
                        schedule.LastRunStatus = status switch
                        {
                            0 => "Failed",
                            1 => "Succeeded",
                            2 => "Retry",
                            3 => "Canceled",
                            _ => "In Progress"
                        };
                    }
                }
            }
            catch
            {
                throw new InvalidOperationException("Không đọc được lịch SQL Agent; kiểm tra kết nối và quyền msdb.");
            }

            return schedule;
        }

        public async Task<bool> UpdateScheduleAsync(bool isEnabled, int hour, int minute, string freq = "Daily")
        {
            if (hour is < 0 or > 23 || minute is < 0 or > 59 ||
                (freq != "Daily" && freq != "Weekly"))
                throw new ArgumentException("Use Daily or Weekly with a valid hour/minute.");
            try
            {
                await EnsureJobExistsAsync();

                using var conn = _connectionFactory.CreateConnection();
                conn.Open();
                using var transaction = conn.BeginTransaction();
                int timeInt = (hour * 10000) + (minute * 100);

                int freqType = freq.ToLower() switch
                {
                    "weekly" => 8,
                    "once" => 1,
                    _ => 4
                };

                const string sql = @"
                DECLARE @rc int;
                EXEC @rc = msdb.dbo.sp_getapplock @Resource=N'KhangNghi.DWMS.AgentSetup',
                    @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
                IF @rc < 0 THROW 51000, 'Cannot lock Agent configuration.', 1;
                EXEC @rc = msdb.dbo.sp_update_job @job_name = @JobName, @enabled = @JobEnabled;
                IF @rc <> 0 THROW 51000, 'Cannot update Agent job.', 1;

                -- Lấy schedule_id gắn với Job
                DECLARE @sched_id INT;
                SELECT TOP 1 @sched_id = js.schedule_id
                FROM msdb.dbo.sysjobs j
                JOIN msdb.dbo.sysjobschedules js ON j.job_id = js.job_id
                WHERE j.name = @JobName;

                IF @sched_id IS NULL THROW 51000, 'Agent schedule is missing.', 1;
                IF (SELECT COUNT(*) FROM msdb.dbo.sysjobschedules js
                    JOIN msdb.dbo.sysjobs j ON j.job_id=js.job_id WHERE j.name=@JobName)<>1
                    THROW 51000, 'Multiple schedules: review manually.', 1;
                IF (SELECT COUNT(*) FROM msdb.dbo.sysjobschedules WHERE schedule_id=@sched_id)<>1
                    THROW 51000, 'Shared schedule: review manually.', 1;
                IF @sched_id IS NOT NULL
                BEGIN
                    EXEC @rc = msdb.dbo.sp_update_schedule 
                        @schedule_id = @sched_id, 
                        @enabled = @JobEnabled, 
                        @freq_type = @FreqType,
                        @freq_interval = 1, -- daily: every day; weekly: Sunday
                        @freq_recurrence_factor = 1,
                        @active_start_time = @StartTime;
                    IF @rc <> 0 THROW 51000, 'Cannot update Agent schedule.', 1;
                END";

                await conn.ExecuteAsync(sql, new
                {
                    JobName = DefaultJobName,
                    JobEnabled = isEnabled ? 1 : 0,
                    FreqType = freqType,
                    StartTime = timeInt
                }, transaction);

                // Chốt thay đổi job/schedule trên HungNguyen\SQL2025.
                transaction.Commit();

                // Đồng bộ cấu hình hiển thị vào database quản trị trên HungNguyen.
                // Hai instance khác nhau: không dùng transaction của kết nối Agent.
                try
                {
                    using var managementConn =
                        _managementConnectionFactory.CreateConnection();

                    const string syncManagementSql = @"
        UPDATE dbo.Sys_Schedule
        SET IsEnabled = @IsEnabled,
            Hour = @Hour,
            Minute = @Minute,
            FrequencyType = @Freq
        WHERE JobName = @JobName;

        IF @@ROWCOUNT = 0
        BEGIN
            INSERT INTO dbo.Sys_Schedule
                (ScheduleName, JobName, FrequencyType,
                 Hour, Minute, IsEnabled)
            VALUES
                (@JobName, @JobName, @Freq,
                 @Hour, @Minute, @IsEnabled);
        END;";

                    await managementConn.ExecuteAsync(
                        syncManagementSql,
                        new
                        {
                            JobName = DefaultJobName,
                            IsEnabled = isEnabled,
                            Hour = hour,
                            Minute = minute,
                            Freq = freq
                        });
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Lịch SQL Agent đã được cập nhật trên SQL2025, " +
                        "nhưng chưa đồng bộ được Sys_Schedule trong " +
                        "KhangNghi_AppManagement. Hãy tải lại lịch từ Agent " +
                        "và kiểm tra kết nối database quản trị.",
                        ex);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi cập nhật lịch SQL Server Agent: " + ex.Message);
                throw;
            }
        }

        public async Task<bool> TriggerJobNowAsync()
        {
            try
            {
                await EnsureJobExistsAsync();
                using var conn = _connectionFactory.CreateConnection();
                await conn.ExecuteAsync(@"DECLARE @rc int;
                    EXEC @rc = msdb.dbo.sp_start_job @job_name = @JobName;
                    IF @rc <> 0 THROW 51000, 'SQL Agent did not accept the start request.', 1;",
                    new { JobName = DefaultJobName });
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi kích hoạt SQL Server Agent Job: " + ex.Message);
                throw;
            }
        }

        public async Task<List<JobHistoryDto>> GetJobHistoryAsync(int top = 20)
        {
            var list = new List<JobHistoryDto>();
            try
            {
                using var conn = _connectionFactory.CreateConnection();
                const string sql = @"
                SELECT TOP (@Top)
                    h.instance_id AS InstanceId,
                    j.name AS JobName,
                    h.step_name AS StepName,
                    h.run_date,
                    h.run_time,
                    h.run_status,
                    h.run_duration,
                    h.message AS Message
                FROM msdb.dbo.sysjobhistory h
                JOIN msdb.dbo.sysjobs j ON h.job_id = j.job_id
                WHERE j.name = @JobName
                ORDER BY h.instance_id DESC";

                var rows = await conn.QueryAsync<dynamic>(sql, new { Top = Math.Clamp(top, 1, 500), JobName = DefaultJobName });

                foreach (var r in rows)
                {
                    var dto = new JobHistoryDto
                    {
                        InstanceId = (int)r.InstanceId,
                        JobName = (string)r.JobName,
                        StepName = (string)r.StepName,
                        Message = (string)(r.Message ?? string.Empty)
                    };

                    int status = (int)r.run_status;
                    dto.RunStatus = status switch
                    {
                        0 => "Failed",
                        1 => "Succeeded",
                        2 => "Retry",
                        3 => "Canceled",
                        _ => "In Progress"
                    };

                    if (r.run_date != null && r.run_time != null)
                    {
                        string dateStr = r.run_date.ToString().PadLeft(8, '0');
                        string timeStr = r.run_time.ToString().PadLeft(6, '0');
                        if (DateTime.TryParseExact($"{dateStr}{timeStr}", "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                        {
                            dto.RunDateTime = dt;
                        }
                    }

                    if (r.run_duration != null)
                    {
                        int dur = (int)r.run_duration; // HHMMSS
                        int h = dur / 10000;
                        int m = (dur % 10000) / 100;
                        int s = dur % 100;
                        dto.RunDuration = new TimeSpan(h, m, s);
                    }

                    list.Add(dto);
                }
            }
            catch
            {
                throw new InvalidOperationException("Không đọc được lịch sử SQL Agent; kiểm tra kết nối và quyền msdb.");
            }

            return list;
        }
    }
}
