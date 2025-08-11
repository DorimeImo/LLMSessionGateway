using FluentValidation;
using LLMSessionGateway.API.DTOs;

namespace LLMSessionGateway.API.Validation
{
    public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
    {
        public SendMessageRequestValidator()
        {
            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("Message is required.");

            RuleFor(x => x.MessageId)
                .MaximumLength(100)
                .Matches("^[a-zA-Z0-9-_]+$").When(x => !string.IsNullOrWhiteSpace(x.MessageId));
        }
    }
}

