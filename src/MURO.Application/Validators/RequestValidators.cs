using FluentValidation;
using MURO.Application.DTOs.Auth;
using MURO.Application.DTOs.Support;
using MURO.Application.DTOs.Courses;
using MURO.Application.DTOs.Users;

namespace MURO.Application.Validators;

// ═══════════════════════════════════════════════════════════════
// AUTH VALIDATORS
// ═══════════════════════════════════════════════════════════════

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi gereklidir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre gereklidir.")
            .MinimumLength(6).WithMessage("Şifre en az 6 karakter olmalıdır.");
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta adresi gereklidir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre gereklidir.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .Matches("[A-Z]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermelidir.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad gereklidir.")
            .MaximumLength(100).WithMessage("Ad en fazla 100 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad gereklidir.")
            .MaximumLength(100).WithMessage("Soyad en fazla 100 karakter olabilir.");
    }
}

// ═══════════════════════════════════════════════════════════════
// SUPPORT VALIDATORS
// ═══════════════════════════════════════════════════════════════

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Konu gereklidir.")
            .MaximumLength(200).WithMessage("Konu en fazla 200 karakter olabilir.");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("İçerik gereklidir.")
            .MaximumLength(5000).WithMessage("İçerik en fazla 5000 karakter olabilir.");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Öncelik gereklidir.")
            .MaximumLength(20).WithMessage("Öncelik en fazla 20 karakter olabilir.");
    }
}

public class ReplyTicketRequestValidator : AbstractValidator<ReplyTicketRequest>
{
    public ReplyTicketRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Yanıt içeriği gereklidir.")
            .MaximumLength(5000).WithMessage("Yanıt en fazla 5000 karakter olabilir.");
    }
}

public class CreateFaqRequestValidator : AbstractValidator<CreateFaqRequest>
{
    public CreateFaqRequestValidator()
    {
        RuleFor(x => x.QuestionText)
            .NotEmpty().WithMessage("Soru metni gereklidir.")
            .MaximumLength(500).WithMessage("Soru en fazla 500 karakter olabilir.");

        RuleFor(x => x.AnswerText)
            .NotEmpty().WithMessage("Cevap metni gereklidir.")
            .MaximumLength(2000).WithMessage("Cevap en fazla 2000 karakter olabilir.");
    }
}

// ═══════════════════════════════════════════════════════════════
// COURSE VALIDATORS
// ═══════════════════════════════════════════════════════════════

public class CreateCourseRequestValidator : AbstractValidator<CreateCourseRequest>
{
    public CreateCourseRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Kurs başlığı gereklidir.")
            .MaximumLength(200).WithMessage("Başlık en fazla 200 karakter olabilir.");
    }
}

// ═══════════════════════════════════════════════════════════════
// USER VALIDATORS
// ═══════════════════════════════════════════════════════════════

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta gereklidir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad gereklidir.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad gereklidir.")
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre gereklidir.")
            .MinimumLength(6).WithMessage("Şifre en az 6 karakter olmalıdır.");
    }
}
