using System.ComponentModel.DataAnnotations;

namespace SoundMoney.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email or Username is required.")]
        [Display(Name = "Email or Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public class UserViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Active", "Inactive", "Pending"
        public DateTime LastLogin { get; set; }
    }

    public class AccountDashboardViewModel
    {
        public List<UserViewModel> Users { get; set; } = new();
    }
}