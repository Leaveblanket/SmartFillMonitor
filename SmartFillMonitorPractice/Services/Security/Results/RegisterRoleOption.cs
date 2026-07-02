using SmartFillMonitor.Models.Enum;

namespace SmartFillMonitor.Services.Security
{
    public sealed class RegisterRoleOption
    {
        public RegisterRoleOption(Role role, string label)
        {
            Role = role;
            Label = label;
        }

        public Role Role { get; }

        public string Label { get; }
    }
}
