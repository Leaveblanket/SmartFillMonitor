using System;
using System.ComponentModel;
using FreeSql.DataAnnotations;
using SmartFillMonitor.Models.Enum;

namespace SmartFillMonitor.Models
{
    [Table(Name = "Users")]
    [Index("idx_unique_username", "UserName", true)]
    public class User
    {
        [Column(IsPrimary = true, IsIdentity = true)]
        public long Id { get; set; }

        [Column(StringLength = 50, IsNullable = false)]
        public string UserName { get; set; } = string.Empty;

        [Column(Name = "PasswordCredential", StringLength = 512, IsNullable = false)]
        public string PasswordCredential { get; set; } = string.Empty;

        [Column(MapType = typeof(int))]
        public Role Role { get; set; }

        public bool IsDisabled { get; set; }

        public int FailedLoginCount { get; set; }

        public DateTime? LockedUntil { get; set; }

        public DateTime? LastFailedLoginTime { get; set; }

        public DateTime? PasswordChangedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLoginTime { get; set; }

        [Column(IsIgnore = true)]
        public string RoleName => Role switch
        {
            Role.Admin => "管理员",
            Role.Engineer => "工程师",
            _ => "未知",
        };
    }
}
