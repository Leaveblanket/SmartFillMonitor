using System.Collections.Generic;
using System.Threading.Tasks;
using SmartFillMonitor.Models;

namespace SmartFillMonitor.Services.Security
{
    /// <summary>
    /// 负责用户账户相关核心业务，包括登录、注册、登出和密码修改。
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// 初始化用户服务及其依赖状态。
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 注册新用户。
        /// </summary>
        Task<RegisterOperationResult> RegisterAsync(string userName, string password, Role role);

        /// <summary>
        /// 校验用户名和密码并执行登录。
        /// </summary>
        Task<LoginOperationResult> AuthenticateAsync(string userName, string password);

        /// <summary>
        /// 修改当前登录用户密码。
        /// </summary>
        Task<ChangePasswordOperationResult> ChangeCurrentUserPasswordAsync(string currentPassword, string newPassword);

        /// <summary>
        /// 退出当前登录会话。
        /// </summary>
        Task LogoutAsync();

        /// <summary>
        /// 获取允许在登录页中选择的用户列表。
        /// </summary>
        Task<List<User>> GetLoginUsersAsync();
    }
}
