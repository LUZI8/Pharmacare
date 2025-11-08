using Microsoft.AspNetCore.Mvc;
using PharmaCare.Models;
using PharmaCare.Repositories.Interface;
using PharmaCare.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PharmaCare.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly EmailSender _emailSender;

        public AccountController(
            IUserRepository userRepository,
            ICategoryRepository categoryRepository,
            EmailSender emailSender)
        {
            _userRepository = userRepository;
            _categoryRepository = categoryRepository;
            _emailSender = emailSender;
        }

        private void LoadCategories()
        {
            var categories = _categoryRepository.View();
            ViewBag.Categories = categories;
        }

        // ================================
        // ✅ LOGIN
        // ================================
        public IActionResult Login()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            var userRole = HttpContext.Session.GetString("UserRole");
            if (!string.IsNullOrEmpty(userRole))
            {
                return Redirect(userRole == "Admin" ? "/Admin/Index" : "/FrontEnd/Index");
            }

            Console.WriteLine("GET Login page accessed");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string Email, string Password)
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";

            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ModelState.AddModelError("", "Email and password are required");
                return View();
            }

            var user = await _userRepository.GetByEmailAsync(Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View();
            }

            var isValid = await _userRepository.ValidateCredentialsAsync(Email, Password);
            if (!isValid)
            {
                ModelState.AddModelError("", "Invalid email or password");
                return View();
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError("", "Please verify your email before logging in.");
                return View();
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "Your account is inactive. Please contact an administrator.");
                return View();
            }

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
            HttpContext.Session.SetString("UserRole", user.Role);

            var redirectUrl = (user.Role == "Admin" || user.Role == "Pharmacist")
                ? "/Admin/Index?loggedIn=true"
                : "/FrontEnd/Index?loggedIn=true";

            return Redirect(redirectUrl);
        }

        // ================================
        // ✅ LOGOUT
        // ================================
        public IActionResult Logout()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            HttpContext.Session.Clear();
            return Redirect("/Account/Login");
        }

        // ================================
        // ✅ REGISTER (with OTP)
        // ================================
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User user, string confirmPassword)
        {
            try
            {
                if (user.Password != confirmPassword)
                {
                    ModelState.AddModelError("", "Password and confirmation do not match");
                    return View(user);
                }

                if (user.Password.Length < 8 || !user.Password.Any(char.IsUpper) || !user.Password.Any(c => !char.IsLetterOrDigit(c)))
                {
                    ModelState.AddModelError("", "Password must be 8+ chars, contain uppercase and special character");
                    return View(user);
                }

                if (await _userRepository.UserExistsAsync(user.Email))
                {
                    ModelState.AddModelError("Email", "Email already exists");
                    return View(user);
                }

                user.Role ??= "User";

                // توليد رمز تحقق (OTP)
                var otp = new Random().Next(100000, 999999).ToString();
                user.EmailOtp = otp;
                user.EmailOtpExpiresAt = DateTime.UtcNow.AddMinutes(10);
                user.EmailConfirmed = false;

                var newUser = await _userRepository.CreateAsync(user);
                if (newUser == null)
                {
                    TempData["ErrorMessage"] = "Registration failed.";
                    return View(user);
                }

                // إرسال الإيميل بالرمز
                var subject = "Your PharmaCare verification code";
                var body = $@"
                    <h2>Verify your email</h2>
                    <p>Hello {newUser.FirstName},</p>
                    <p>Your verification code is:</p>
                    <h1 style='color:blue;'>{otp}</h1>
                    <p>This code expires in 10 minutes.</p>";

                try
                {
                    await _emailSender.SendEmailAsync(newUser.Email, subject, body);
                    Console.WriteLine($"✅ OTP email sent to {newUser.Email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Email send failed: {ex.Message}");
                    Console.WriteLine($"OTP for testing: {otp}");
                }

                // ✅ حفظ الإيميل في الـ Session
                HttpContext.Session.SetString("PendingEmail", newUser.Email);

                TempData["SuccessMessage"] = "Account created! Please check your email for the verification code.";
                return RedirectToAction("VerifyOtp");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Registration error: {ex.Message}");
                ModelState.AddModelError("", $"Registration error: {ex.Message}");
                return View(user);
            }
        }

        // ================================
        // ✅ VERIFY OTP
        // ================================
        [HttpGet]
        public async Task<IActionResult> VerifyOtp()
        {
            var email = HttpContext.Session.GetString("PendingEmail");
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Session expired. Please register again.";
                return RedirectToAction("Register");
            }

            // تحقق من وجود المستخدم
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found. Please register again.";
                HttpContext.Session.Remove("PendingEmail");
                return RedirectToAction("Register");
            }

            if (user.EmailConfirmed)
            {
                TempData["InfoMessage"] = "Email already verified!";
                HttpContext.Session.Remove("PendingEmail");
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string otp)
        {
            var email = HttpContext.Session.GetString("PendingEmail");
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Session expired. Please try again.";
                return RedirectToAction("Register");
            }

            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Register");
            }

            // تحقق من صلاحية الـ OTP
            if (user.EmailOtpExpiresAt < DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "Verification code has expired. Please request a new code.";
                return View();
            }

            // تحقق من صحة الـ OTP
            if (user.EmailOtp != otp)
            {
                TempData["ErrorMessage"] = "Invalid verification code. Please try again.";
                return View();
            }

            // تحديث حالة البريد الإلكتروني إلى مؤكد
            user.EmailConfirmed = true;
            user.EmailOtp = null;
            user.EmailOtpExpiresAt = null;

            await _userRepository.UpdateAsync(user);
            HttpContext.Session.Remove("PendingEmail");

            TempData["SuccessMessage"] = "Email successfully confirmed! You can now login.";
            return RedirectToAction("Login");
        }

        // ================================
        // ✅ RESEND OTP
        // ================================
        [HttpGet]
        public async Task<IActionResult> ResendOtp()
        {
            var email = HttpContext.Session.GetString("PendingEmail");
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Session expired. Please register again.";
                return RedirectToAction("Register");
            }

            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Register");
            }

            if (user.EmailConfirmed)
            {
                TempData["InfoMessage"] = "Email already verified!";
                HttpContext.Session.Remove("PendingEmail");
                return RedirectToAction("Login");
            }

            // توليد كود جديد
            var otp = new Random().Next(100000, 999999).ToString();
            user.EmailOtp = otp;
            user.EmailOtpExpiresAt = DateTime.UtcNow.AddMinutes(10);

            try
            {
                var subject = "Your New PharmaCare Verification Code";
                var body = $@"
                    <h2>New Verification Code</h2>
                    <p>Hello {user.FirstName},</p>
                    <p>Your new verification code is:</p>
                    <h1 style='color:blue;'>{otp}</h1>
                    <p>This code will expire in 10 minutes.</p>";

                await _emailSender.SendEmailAsync(user.Email, subject, body);
                Console.WriteLine($"✅ New OTP sent to {user.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Resend email failed: {ex.Message}");
                Console.WriteLine($"OTP for testing: {otp}");
            }

            await _userRepository.UpdateAsync(user);
            TempData["SuccessMessage"] = "A new verification code has been sent to your email.";
            return RedirectToAction("VerifyOtp");
        }
        public IActionResult ForgotPassword()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("", "Email is required.");
                return View();
            }

            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "No user found with this email.");
                return View();
            }

            // توليد توكين استرجاع كلمة المرور
            var resetToken = Guid.NewGuid().ToString();

            // حفظ التوكين في قاعدة البيانات مع تاريخ انتهاء صلاحية
            user.PasswordResetToken = resetToken;
            user.PasswordResetExpiresAt = DateTime.UtcNow.AddHours(1); // التوكين صالح لمدة ساعة
            await _userRepository.UpdateAsync(user);

            // إرسال التوكين عبر البريد الإلكتروني
            var resetLink = Url.Action("ResetPassword", "Account", new { token = resetToken }, protocol: Request.Scheme);
            var subject = "Password Reset Request";
            var body = $@"
        <h2>Password Reset Request</h2>
        <p>Hello {user.FirstName},</p>
        <p>You requested a password reset. Click the link below to reset your password:</p>
        <a href='{resetLink}'>Reset your password</a>
        <p>This link will expire in 1 hour.</p>";

            try
            {
                await _emailSender.SendEmailAsync(user.Email, subject, body);
                TempData["SuccessMessage"] = "A password reset link has been sent to your email.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to send password reset email. Please try again.";
            }

            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest("Invalid or expired token.");
            }

            var user = await _userRepository.GetByResetTokenAsync(token);
            if (user == null || user.PasswordResetExpiresAt < DateTime.UtcNow)
            {
                return BadRequest("Token is invalid or has expired.");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string token, string newPassword)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newPassword))
            {
                ModelState.AddModelError("", "Invalid request.");
                return View();
            }

            var user = await _userRepository.GetByResetTokenAsync(token);
            if (user == null || user.PasswordResetExpiresAt < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "Token is invalid or has expired.");
                return View();
            }

            user.Password = newPassword; // يجب أن تقوم بتطبيق تشفير كلمة المرور هنا
            user.PasswordResetToken = null;
            user.PasswordResetExpiresAt = null;

            await _userRepository.UpdateAsync(user);

            TempData["SuccessMessage"] = "Your password has been successfully reset!";
            return RedirectToAction("Login", "Account");
        }

    }
}