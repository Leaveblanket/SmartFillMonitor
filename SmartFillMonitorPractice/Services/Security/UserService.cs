using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SmartFillMonitor.Helper;
using SmartFillMonitor.Models;
using SmartFillMonitor.Models.Enum;
using SmartFillMonitor.Services.Session;

namespace SmartFillMonitor.Services.Security
{
    public class UserService : IUserService
    {
        #region 依赖配置与会话状态

        private const int SqliteConstraintErrorCode = 19;
        private const int SqliteUniqueConstraintExtendedErrorCode = 2067;
        private const int MaxFailedLoginCount = 5;
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);

        private readonly IAppDbContext _dbContext;
        private readonly ISessionService _sessionService;
        private readonly IAuditService _auditService;

        public UserService(
            IAppDbContext dbContext,
            ISessionService sessionService,
            IAuditService auditService)
        {
            _dbContext = dbContext;
            _sessionService = sessionService;
            _auditService = auditService;
        }

        /// <summary>
        /// 记录最后一次错误信息，供调用方读取
        /// </summary>
        public string LastErrorMessage { get; private set; } = string.Empty;

        /// <summary>
        /// 初始化用户服务（预留扩展点）
        /// </summary>
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region 用户注册入口

        /// <summary>
        /// 公开注册接口：创建新用户账号。
        /// 当系统已存在用户时，仅允许注册工程师（Engineer）角色，管理员（Admin）角色被拒绝。
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="password">明文密码，内部会进行标准化处理</param>
        /// <param name="role">期望注册的角色</param>
        /// <returns>注册成功返回 Success，失败返回 Failed</returns>
        public async Task<RegisterOperationResult> RegisterAsync(string userName, string password, Role role)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                return RegisterOperationResult.Fail(RegisterFailure.CredentialsMissing, "用户名或密码不能为空。");
            }

            PasswordHelper.EnsureStrongPassword(password);

            userName = userName.Trim();

            bool hasAdmin = await _dbContext.Fsql.Select<User>()
                .Where(u => u.Role == Role.Admin)
                .AnyAsync();

            // 安全策略：已有管理员时禁止通过公开注册再次创建管理员账号
            if (hasAdmin && role == Role.Admin)
            {
                return RegisterOperationResult.Fail(
                    RegisterFailure.AdminRegistrationNotAllowed,
                    "系统已有管理员时，公开注册只允许创建工程师账号。");
            }

            var exists = await _dbContext.Fsql.Select<User>()
                .Where(u => u.UserName == userName)
                .AnyAsync();
            if (exists)
            {
                return RegisterOperationResult.Fail(RegisterFailure.UserAlreadyExists, $"用户 {userName} 已存在。");
            }

            var passwordCredential = PasswordHelper.CreatePasswordCredential(userName, password);
            var user = new User
            {
                UserName = userName,
                PasswordCredential = passwordCredential,
                Role = role,
                PasswordChangedAt = DateTime.Now,
                CreatedAt = DateTime.Now,
            };

            try
            {
                await _dbContext.Fsql.Insert(user).ExecuteAffrowsAsync();
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                throw new BusinessException($"用户 {userName} 已存在。");
            }

            _auditService.Security("PublicRegister", "Success", $"已创建 {role} 用户：{userName}", userName);

            return RegisterOperationResult.Success();
        }

        #endregion

        #region 登录认证与锁定处理

        /// <summary>
        /// 用户登录认证：验证用户名密码，并处理登录失败锁定逻辑。
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="password">明文密码</param>
        /// <returns>登录成功返回 Success，失败返回 Failed（含锁定信息）</returns>
        public async Task<LoginOperationResult> AuthenticateAsync(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                LastErrorMessage = "用户名或密码不能为空。";
                return LoginOperationResult.Fail(LoginFailure.CredentialsMissing, LastErrorMessage);
            }

            LastErrorMessage = string.Empty;
            userName = userName.Trim();

            try
            {
                // 根据用户名查询用户实体
                var user = await _dbContext.Fsql.Select<User>()
                    .Where(u => u.UserName == userName)
                    .FirstAsync();

                // 前置检查：账户是否被锁定或禁用
                var preLoginResult = await EnsureUserCanLoginAsync(user, userName);
                if (preLoginResult != null)
                {
                    LastErrorMessage = preLoginResult.Message;
                    return preLoginResult;
                }

                // 密码校验失败时处理失败计数与锁定
                if (!PasswordHelper.VerifyPassword(user, password))
                {
                    var failedLoginError = await HandleFailedLoginAsync(user);
                    LastErrorMessage = failedLoginError;
                    return LoginOperationResult.Fail(LoginFailure.UserNotFoundOrPasswordInvalid, failedLoginError);
                }

                // 登录成功，完成会话创建等后续处理
                return await CompleteSuccessfulLoginAsync(user, userName);
            }
            catch (Exception ex)
            {
                LogHelper.Error("用户登录验证失败", ex);
                LastErrorMessage = "登录验证失败，请稍后重试。";
                throw new InfrastructureException("登录验证失败，请稍后重试。", ex);
            }
        }

        #endregion

        #region 密码修改与重置入口

        public async Task<ChangePasswordOperationResult> ChangeCurrentUserPasswordAsync(string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                return ChangePasswordOperationResult.Fail(
                    string.IsNullOrWhiteSpace(currentPassword)
                        ? ChangePasswordFailure.CurrentPasswordMissing
                        : ChangePasswordFailure.NewPasswordMissing,
                    string.IsNullOrWhiteSpace(currentPassword)
                        ? "请输入当前密码。"
                        : "请输入新密码。");
            }

            if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            {
                return ChangePasswordOperationResult.Fail(
                    ChangePasswordFailure.NewPasswordSameAsCurrent,
                    "新密码不能与当前密码相同。");
            }

            var (user, errorMessage) = await TryGetCurrentUserForPasswordChangeAsync();
            if (user == null)
            {
                return ChangePasswordOperationResult.Fail(
                    ChangePasswordFailure.CurrentUserUnavailable,
                    errorMessage ?? "当前用户不存在或无法修改密码。");
            }

            if (!PasswordHelper.VerifyPassword(user, currentPassword))
            {
                return ChangePasswordOperationResult.Fail(
                    ChangePasswordFailure.CurrentPasswordInvalid,
                    "当前密码不正确。");
            }

            try
            {
                await ApplyPasswordChangeAsync(user, newPassword);
            }
            catch (BusinessException ex)
            {
                return ChangePasswordOperationResult.Fail(
                    ChangePasswordFailure.NewPasswordTooWeak,
                    ex.Message);
            }

            _sessionService.SetCurrentUser(user);
            return ChangePasswordOperationResult.Success();
        }

        #endregion

        #region 会话退出

        public Task LogoutAsync()
        {
            if (_sessionService.IsLoggedIn)
            {
                _sessionService.Clear();
            }

            LastErrorMessage = string.Empty;
            return Task.CompletedTask;
        }

        #endregion

        #region 用户查询

        public async Task<List<User>> GetLoginUsersAsync()
        {
            try
            {
                return await _dbContext.Fsql.Select<User>()
                    .Where(u => !u.IsDisabled && u.UserName != "")
                    .OrderBy(u => u.UserName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error("加载登录用户列表失败", ex);
                throw new InfrastructureException("加载登录用户列表失败。", ex);
            }
        }

        #endregion


        #region 用户写入与登录状态更新

        private async Task<LoginOperationResult?> EnsureUserCanLoginAsync(User? user, string userName)
        {
            if (user == null)
            {
                return LoginOperationResult.Fail(LoginFailure.UserNotFoundOrPasswordInvalid, "用户名或密码错误。");
            }

            if (user.IsDisabled)
            {
                _auditService.Security("Login", "Failed", "用户已被禁用。", userName);
                return LoginOperationResult.Fail(LoginFailure.UserDisabled, "当前用户已被禁用。");
            }

            await TryAutoUnlockAsync(user);

            if (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.Now)
            {
                return null;
            }

            return LoginOperationResult.Fail(
                LoginFailure.UserLocked,
                $"账户已锁定，请于 {user.LockedUntil.Value:yyyy-MM-dd HH:mm:ss} 后重试。");
        }

        private async Task<string> HandleFailedLoginAsync(User user)
        {
            user.FailedLoginCount += 1;
            user.LastFailedLoginTime = DateTime.Now;

            if (user.FailedLoginCount >= MaxFailedLoginCount)
            {
                user.LockedUntil = DateTime.Now.Add(LockDuration);
            }

            await _dbContext.Fsql.Update<User>()
                .Where(u => u.Id == user.Id)
                .Set(u => u.FailedLoginCount, user.FailedLoginCount)
                .Set(u => u.LastFailedLoginTime, user.LastFailedLoginTime)
                .Set(u => u.LockedUntil, user.LockedUntil)
                .ExecuteAffrowsAsync();

            return user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.Now
                ? $"密码错误次数过多，账户已锁定至 {user.LockedUntil.Value:yyyy-MM-dd HH:mm:ss}。"
                : "用户名或密码错误。";
        }

        private async Task<bool> TryAutoUnlockAsync(User user)
        {
            if (!user.LockedUntil.HasValue || user.LockedUntil.Value > DateTime.Now)
            {
                return false;
            }

            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.LastFailedLoginTime = null;

            await _dbContext.Fsql.Update<User>()
                .Where(u => u.Id == user.Id)
                .Set(u => u.FailedLoginCount, 0)
                .Set(u => u.LockedUntil, null)
                .Set(u => u.LastFailedLoginTime, null)
                .ExecuteAffrowsAsync();
            return true;
        }

        private async Task<LoginOperationResult> CompleteSuccessfulLoginAsync(User user, string userName)
        {
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.LastFailedLoginTime = null;
            user.LastLoginTime = DateTime.Now;

            await _dbContext.Fsql.Update<User>()
                .SetSource(user)
                .UpdateColumns(u => new
                {
                    u.PasswordCredential,
                    u.FailedLoginCount,
                    u.LockedUntil,
                    u.LastFailedLoginTime,
                    u.LastLoginTime,
                    u.PasswordChangedAt
                })
                .ExecuteAffrowsAsync();

            _sessionService.SetCurrentUser(user);
            _auditService.Security("Login", "Success", "登录成功。", userName);
            return LoginOperationResult.Success();
        }

        private static bool IsUniqueViolation(Exception ex)
        {
            if (TryGetSqliteException(ex, out var sqliteException) && sqliteException != null)
            {
                return sqliteException.SqliteExtendedErrorCode == SqliteUniqueConstraintExtendedErrorCode
                       || (sqliteException.SqliteErrorCode == SqliteConstraintErrorCode
                           && sqliteException.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase));
            }

            var message = ex.ToString();
            return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("SQLITE_CONSTRAINT", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<(User? user, string? errorMessage)> TryGetCurrentUserForPasswordChangeAsync()
        {
            var sessionUser = _sessionService.CurrentUser;
            if (sessionUser == null)
            {
                return (null, "当前未登录，无法修改密码。");
            }

            var user = await _dbContext.Fsql.Select<User>()
                .Where(u => u.Id == sessionUser.Id)
                .FirstAsync();
            if (user == null)
            {
                return (null, $"当前用户 {sessionUser.UserName} 不存在。");
            }

            if (user.IsDisabled)
            {
                return (null, "当前用户已被禁用，无法修改密码。");
            }

            return (user, null);
        }

        private static bool TryGetSqliteException(Exception ex, out SqliteException? sqliteException)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SqliteException candidate)
                {
                    sqliteException = candidate;
                    return true;
                }
            }

            sqliteException = null;
            return false;
        }

        private async Task ApplyPasswordChangeAsync(User user, string newPassword)
        {
            PasswordHelper.EnsureStrongPassword(newPassword);

            var passwordCredential = PasswordHelper.CreatePasswordCredential(user.UserName, newPassword);
            var now = DateTime.Now;

            user.PasswordCredential = passwordCredential;
            user.PasswordChangedAt = now;
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.LastFailedLoginTime = null;

            await _dbContext.Fsql.Update<User>()
                .Where(u => u.Id == user.Id)
                .Set(u => u.PasswordCredential, user.PasswordCredential)
                .Set(u => u.PasswordChangedAt, now)
                .Set(u => u.FailedLoginCount, 0)
                .Set(u => u.LockedUntil, null)
                .Set(u => u.LastFailedLoginTime, null)
                .ExecuteAffrowsAsync();

            _auditService.Security("ChangePassword", "Success", "用户已通过旧密码校验完成主动改密。", user.UserName);
        }
        #endregion

    }
}
