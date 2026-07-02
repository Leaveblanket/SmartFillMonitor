using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using SmartFillMonitor.Models;
using SmartFillMonitor.Services;

namespace SmartFillMonitor.Helper
{
    /// <summary>
    /// 密码工具类：负责密码的规范化、强度校验、哈希生成与验证。
    /// 使用 ASP.NET Core Identity 的 <see cref="PasswordHasher{TUser}"/> 实现安全哈希。
    /// </summary>
    public static class PasswordHelper
    {
        private static readonly PasswordHasher<User> PasswordHasher = new();

        public static void EnsureStrongPassword(string password)
        {
            password = NormalizePasswordInput(password);
            if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
            {
                throw new BusinessException("密码至少需要 10 位，并包含大写字母、小写字母和数字。");
            }

            var hasUpper = password.Any(char.IsUpper);
            var hasLower = password.Any(char.IsLower);
            var hasDigit = password.Any(char.IsDigit);
            if (!hasUpper || !hasLower || !hasDigit)
            {
                throw new BusinessException("密码必须同时包含大写字母、小写字母和数字。");
            }
        }

        public static string CreatePasswordCredential(string userName, string password)
        {
            password = NormalizePasswordInput(password);
            return PasswordHasher.HashPassword(new User { UserName = userName }, password);
        }

        public static bool VerifyPassword(User user, string password)
        {
            var normalizedPassword = NormalizePasswordInput(password);
            if (string.IsNullOrWhiteSpace(user.PasswordCredential))
            {
                return false;
            }

            var result = PasswordHasher.VerifyHashedPassword(user, user.PasswordCredential, normalizedPassword);
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordCredential = CreatePasswordCredential(user.UserName, normalizedPassword);
                return true;
            }

            return result == PasswordVerificationResult.Success;
        }

        private static string NormalizePasswordInput(string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            var start = 0;
            var end = password.Length - 1;

            while (start <= end
                   && (char.IsWhiteSpace(password[start]) || password[start] == '\u200B' || password[start] == '\uFEFF'))
            {
                start++;
            }

            while (end >= start
                   && (char.IsWhiteSpace(password[end]) || password[end] == '\u200B' || password[end] == '\uFEFF'))
            {
                end--;
            }

            return start > end ? string.Empty : password.Substring(start, end - start + 1);
        }
    }
}
