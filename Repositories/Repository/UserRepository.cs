using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace Repositories.Repository
{
    /* Repository implementation for user management with secure password handling */
    public class UserRepository : IUserRepository
    {
        private readonly DataDbContext _context;

        /* Constructor injection for database context */
        public UserRepository(DataDbContext context)
        {
            _context = context;
        }

        /* Retrieve all users for admin management */
        public async Task<IEnumerable<User>> GetAllAsync()
        {
            return await _context.User.ToListAsync();
        }

        /* Find user by unique identifier */
        public async Task<User> GetByIdAsync(int id)
        {
            return await _context.User.FindAsync(id);
        }

        /* Find user by email with case-insensitive comparison */
        public async Task<User> GetByEmailAsync(string email)
        {
            return await _context.User.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        /* Create new user with password hashing and role validation */
        public async Task<User> CreateAsync(User user)
        {
            /* Hash password for security before storing */
            user.Password = HashPassword(user.Password);
            user.DateCreated = DateTime.UtcNow;
            user.IsActive = true;

            /* Set default role if not specified */
            if (string.IsNullOrEmpty(user.Role))
            {
                user.Role = "Customer";
            }

            /* Validate role against allowed values */
            string[] validRoles = new[] { "Admin", "Customer", "Pharmacist" };
            if (!validRoles.Contains(user.Role))
            {
                user.Role = "Customer";
            }

            /* Debug logging for development troubleshooting */
            Console.WriteLine($"Creating user: {user.Email}");
            Console.WriteLine($"Password hash: {user.Password}");
            Console.WriteLine($"Role: {user.Role}");

            _context.User.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        /* Update user information with selective password handling */
        public async Task<User> UpdateAsync(User user)
        {
            var existingUser = await _context.User.FindAsync(user.UserId);

            if (existingUser == null)
                return null;

            /* Update all user properties except password */
            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Email = user.Email;
            existingUser.Role = user.Role;
            existingUser.Address = user.Address;
            existingUser.City = user.City;
            existingUser.PhoneNumber = user.PhoneNumber;
            existingUser.IsActive = user.IsActive;

            /* Handle password updates only when explicitly marked for reset or changed */
            if (!string.IsNullOrEmpty(user.Password) &&
                (user.Password.StartsWith("RESET_PASSWORD_") || user.Password != existingUser.Password))
            {
                string passwordToHash = user.Password;
                /* Strip reset prefix if present */
                if (passwordToHash.StartsWith("RESET_PASSWORD_"))
                {
                    passwordToHash = passwordToHash.Substring("RESET_PASSWORD_".Length);
                }

                existingUser.Password = HashPassword(passwordToHash);
            }

            _context.User.Update(existingUser);
            await _context.SaveChangesAsync();
            return existingUser;
        }

        /* Delete user account by ID */
        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _context.User.FindAsync(id);

            if (user == null)
                return false;

            _context.User.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        /* Check email existence for registration validation */
        public async Task<bool> UserExistsAsync(string email)
        {
            return await _context.User.AnyAsync(u => u.Email.ToLower() == email.ToLower());
        }

        /* Authenticate user credentials with password verification */
        public async Task<bool> ValidateCredentialsAsync(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                return false;

            /* Find active user with matching email */
            var user = await _context.User
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive);

            if (user == null)
                return false;

            /* Verify password against stored hash */
            return VerifyPassword(password, user.Password);
        }

        /* SHA256 password hashing for secure storage */
        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return null;

            /* Use SHA256 algorithm for consistent hashing */
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        /* Verify input password against stored hash */
        private bool VerifyPassword(string inputPassword, string storedHashedPassword)
        {
            if (string.IsNullOrEmpty(inputPassword) || string.IsNullOrEmpty(storedHashedPassword))
                return false;

            /* Hash input password and compare with stored hash */
            var newHash = HashPassword(inputPassword);
            return string.Equals(newHash, storedHashedPassword);
        }

        /* Debug method for testing password validation in development */
        public async Task<bool> TestPasswordForUser(string email, string testPassword)
        {
            var user = await _context.User.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                Console.WriteLine($"User not found: {email}");
                return false;
            }

            string storedHash = user.Password;
            string newHash = HashPassword(testPassword);

            /* Debug output for password verification troubleshooting */
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Test Password: {testPassword}");
            Console.WriteLine($"Stored Hash: {storedHash}");
            Console.WriteLine($"New Hash: {newHash}");
            Console.WriteLine($"Match Result: {newHash == storedHash}");

            return newHash == storedHash;
        }
        public async Task<User?> GetByConfirmationTokenAsync(string token)
        {
            // البحث عن المستخدم الذي يملك رمز تأكيد البريد
            return await _context.User
                .FirstOrDefaultAsync(u => u.EmailConfirmationToken == token);
        }
        public async Task<User?> GetByOtpAsync(string otp)
        {
            return await _context.User.FirstOrDefaultAsync(u => u.EmailOtp == otp);
        }
        public async Task<User?> GetByResetTokenAsync(string token)
        {
            return await _context.User.FirstOrDefaultAsync(u => u.PasswordResetToken == token);
        }






    }
}