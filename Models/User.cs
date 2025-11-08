namespace PharmaCare.Models
{
    /* Entity model representing system users including customers and administrators */
    public class User
    {
        /* Primary key for user identification */
        public int UserId { get; set; }

        /* User's first name for personalization and identification */
        public string FirstName { get; set; }

        /* User's last name for complete identification */
        public string LastName { get; set; }

        /* Email address used for login and communication */
        public string Email { get; set; }

        /* Password stored as SHA256 hash for security protection */
        public string Password { get; set; }

        /* User role for authorization (Admin, Customer, Pharmacist) */
        public string Role { get; set; }

        /* Physical address for delivery and billing purposes */
        public string Address { get; set; }

        /* City for shipping calculations and delivery coordination */
        public string City { get; set; }

        /* Contact phone number for order communication */
        public string PhoneNumber { get; set; }

        /* Timestamp when user account was initially created */
        public DateTime DateCreated { get; set; }

        /* Boolean flag indicating if user account is currently active */
        public bool IsActive { get; set; }
        public bool EmailConfirmed { get; set; } = false;
        public string? EmailConfirmationToken { get; set; }
        public DateTime? EmailConfirmationTokenExpiresAt { get; set; }
        public string? EmailOtp { get; set; } // رمز التحقق المرسل عبر البريد
        public DateTime? EmailOtpExpiresAt { get; set; } // وقت انتهاء صلاحية الرمز
        public string PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpiresAt { get; set; }

    }
}