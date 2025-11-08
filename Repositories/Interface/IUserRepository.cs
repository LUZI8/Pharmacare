namespace PharmaCare.Repositories.Interface
{
    /* Repository interface for user management and authentication operations */
    public interface IUserRepository
    {
        /* Retrieve all users in the system for admin management */
        Task<IEnumerable<User>> GetAllAsync();

        /* Get single user by unique user ID */
        Task<User> GetByIdAsync(int id);

        /* Find user by email address for login and account operations */
        Task<User> GetByEmailAsync(string email);

        /* Create new user account and return created user entity */
        Task<User> CreateAsync(User user);

        /* Update existing user information and return updated entity */
        Task<User> UpdateAsync(User user);

        /* Delete user account by ID - returns success status */
        Task<bool> DeleteAsync(int id);

        /* Check if user account exists with given email - returns boolean for validation */
        Task<bool> UserExistsAsync(string email);

        /* Authenticate user credentials during login process - validates email/password combination */
        Task<bool> ValidateCredentialsAsync(string email, string password);
        Task<User?> GetByConfirmationTokenAsync(string token);
        Task<User?> GetByOtpAsync(string otp);
        Task<User?> GetByResetTokenAsync(string token);



    }
}