using FluentValidation;
using LLMSessionGateway.API.DTOs;

namespace LLMSessionGateway.API.Validation
{
    public class StreamReplyRequestValidator : AbstractValidator<StreamReplyRequest>
    {
        public StreamReplyRequestValidator()
        {
            RuleFor(x => x.ParentMessageId)
                .NotEmpty().WithMessage("ParentMessageId is required.");
        }
    }
}
